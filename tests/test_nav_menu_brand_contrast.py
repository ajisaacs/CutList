"""Regression checks for the CutList navigation brand on its light header."""

from __future__ import annotations

import re
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
NAV_MENU = REPO_ROOT / "CutList.Web/Components/Layout/NavMenu.razor"
NAV_MENU_CSS = REPO_ROOT / "CutList.Web/Components/Layout/NavMenu.razor.css"
APP_CSS = REPO_ROOT / "CutList.Web/wwwroot/css/app.css"
APP_SHELL = REPO_ROOT / "CutList.Web/Components/App.razor"


def selector_color(css: str, selector: str) -> str:
    match = re.search(rf"{re.escape(selector)}\s*\{{([^}}]*)\}}", css)
    if match is None:
        raise AssertionError(f"Missing CSS selector: {selector}")

    color = re.search(r"\bcolor:\s*(#[0-9a-fA-F]{6})", match.group(1))
    if color is None:
        raise AssertionError(f"Missing hex color for selector: {selector}")
    return color.group(1)


def contrast_ratio(foreground: str, background: str) -> float:
    def luminance(color: str) -> float:
        channels = [int(color[index:index + 2], 16) / 255 for index in (1, 3, 5)]
        linear = [value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4 for value in channels]
        return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2]

    lighter, darker = sorted((luminance(foreground), luminance(background)), reverse=True)
    return (lighter + 0.05) / (darker + 0.05)


class NavMenuBrandContrastTests(unittest.TestCase):
    def test_list_word_has_accessible_contrast_against_light_top_row(self) -> None:
        list_color = selector_color(NAV_MENU_CSS.read_text(), ".navbar-brand .brand-list")
        top_row_color = selector_color(APP_CSS.read_text(), ".top-row")

        self.assertGreaterEqual(contrast_ratio(list_color, top_row_color), 4.5)

    def test_brand_keeps_cut_and_list_as_adjacent_words(self) -> None:
        markup = NAV_MENU.read_text()
        self.assertIn('<span class="brand-cut">Cut</span><span class="brand-list">List</span>', markup)

    def test_visual_system_uses_the_industrial_blue_accent(self) -> None:
        css = APP_CSS.read_text()
        self.assertIn("--accent: #2b75a8;", css)
        self.assertIn("--navy: #203747;", css)
        self.assertNotIn("--orange:", css)

    def test_desktop_content_padding_does_not_push_the_nav_brand_right(self) -> None:
        css = APP_CSS.read_text()
        self.assertNotIn(".top-row, article {", css)

    def test_nav_brand_is_left_aligned_with_the_navigation_items(self) -> None:
        css = NAV_MENU_CSS.read_text()
        self.assertIn(".top-row { justify-content: flex-start;", css)

    def test_typography_uses_a_sans_serif_face_and_compact_heading_scale(self) -> None:
        shell = APP_SHELL.read_text()
        css = APP_CSS.read_text()
        self.assertIn("family=IBM+Plex+Sans", shell)
        self.assertIn('font-family: "IBM Plex Sans",', css)
        self.assertIn("h1 { font-size: clamp(1.85rem, 3.2vw, 3.15rem);", css)
        self.assertIn("h2 { font-size: clamp(1.35rem, 2vw, 1.85rem);", css)
        self.assertIn(".overview-panel-heading h2 { font-size: 1.25rem;", css)
        self.assertIn('font-family: "IBM Plex Sans",', NAV_MENU_CSS.read_text())


if __name__ == "__main__":
    unittest.main()
