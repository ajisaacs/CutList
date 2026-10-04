"""Focused regression checks for job-level actions on the Results tab."""

from __future__ import annotations

import re
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
        self.assertIn('class="text-muted results-optimized-at"', toolbar)
        self.assertIn('class="text-muted packing-engine-used"', toolbar)
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


if __name__ == "__main__":
    unittest.main()
