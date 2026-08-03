"""Focused regression checks for job-level actions on the Results tab."""

from __future__ import annotations

import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
JOB_EDITOR = REPO_ROOT / "CutList.Web/Components/Pages/Jobs/Edit.razor"
MAIN_LAYOUT = REPO_ROOT / "CutList.Web/Components/Layout/MainLayout.razor"


class JobResultsActionsTests(unittest.TestCase):
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

    def test_print_report_hides_workspace_header_and_ready_status(self) -> None:
        markup = MAIN_LAYOUT.read_text()

        self.assertIn('class="workspace-header print-screen-only"', markup)
        self.assertIn("Cut planning workspace", markup)
        self.assertIn("Ready to plan", markup)

    def test_print_report_forces_backgrounds_to_plain_white(self) -> None:
        report_css = (REPO_ROOT / "CutList.Web/wwwroot/css/report.css").read_text()
        print_styles = report_css[report_css.index("@media print {"):]

        self.assertIn("*::before", print_styles)
        self.assertIn("background: transparent !important;", print_styles)
        self.assertIn("box-shadow: none !important;", print_styles)


if __name__ == "__main__":
    unittest.main()
