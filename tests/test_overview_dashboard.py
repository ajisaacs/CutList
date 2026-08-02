"""Focused regression checks for the data-backed CutList overview."""

from __future__ import annotations

import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
HOME_PAGE = REPO_ROOT / "CutList.Web/Components/Pages/Home.razor"
OVERVIEW_SERVICE = REPO_ROOT / "CutList.Web/Services/OverviewService.cs"
PROGRAM = REPO_ROOT / "CutList.Web/Program.cs"
APP_CSS = REPO_ROOT / "CutList.Web/wwwroot/css/app.css"


class OverviewDashboardTests(unittest.TestCase):
    def test_overview_uses_a_dedicated_data_service(self) -> None:
        self.assertTrue(OVERVIEW_SERVICE.exists(), "Expected a data-backed overview service")
        source = OVERVIEW_SERVICE.read_text()
        self.assertIn("GetSnapshotAsync", source)
        self.assertIn("context.Jobs", source)
        self.assertIn("context.JobStocks", source)
        self.assertIn("OrderByDescending(j => j.CreatedAt)", source)
        self.assertIn("GroupBy(s => new { s.MaterialId, s.LengthInches, s.Material.Shape, s.Material.Size })", source)

    def test_overview_renders_recent_jobs_and_frequently_specified_stock(self) -> None:
        markup = HOME_PAGE.read_text()
        self.assertIn("@inject OverviewService OverviewService", markup)
        self.assertIn("Recently created jobs", markup)
        self.assertIn("Frequently specified stock", markup)
        self.assertIn("Based on stock configured on jobs", markup)
        self.assertIn("overview.RecentJobs", markup)
        self.assertIn("overview.FrequentStock", markup)

    def test_overview_groups_stock_by_scalar_material_fields(self) -> None:
        source = OVERVIEW_SERVICE.read_text()
        self.assertIn(
            "GroupBy(s => new { s.MaterialId, s.LengthInches, s.Material.Shape, s.Material.Size })",
            source,
        )
        self.assertNotIn("Material = g.Select(s => s.Material).First()", source)

    def test_overview_service_is_registered_and_has_dashboard_layout_rules(self) -> None:
        self.assertIn("AddScoped<OverviewService>()", PROGRAM.read_text())
        css = APP_CSS.read_text()
        self.assertIn(".overview-grid", css)
        self.assertIn(".overview-stock-list", css)
        self.assertIn(".overview-recent-jobs", css)

    def test_overview_shows_an_explicit_unavailable_state_if_data_cannot_load(self) -> None:
        markup = HOME_PAGE.read_text()
        self.assertIn("loadError", markup)
        self.assertIn("Overview data is unavailable", markup)
        self.assertIn("catch (Exception ex)", markup)


if __name__ == "__main__":
    unittest.main()
