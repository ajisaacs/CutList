"""Focused regression checks for job-level actions on the Results tab."""

from __future__ import annotations

import os
import re
import subprocess
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
JOB_EDITOR = REPO_ROOT / "CutList.Web/Components/Pages/Jobs/Edit.razor"


class JobResultsActionsTests(unittest.TestCase):
    def css_declarations(self, selector: str) -> dict[str, str]:
        css = (REPO_ROOT / "CutList.Web/wwwroot/css/app.css").read_text()
        match = re.search(r"(?:^|\n)" + re.escape(selector) + r"\s*\{([^}]+)\}", css)
        if match is None:
            self.fail(f"Missing scoped style for {selector}")
        return dict(
            (name.strip(), value.strip())
            for name, value in (declaration.split(":", 1) for declaration in match[1].split(";") if ":" in declaration)
        )

    def test_results_toolbar_wraps_complete_items_with_center_alignment_and_gaps(self) -> None:
        declarations = self.css_declarations(".results-toolbar")
        self.assertEqual("flex", declarations.get("display"))
        self.assertEqual("wrap", declarations.get("flex-wrap"))
        self.assertEqual("center", declarations.get("align-items"))
        self.assertEqual(".5rem .75rem", declarations.get("gap"))
        items = self.css_declarations(".results-toolbar > *")
        self.assertEqual("0 0 auto", items.get("flex"))
        self.assertEqual("100%", items.get("max-width"))

    def test_results_toolbar_does_not_mix_inline_spacing_utilities_with_flex_gaps(self) -> None:
        markup = JOB_EDITOR.read_text()
        if '<div class="results-toolbar mb-3">' not in markup:
            self.fail("Missing dedicated Results toolbar")
        start = markup.index('<div class="results-toolbar mb-3">')
        end = markup.index("@if (SelectedEngine != null)", start)
        toolbar = markup[start:end]
        classes = " ".join(re.findall(r'class="([^"]+)"', toolbar)).split()
        self.assertNotIn("d-inline-flex", classes)
        self.assertNotIn("w-auto", classes)
        self.assertFalse(any(re.fullmatch(r"m[se]-[23]", name) for name in classes))
        self.assertIn('class="packing-engine-picker print-screen-only"', toolbar)
        self.assertIn('class="text-muted results-optimized-at print-screen-only"', toolbar)
        self.assertIn('class="text-muted packing-engine-used print-screen-only"', toolbar)
        self.assertIn('class="packing-engine-used-prefix">Engine: @engineNameParts[0]</span>', toolbar)

    def test_engine_metadata_can_wrap_a_long_value_without_orphaning_its_label(self) -> None:
        declarations = self.css_declarations(".results-toolbar .packing-engine-used")
        self.assertEqual("0 1 auto", declarations.get("flex"))
        self.assertEqual("0", declarations.get("min-width"))
        self.assertEqual("normal", declarations.get("white-space"))
        self.assertNotIn("overflow", declarations)
        self.assertNotIn("text-overflow", declarations)
        prefix = self.css_declarations(".results-toolbar .packing-engine-used-prefix")
        self.assertEqual("nowrap", prefix.get("white-space"))
        picker = self.css_declarations(".results-toolbar .packing-engine-picker")
        self.assertEqual("inline-flex", picker.get("display"))
        self.assertEqual("center", picker.get("align-items"))
        select = self.css_declarations(".results-toolbar .packing-engine-picker select")
        self.assertEqual("0", select.get("min-width"))
        self.assertEqual("auto", select.get("width"))

    def test_engine_help_keeps_native_keyboard_disclosure_and_focus_visibility(self) -> None:
        markup = JOB_EDITOR.read_text()
        help_match = re.search(r'<details class="packing-engine-help[^\"]*">(.*?)</details>', markup, re.DOTALL)
        if help_match is None:
            self.fail("Missing native engine-help disclosure")
        help_markup = help_match[1]
        self.assertIn('<summary>About this engine</summary>', help_markup)
        self.assertIn('@SelectedEngine.Description', help_markup)
        self.assertEqual(1, markup.count('@SelectedEngine.Description'))
        self.assertIn('title="@SelectedEngine?.Description"', markup)
        focus = self.css_declarations(".packing-engine-help > summary:focus-visible")
        self.assertEqual("2px solid var(--accent)", focus.get("outline"))
        self.assertEqual(".2rem", focus.get("outline-offset"))

    def test_lock_job_is_in_the_results_action_row_before_result_cards(self) -> None:
        markup = JOB_EDITOR.read_text()
        results_start = markup.index("private RenderFragment RenderResultsTab()")
        result_cards_start = markup.index("@if (packResult != null && summary != null)", results_start)
        action_row = markup[results_start:result_cards_start]

        self.assertIn('@onclick="PrintReport"', action_row)
        self.assertIn('@onclick="LockJob"', action_row)
        self.assertIn("Lock Job", action_row)

    def test_print_report_hides_job_lock_status(self) -> None:
        markup = JOB_EDITOR.read_text()
        report_css = (REPO_ROOT / "CutList.Web/wwwroot/css/report.css").read_text()

        self.assertIn('class="alert alert-warning d-flex justify-content-between align-items-center mb-3 print-screen-only"', markup)
        self.assertIn('<span class="badge bg-success print-screen-only">', markup)
        self.assertIn(".print-screen-only", report_css)
        self.assertIn("display: none !important;", report_css)

    def test_print_report_reserves_half_inch_margins_on_every_page(self) -> None:
        css = (REPO_ROOT / "CutList.Web/wwwroot/css/report.css").read_text()
        css = re.sub(r"/\*.*?\*/", "", css, flags=re.DOTALL)
        screen, printed = css.split("@media print {", 1)
        self.assertNotIn("@page", screen)
        # An unqualified @page rule covers continuation pages, not just :first.
        pages = re.findall(r"@page\s*\{([^}]*)\}", printed)
        self.assertEqual(1, len(pages), "Missing physical page margins")
        self.assertRegex(pages[0], r"(?:^|;)\s*margin:\s*0\.5in\s*;")
        self.assertNotRegex(pages[0], r"\bsize\s*:", "Respect the user's paper size")

    def test_print_summary_cancels_bootstrap_negative_row_margins(self) -> None:
        css = (REPO_ROOT / "CutList.Web/wwwroot/css/report.css").read_text()
        printed = re.sub(r"/\*.*?\*/", "", css.split("@media print {", 1)[1], flags=re.DOTALL)
        summary = re.search(r"\.print-summary\s*\{([^}]*)\}", printed)
        if summary is None:
            self.fail("Missing print summary rule")
        # Bootstrap .row gutters must not draw borders into the physical margins.
        self.assertRegex(summary[1], r"(?:^|;)\s*margin:\s*0 0 0\.5rem !important\s*;")

    def test_print_report_forces_backgrounds_to_plain_white(self) -> None:
        report_css = (REPO_ROOT / "CutList.Web/wwwroot/css/report.css").read_text()
        print_styles = report_css[report_css.index("@media print {"):]

        self.assertIn("*::before", print_styles)
        self.assertIn("background: transparent !important;", print_styles)
        self.assertIn("box-shadow: none !important;", print_styles)

    def test_print_report_shows_the_selected_cutting_method_and_kerf(self) -> None:
        markup = JOB_EDITOR.read_text()
        results_start = markup.index("private RenderFragment RenderResultsTab()")
        summary_start = markup.index('<div class="row mb-4 print-summary">', results_start)
        material_list_start = markup.index('<!-- Material List:', summary_start)
        report_summary = markup[summary_start:material_list_start]

        self.assertIn('class="print-cut-method"', report_summary)
        self.assertIn("<strong>Cut Method:</strong>", report_summary)
        self.assertIn("@job.CuttingTool.Name", report_summary)
        self.assertIn("Kerf:", report_summary)

    def test_print_metadata_and_notes_are_hidden_on_screen_and_can_paginate(self) -> None:
        css = (REPO_ROOT / "CutList.Web/wwwroot/css/report.css").read_text()
        screen, printed = css.split("@media print {", 1)
        for selector in (".print-job-metadata", ".print-job-notes"):
            self.assertRegex(screen, re.escape(selector) + r"[^{}]*\{\s*display: none;")
            self.assertRegex(printed, re.escape(selector) + r"\s*\{[^}]*display: block !important;")
        for selector in (".print-material-list", ".print-job-notes"):
            self.assertRegex(printed, re.escape(selector) + r"\s*\{[^}]*break-inside: auto;")
            self.assertNotRegex(printed, re.escape(selector) + r"\s*\{[^}]*break-inside: avoid;")
        self.assertRegex(css, r"\.notes-section p\s*\{[^}]*white-space: pre-wrap;")
        self.assertRegex(printed, r"\.print-job-notes p\s*\{[^}]*overflow-wrap: anywhere;")

    def test_print_material_summary_rows_stay_intact_while_the_list_can_paginate(self) -> None:
        css = (REPO_ROOT / "CutList.Web/wwwroot/css/report.css").read_text()
        printed = re.sub(r"/\*.*?\*/", "", css.split("@media print {", 1)[1], flags=re.DOTALL)
        print_rules: dict[str, dict[str, str]] = {}
        for selectors, block in re.findall(r"([^{}]+)\{([^{}]*)\}", printed):
            declarations = dict(
                (name.strip(), value.strip())
                for name, value in (declaration.split(":", 1) for declaration in block.split(";") if ":" in declaration)
            )
            # Match whole selectors, including comma-separated groups, not substrings.
            for selector in selectors.split(","):
                print_rules.setdefault(selector.strip(), {}).update(declarations)

        for selector, expected in (
            (".print-material-list", "auto"),
            (".print-material-list tbody tr", "avoid"),
            (".cutlist-material-card tbody tr", "avoid"),
        ):
            with self.subTest(selector=selector):
                self.assertIn(selector, print_rules, f"Missing print style for {selector}")
                self.assertEqual(expected, print_rules[selector].get("break-inside"))
                self.assertEqual(expected, print_rules[selector].get("page-break-inside"))

    def test_browser_refreshes_every_timestamp_on_native_and_repeated_cancelled_prints(self) -> None:
        app = (REPO_ROOT / "CutList.Web/Components/App.razor").read_text()
        script = re.search(r"<script>(.*?)</script>", app, re.DOTALL)
        if script is None:
            self.fail("Missing App print script")
        # Execute the actual App script; no browser, SQL connection, or synthesized implementation.
        harness = r"""
const assert = require('node:assert/strict');
const vm = require('node:vm');
const script = require('node:fs').readFileSync(0, 'utf8');
const listeners = new Map();
const dispatch = event => { for (const fn of [...(listeners.get(event) || [])]) fn(); };
const count = event => (listeners.get(event) || new Set()).size;
const element = () => ({textContent: '', dateTime: '', set innerHTML(value) { throw Error('Unsafe HTML write'); }});
let targets = [element(), element()];
let queries = 0;
const document = { title: 'Job page', querySelectorAll(selector) {
    assert.equal(selector, 'time[data-print-timestamp]'); queries++; return targets;
}};
const window = {
    addEventListener(event, fn) { if (!listeners.has(event)) listeners.set(event, new Set()); listeners.get(event).add(fn); },
    removeEventListener(event, fn) { listeners.get(event)?.delete(fn); },
    print() { dispatch('beforeprint'); }
};
let dates = 0;
const instants = ['2026-10-03T12:00:00Z', '2026-10-03T12:02:00Z', '2026-10-04T13:05:00Z'];
class PrintDate extends Date { constructor() { super(instants[dates++]); } }
const context = {window, document, Date: PrintDate};
vm.createContext(context);
vm.runInContext(script, context);
assert.equal(queries, 0, 'Must not stamp at page load');
assert.equal(dates, 0);
assert.equal(count('beforeprint'), 1);
assert.equal(count('afterprint'), 0);
for (let attempt = 0; attempt < 2; attempt++) {
    context.printWithTitle('Printable job');
    assert.equal(document.title, 'Printable job');
    assert.equal(count('beforeprint'), 1, 'Do not accumulate print listeners');
    assert.equal(count('afterprint'), 1);
    for (const target of targets) {
        assert.equal(target.dateTime, new Date(instants[attempt]).toISOString());
        assert.match(target.textContent, /Oct 3, 2026/);
        assert.match(target.textContent, attempt === 0 ? /8:00:00 AM/ : /8:02:00 AM/);
        assert.match(target.textContent, /EDT|GMT-4/);
    }
    dispatch('afterprint'); // Closing/cancelling the dialog restores the page title too.
    assert.equal(document.title, 'Job page');
    assert.equal(count('afterprint'), 0);
}
targets = [element(), element(), element()]; // Query current DOM after navigating/rerendering.
dispatch('beforeprint'); // Native Ctrl+P does not call printWithTitle.
assert.equal(document.title, 'Job page');
assert.equal(count('beforeprint'), 1);
assert.equal(count('afterprint'), 0);
assert.equal(queries, 3);
assert.equal(dates, 3, 'One current instant per print attempt');
for (const target of targets) {
    assert.equal(target.dateTime, '2026-10-04T13:05:00.000Z');
    assert.match(target.textContent, /Oct 4, 2026/);
    assert.match(target.textContent, /9:05:00 AM/);
    assert.match(target.textContent, /EDT|GMT-4/);
}
"""
        result = subprocess.run(["node", "-e", harness], input=script[1], capture_output=True,
                                text=True, env={**os.environ, "TZ": "America/New_York", "LANG": "en_US.UTF-8"})
        self.assertEqual(0, result.returncode, result.stderr)


if __name__ == "__main__":
    unittest.main()
