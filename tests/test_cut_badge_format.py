"""Focused regression checks for split part-number and length cut badges."""

from __future__ import annotations

import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
JOB_EDITOR = REPO_ROOT / "CutList.Web/Components/Pages/Jobs/Edit.razor"
APP_CSS = REPO_ROOT / "CutList.Web/wwwroot/css/app.css"


class CutBadgeFormatTests(unittest.TestCase):
    def test_named_cut_badge_renders_part_number_and_length_in_separate_sections(self) -> None:
        markup = JOB_EDITOR.read_text()
        results_start = markup.index("private RenderFragment RenderResultsTab()")
        cuts_cell_start = markup.index("@foreach (var item in entry.Bin.Items)", results_start)
        cut_rows_start = markup.rfind("<td>", results_start, cuts_cell_start)
        cut_rows = markup[cut_rows_start:markup.index("</td>", cuts_cell_start)]

        self.assertIn('class="badge me-1 mb-1 fs-6 cut-part-badge"', cut_rows)
        self.assertIn('class="cut-part-badge-name"', cut_rows)
        self.assertIn('class="cut-part-badge-length"', cut_rows)
        self.assertIn('@item.Name', cut_rows)
        self.assertIn('@ArchUnits.FormatFromInches(item.Length)', cut_rows)
        self.assertNotIn('$"{item.Name} (', cut_rows)

    def test_length_section_has_a_darker_background_than_the_part_number_section(self) -> None:
        css = APP_CSS.read_text()

        self.assertIn('.cut-part-badge {', css)
        self.assertIn('.cut-part-badge-name {', css)
        self.assertIn('.cut-part-badge-length {', css)
        self.assertIn('background-color: #4c6680;', css)


if __name__ == "__main__":
    unittest.main()
