#!/usr/bin/env python3
"""
Alro Steel SmartGrid Scraper
Scrapes myalro.com's SmartGrid for Carbon Steel materials and outputs
a catalog JSON matching the O'Neal catalog format.

Usage:
    python scrape_alro.py              # Scrape filtered grades (resumes from saved progress)
    python scrape_alro.py --all-grades # Scrape ALL grades (slow)
    python scrape_alro.py --discover   # Scrape first item only, dump HTML/screenshots
    python scrape_alro.py --fresh      # Start fresh, ignoring saved progress
"""

import asyncio
import json
import re
import sys
import logging
from datetime import datetime, timezone
from pathlib import Path
from playwright.async_api import async_playwright, Page, TimeoutError as PwTimeout
from playwright_stealth import Stealth

# ── Logging ──────────────────────────────────────────────────────────
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%H:%M:%S",
)
log = logging.getLogger(__name__)

# ── Paths ────────────────────────────────────────────────────────────
SCRIPT_DIR = Path(__file__).parent.resolve()
OUTPUT_PATH = (SCRIPT_DIR / "../../CutList.Web/Data/SeedData/alro-catalog.json").resolve()
PROGRESS_PATH = SCRIPT_DIR / "alro-scrape-progress.json"
SCREENSHOTS_DIR = SCRIPT_DIR / "screenshots"

# ── Config ───────────────────────────────────────────────────────────
BASE_URL = "https://www.myalro.com/SmartGrid.aspx?PT=Steel&Clear=true"
DELAY = 5             # seconds between postback clicks
TIMEOUT = 15_000      # ms for element waits
CS_ROW = 4            # Carbon Steel row index in main grid

CATEGORIES = ["Bars", "Pipe / Tube", "Structural"]

# ┌─────────────────────────────────────────────────────────────────┐
# │  GRADE FILTER — only these grades will be scraped.             │
# │  Use --all-grades flag to override and scrape everything.      │
# │  Grade names must match the gpname attribute exactly.          │
# └─────────────────────────────────────────────────────────────────┘
GRADE_FILTER = {
    # Common structural / general purpose
    "A-36",
    # Mild steel
    "1018 CF",
    "1018 HR",
    # Medium carbon (shafts, gears, pins)
    "1045 CF",
    "1045 HR",
    "1045 TG&P",
    # Free-machining
    "1144 CF",
    "1144 HR",
    "12L14 CF",
    # Hot-rolled plate/bar
    "1044 HR",
    # Stressproof (high-strength shafting)
    "A311/Stressproof",
}

# Alro shape column header → our MaterialShape enum
SHAPE_MAP = {
    "ROUND":            "RoundBar",
    "FLAT":             "FlatBar",
    "SQUARE":           "SquareBar",
    "ANGLE":            "Angle",
    "CHANNEL":          "Channel",
    "BEAM":             "IBeam",
    "SQ TUBE":          "SquareTube",
    "SQUARE TUBE":      "SquareTube",
    "REC TUBE":         "RectangularTube",
    "RECT TUBE":        "RectangularTube",
    "RECTANGULAR TUBE": "RectangularTube",
    "ROUND TUBE":       "RoundTube",
    "RND TUBE":         "RoundTube",
    "PIPE":             "Pipe",
}

# ── ASP.NET control IDs ─────────────────────────────────────────
_CP = "ctl00_ContentPlaceHolder1"
_PU = f"{_CP}_pnlPopUP"
ID = dict(
    main_grid    = f"{_CP}_grdMain",
    popup_grid   = f"{_PU}_grdPopUp",
    popup_window = f"{_PU}_Window",
    dims_panel   = f"{_PU}_upnlDims",
    back_btn     = f"{_PU}_btnBack",
    # Dimension dropdowns (cascading: A → B → C → Length)
    dim_a        = f"{_PU}_ddlDimA",
    dim_b        = f"{_PU}_ddlDimB",
    dim_c        = f"{_PU}_ddlDimC",
    dim_length   = f"{_PU}_ddlLength",
    btn_next     = f"{_PU}_btnSearch",
)

# Postback targets ($ separators)
PB = dict(
    main_grid  = "ctl00$ContentPlaceHolder1$grdMain",
    popup_grid = "ctl00$ContentPlaceHolder1$pnlPopUP$grdPopUp",
    back_btn   = "ctl00$ContentPlaceHolder1$pnlPopUP$btnBack",
    popup      = "ctl00$ContentPlaceHolder1$pnlPopUP",
    dim_a      = "ctl00$ContentPlaceHolder1$pnlPopUP$ddlDimA",
    dim_b      = "ctl00$ContentPlaceHolder1$pnlPopUP$ddlDimB",
    dim_c      = "ctl00$ContentPlaceHolder1$pnlPopUP$ddlDimC",
)


# ═══════════════════════════════════════════════════════════════════════
#  Utility helpers
# ═══════════════════════════════════════════════════════════════════════

def parse_fraction(s: str) -> float | None:
    """Parse fraction/decimal string → float.  '1-1/4' → 1.25, '.250' → 0.25"""
    if not s:
        return None
    s = s.strip().strip('"\'')
    # Collapse double spaces from Alro dropdown text ("1  1/4" → "1 1/4")
    s = re.sub(r"\s+", " ", s)
    if not s:
        return None
    try:
        return float(s)
    except ValueError:
        pass
    # Mixed fraction: "1-1/4" or "1 1/4"
    m = re.match(r"^(\d+)[\s-](\d+)/(\d+)$", s)
    if m:
        return int(m[1]) + int(m[2]) / int(m[3])
    m = re.match(r"^(\d+)/(\d+)$", s)
    if m:
        return int(m[1]) / int(m[2])
    m = re.match(r"^(\d+)$", s)
    if m:
        return float(m[1])
    return None


def decimal_to_fraction(value: float) -> str:
    """0.25 → '1/4',  1.25 → '1-1/4',  3.0 → '3'"""
    if value <= 0:
        return "0"
    whole = int(value)
    frac = value - whole
    if abs(frac) < 0.001:
        return str(whole)
    from math import gcd
    sixteenths = round(frac * 16)
    if sixteenths == 16:
        return str(whole + 1)
    g = gcd(sixteenths, 16)
    num, den = sixteenths // g, 16 // g
    frac_s = f"{num}/{den}"
    return f"{whole}-{frac_s}" if whole else frac_s


def normalize_dim_text(s: str) -> str:
    """Normalize dimension text: '1  1/4' → '1-1/4', '3/16' → '3/16'"""
    s = re.sub(r"\s+", " ", s.strip())
    # "1 1/4" → "1-1/4" (mixed fraction with space → hyphen)
    s = re.sub(r"^(\d+)\s+(\d+/\d+)$", r"\1-\2", s)
    return s


def parse_length_to_inches(text: str) -> float | None:
    """Parse length string to inches.  \"20'\" → 240, \"240\" → 240"""
    s = text.strip().upper()
    s = re.sub(r"\s*(RL|RANDOM.*|LENGTHS?|EA|EACH|STOCK)\s*", "", s).strip()
    m = re.match(r"^(\d+(?:\.\d+)?)\s*['\u2032]", s)
    if m:
        return float(m[1]) * 12
    m = re.match(r"^(\d+(?:\.\d+)?)\s*FT", s)
    if m:
        return float(m[1]) * 12
    m = re.match(r'^(\d+(?:\.\d+)?)\s*"?\s*$', s)
    if m:
        v = float(m[1])
        return v * 12 if v <= 30 else v
    return None


# ═══════════════════════════════════════════════════════════════════════
#  SmartGrid navigation
# ═══════════════════════════════════════════════════════════════════════

async def wait_for_update(page: Page, timeout: int = TIMEOUT):
    """Wait for ASP.NET partial postback to finish."""
    try:
        await page.wait_for_load_state("networkidle", timeout=timeout)
    except PwTimeout:
        log.warning("  networkidle timeout – continuing")
    await asyncio.sleep(0.5)


async def do_postback(page: Page, target: str, arg: str):
    """Execute a __doPostBack call."""
    await page.evaluate(f"__doPostBack('{target}', '{arg}')")


async def click_category(page: Page, category: str) -> bool:
    """Click a category blue-button for Carbon Steel in the main grid."""
    log.info(f"Clicking main grid: {category} (row {CS_ROW})")
    arg = f"{category}${CS_ROW}"
    link = await page.query_selector(
        f"#{ID['main_grid']} a[href*=\"'{arg}'\"] img[src*='blue_button']"
    )
    if not link:
        log.error(f"  Button not found for {arg}")
        return False

    parent = await link.evaluate_handle("el => el.parentElement")
    await parent.as_element().click()

    try:
        await page.wait_for_selector(f"#{ID['popup_grid']}", state="visible", timeout=TIMEOUT)
        await wait_for_update(page)
        return True
    except PwTimeout:
        log.error(f"  Popup did not appear for {category}")
        return False


async def scrape_popup_grid(page: Page):
    """Parse the popup grid → [(grade_name, grade_id, shape, row_idx, has_btn)]."""
    headers = await page.eval_on_selector_all(
        f"#{ID['popup_grid']} tr.DataHeader th",
        "els => els.map(el => el.textContent.trim())",
    )
    log.info(f"  Popup columns: {headers}")

    rows = await page.query_selector_all(
        f"#{ID['popup_grid']} tr.griditemP, #{ID['popup_grid']} tr.gridaltItemP"
    )
    combos = []
    for row_idx, row in enumerate(rows):
        first_td = await row.query_selector("td[gpid]")
        if not first_td:
            continue
        gid = (await first_td.get_attribute("gpid") or "").strip()
        gname = (await first_td.get_attribute("gpname") or "").strip()
        tds = await row.query_selector_all("td")
        for col_idx, td in enumerate(tds):
            if col_idx == 0:
                continue
            shape = headers[col_idx] if col_idx < len(headers) else ""
            img = await td.query_selector("img[src*='blue_button']")
            combos.append((gname, gid, shape, row_idx, img is not None))

    active = sum(1 for c in combos if c[4])
    log.info(f"  {active} active grade/shape combos")
    return combos


async def click_shape(page: Page, shape: str, row_idx: int) -> bool:
    """Click a shape button in the popup grid; wait for dims panel."""
    arg = f"{shape}${row_idx}"
    link = await page.query_selector(
        f"#{ID['popup_grid']} a[href*=\"'{arg}'\"] img[src*='blue_button']"
    )
    if not link:
        try:
            await do_postback(page, PB["popup_grid"], arg)
        except Exception:
            log.warning(f"  Could not click shape {arg}")
            return False
    else:
        parent = await link.evaluate_handle("el => el.parentElement")
        await parent.as_element().click()

    try:
        # Wait for the DimA dropdown to appear (the real indicator of dims panel loaded)
        await page.wait_for_selector(f"#{ID['dim_a']}", state="attached", timeout=TIMEOUT)
        await wait_for_update(page)
        return True
    except PwTimeout:
        # Check if panel has any content at all
        html = await page.inner_html(f"#{ID['dims_panel']}")
        if len(html.strip()) > 50:
            await wait_for_update(page)
            return True
        log.warning(f"  Dims panel timeout for {arg}")
        return False


async def click_back(page: Page):
    """Click Back to return to the popup grid view."""
    try:
        await do_postback(page, PB["back_btn"], "")
        await wait_for_update(page)
        await asyncio.sleep(DELAY)
    except Exception as e:
        log.warning(f"  Back button error: {e}")


async def close_popup(page: Page):
    """Close the popup window and return to the main grid."""
    try:
        await do_postback(page, PB["popup"], "Close")
        await wait_for_update(page)
        await asyncio.sleep(DELAY)
    except Exception as e:
        log.warning(f"  Close popup error: {e}")


# ═══════════════════════════════════════════════════════════════════════
#  Level 3 — Dimension Panel Scraping
# ═══════════════════════════════════════════════════════════════════════

async def get_select_options(page: Page, sel_id: str):
    """Return [(value, text), ...] for a <select>, excluding placeholders."""
    el = await page.query_selector(f"#{sel_id}")
    if not el:
        return []
    # Check if disabled
    disabled = await el.get_attribute("disabled")
    if disabled:
        return []
    try:
        opts = await page.eval_on_selector(
            f"#{sel_id}",
            """el => Array.from(el.options).map(o => ({
                v: o.value, t: o.text.trim(), d: o.disabled
            }))""",
        )
    except Exception:
        return []
    return [
        (o["v"], o["t"])
        for o in opts
        if o["v"] and o["v"] != "-1" and o["t"] and not o["d"]
        and o["t"].lower() not in ("- select -", "--select--", "select...", "select", "")
    ]


async def scrape_dims_panel(page: Page, grade: str, shape_alro: str,
                             shape_mapped: str, *, save_discovery: bool = False,
                             on_item=None, scraped_dim_a: set[str] | None = None):
    """Main Level 3 extraction. Returns list of raw item dicts.

    If on_item callback is provided, it is called with each item dict
    as soon as it is discovered (for incremental saving).
    If scraped_dim_a is provided, DimA values in that set are skipped (resume).
    """
    items: list[dict] = []

    if save_discovery:
        SCREENSHOTS_DIR.mkdir(exist_ok=True)
        safe = f"{grade}_{shape_alro}".replace(" ", "_").replace("/", "-")
        await page.screenshot(path=str(SCREENSHOTS_DIR / f"dims_{safe}.png"), full_page=True)
        html = await page.inner_html(f"#{ID['dims_panel']}")
        (SCREENSHOTS_DIR / f"dims_{safe}.html").write_text(html, encoding="utf-8")
        log.info(f"  Discovery saved → screenshots/dims_{safe}.*")

    # ── Get DimA options (primary dimension: diameter, width, size, etc.) ──
    dim_a_opts = await get_select_options(page, ID["dim_a"])
    if not dim_a_opts:
        log.warning(f"    No DimA options found")
        try:
            html = await page.inner_html(f"#{ID['dims_panel']}")
            if len(html) > 50:
                SCREENSHOTS_DIR.mkdir(exist_ok=True)
                safe = f"{grade}_{shape_alro}_nodimopts".replace(" ", "_").replace("/", "-")
                (SCREENSHOTS_DIR / f"{safe}.html").write_text(html, encoding="utf-8")
        except Exception as e:
            log.warning(f"    Could not dump dims panel: {e}")
        return []

    already_done = scraped_dim_a or set()
    remaining = [(v, t) for v, t in dim_a_opts if v not in already_done]
    if already_done:
        log.info(f"    DimA: {len(dim_a_opts)} sizes ({len(dim_a_opts) - len(remaining)} already scraped, {len(remaining)} remaining)")
    else:
        log.info(f"    DimA: {len(dim_a_opts)} sizes")

    # All DimA values already scraped — combo is complete
    if not remaining:
        return []

    for a_val, a_text in remaining:
        # Select DimA → triggers postback → DimB/Length populate
        await page.select_option(f"#{ID['dim_a']}", a_val)
        await asyncio.sleep(DELAY)
        await wait_for_update(page)

        # Check if DimB appeared (secondary dimension: thickness, wall, etc.)
        dim_b_opts = await get_select_options(page, ID["dim_b"])
        if dim_b_opts:
            for b_val, b_text in dim_b_opts:
                await page.select_option(f"#{ID['dim_b']}", b_val)
                await asyncio.sleep(DELAY)
                await wait_for_update(page)

                # Check for DimC (tertiary — rare)
                dim_c_opts = await get_select_options(page, ID["dim_c"])
                if dim_c_opts:
                    for c_val, c_text in dim_c_opts:
                        await page.select_option(f"#{ID['dim_c']}", c_val)
                        await asyncio.sleep(DELAY)
                        await wait_for_update(page)

                        lengths = await get_select_options(page, ID["dim_length"])
                        for l_val, l_text in lengths:
                            item = _make_item(
                                grade, shape_mapped,
                                a_val, a_text, b_val, b_text, c_val, c_text,
                                l_text,
                            )
                            items.append(item)
                            if on_item:
                                on_item(item)
                else:
                    # No DimC — read lengths
                    lengths = await get_select_options(page, ID["dim_length"])
                    for l_val, l_text in lengths:
                        item = _make_item(
                            grade, shape_mapped,
                            a_val, a_text, b_val, b_text, None, None,
                            l_text,
                        )
                        items.append(item)
                        if on_item:
                            on_item(item)
        else:
            # No DimB — just DimA + Length
            lengths = await get_select_options(page, ID["dim_length"])
            for l_val, l_text in lengths:
                item = _make_item(
                    grade, shape_mapped,
                    a_val, a_text, None, None, None, None,
                    l_text,
                )
                items.append(item)
                if on_item:
                    on_item(item)

    return items


def _make_item(grade, shape, a_val, a_text, b_val, b_text, c_val, c_text, l_text):
    """Build a raw item dict from dimension selections."""
    return {
        "grade": grade,
        "shape": shape,
        "dim_a_val": a_val,     # decimal string like ".500"
        "dim_a_text": a_text,   # fraction string like "1/2"
        "dim_b_val": b_val,
        "dim_b_text": b_text,
        "dim_c_val": c_val,
        "dim_c_text": c_text,
        "length_text": l_text,
        "length_inches": parse_length_to_inches(l_text),
    }


# ═══════════════════════════════════════════════════════════════════════
#  Output — build catalog JSON
# ═══════════════════════════════════════════════════════════════════════

def build_size_and_dims(shape: str, item: dict):
    """Return (size_string, dimensions_dict) for a catalog material entry.

    Uses the decimal values from dropdown option values for precision,
    and fraction text from dropdown option text for display.
    """
    # Use the numeric value from the dropdown (e.g. ".500") for precision
    a = float(item["dim_a_val"]) if item.get("dim_a_val") else None
    b = float(item["dim_b_val"]) if item.get("dim_b_val") else None
    c = float(item["dim_c_val"]) if item.get("dim_c_val") else None

    a_txt = normalize_dim_text(item.get("dim_a_text") or "")
    b_txt = normalize_dim_text(item.get("dim_b_text") or "")
    c_txt = normalize_dim_text(item.get("dim_c_text") or "")

    if shape == "RoundBar" and a is not None:
        return f'{a_txt}"', {"diameter": round(a, 4)}

    if shape == "FlatBar":
        if a is not None and b is not None:
            return (f'{a_txt}" x {b_txt}"',
                    {"width": round(a, 4), "thickness": round(b, 4)})
        if a is not None:
            return f'{a_txt}"', {"width": round(a, 4), "thickness": 0}

    if shape == "SquareBar" and a is not None:
        return f'{a_txt}"', {"sideLength": round(a, 4)}

    if shape == "Angle":
        if a is not None and b is not None:
            return (f'{a_txt}" x {a_txt}" x {b_txt}"',
                    {"leg1": round(a, 4), "leg2": round(a, 4), "thickness": round(b, 4)})
        if a is not None:
            return f'{a_txt}"', {"leg1": round(a, 4), "leg2": round(a, 4), "thickness": 0}

    if shape == "Channel":
        # Channels may use DimA for combined designation or height
        if a is not None and b is not None:
            return (f'{a_txt}" x {b_txt}"',
                    {"height": round(a, 4), "flange": round(b, 4), "web": 0})
        if a is not None:
            return a_txt, {"height": round(a, 4), "flange": 0, "web": 0}

    if shape == "IBeam":
        # DimA might be the W-designation, DimB the weight/ft
        if a is not None and b is not None:
            return (f"W{int(a)} x {b}",
                    {"height": round(a, 4), "weightPerFoot": round(b, 4)})
        if a is not None:
            return f"W{int(a)}", {"height": round(a, 4), "weightPerFoot": 0}

    if shape == "SquareTube":
        if a is not None and b is not None:
            return (f'{a_txt}" x {b_txt}" wall',
                    {"sideLength": round(a, 4), "wall": round(b, 4)})
        if a is not None:
            return f'{a_txt}"', {"sideLength": round(a, 4), "wall": 0}

    if shape == "RectangularTube":
        if a is not None and b is not None and c is not None:
            return (f'{a_txt}" x {b_txt}" x {c_txt}" wall',
                    {"width": round(a, 4), "height": round(b, 4), "wall": round(c, 4)})
        if a is not None and b is not None:
            return (f'{a_txt}" x {b_txt}"',
                    {"width": round(a, 4), "height": round(b, 4), "wall": 0})

    if shape == "RoundTube":
        if a is not None and b is not None:
            return (f'{a_txt}" OD x {b_txt}" wall',
                    {"outerDiameter": round(a, 4), "wall": round(b, 4)})
        if a is not None:
            return f'{a_txt}" OD', {"outerDiameter": round(a, 4), "wall": 0}

    if shape == "Pipe":
        sched = b_txt or c_txt or "40"
        if a is not None:
            return (f'{a_txt}" NPS Sch {sched}',
                    {"nominalSize": round(a, 4), "schedule": sched})

    # Fallback
    return a_txt or "", {}


SHAPE_GROUP_KEY = {
    "Angle":            "angles",
    "Channel":          "channels",
    "FlatBar":          "flatBars",
    "IBeam":            "iBeams",
    "Pipe":             "pipes",
    "RectangularTube":  "rectangularTubes",
    "RoundBar":         "roundBars",
    "RoundTube":        "roundTubes",
    "SquareBar":        "squareBars",
    "SquareTube":       "squareTubes",
}


def build_catalog(scraped: list[dict]) -> dict:
    """Assemble the final catalog JSON from scraped item dicts."""
    materials: dict[tuple, dict] = {}

    for item in scraped:
        shape = item.get("shape", "")
        grade = item.get("grade", "")
        if not shape or not grade:
            continue

        size_str, dims = build_size_and_dims(shape, item)
        key = (shape, grade, size_str)

        if key not in materials:
            mat = {
                "type": "Steel",
                "grade": grade,
                "size": size_str,
                "stockItems": [],
            }
            mat.update(dims)
            materials[key] = mat

        length = item.get("length_inches")
        if length and length > 0:
            existing = {si["lengthInches"] for si in materials[key]["stockItems"]}
            if round(length, 4) not in existing:
                materials[key]["stockItems"].append({
                    "lengthInches": round(length, 4),
                    "quantityOnHand": 0,
                    "supplierOfferings": [{
                        "supplierName": "Alro Steel",
                        "partNumber": "",
                        "supplierDescription": "",
                    }],
                })

    # Group by shape key
    grouped: dict[str, list] = {v: [] for v in SHAPE_GROUP_KEY.values()}
    for (shape, _, _), mat in sorted(materials.items(), key=lambda kv: (kv[0][0], kv[0][1], kv[0][2])):
        group_key = SHAPE_GROUP_KEY.get(shape)
        if group_key:
            grouped[group_key].append(mat)

    return {
        "exportedAt": datetime.now(timezone.utc).isoformat(),
        "suppliers": [{"name": "Alro Steel"}],
        "cuttingTools": [
            {"name": "Bandsaw", "kerfInches": 0.0625, "isDefault": True},
            {"name": "Chop Saw", "kerfInches": 0.125, "isDefault": False},
            {"name": "Cold Cut Saw", "kerfInches": 0.0625, "isDefault": False},
            {"name": "Hacksaw", "kerfInches": 0.0625, "isDefault": False},
        ],
        "materials": grouped,
    }


# ═══════════════════════════════════════════════════════════════════════
#  Progress management
# ═══════════════════════════════════════════════════════════════════════

def load_progress() -> dict:
    if PROGRESS_PATH.exists():
        return json.loads(PROGRESS_PATH.read_text(encoding="utf-8"))
    return {"completed": [], "items": []}


def save_progress(progress: dict):
    PROGRESS_PATH.write_text(json.dumps(progress, indent=2, ensure_ascii=False), encoding="utf-8")


# ═══════════════════════════════════════════════════════════════════════
#  Main
# ═══════════════════════════════════════════════════════════════════════

async def main():
    discover = "--discover" in sys.argv
    fresh = "--fresh" in sys.argv
    all_grades = "--all-grades" in sys.argv

    progress = {"completed": [], "items": []} if fresh else load_progress()
    all_items: list[dict] = progress.get("items", [])
    done_keys: set[tuple] = {tuple(k) for k in progress.get("completed", [])}

    # Build index of saved DimA values per (grade, shape) for partial resume
    saved_dim_a: dict[tuple[str, str], set[str]] = {}
    if all_items and not fresh:
        for item in all_items:
            key = (item.get("grade", ""), item.get("shape", ""))
            saved_dim_a.setdefault(key, set()).add(item.get("dim_a_val", ""))

    log.info("Alro Steel SmartGrid Scraper")
    if all_grades:
        log.info("  Mode: ALL grades")
    else:
        log.info(f"  Filtering to {len(GRADE_FILTER)} grades: {', '.join(sorted(GRADE_FILTER))}")
    if fresh:
        log.info("  Fresh start — ignoring saved progress")
    elif done_keys:
        log.info(f"  Resuming: {len(done_keys)} combos done, {len(all_items)} items saved")
    if discover:
        log.info("  Discovery mode — will scrape first item then stop")

    async with Stealth().use_async(async_playwright()) as pw:
        browser = await pw.chromium.launch(headless=False)
        ctx = await browser.new_context(
            viewport={"width": 1280, "height": 900},
            user_agent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            locale="en-US",
            timezone_id="America/Indiana/Indianapolis",
        )
        page = await ctx.new_page()

        log.info(f"Navigating to SmartGrid …")
        await page.goto(BASE_URL, wait_until="networkidle", timeout=30_000)
        await asyncio.sleep(2)

        if not await page.query_selector(f"#{ID['main_grid']}"):
            log.error("Main grid not found! Saving screenshot.")
            SCREENSHOTS_DIR.mkdir(exist_ok=True)
            await page.screenshot(path=str(SCREENSHOTS_DIR / "error_no_grid.png"))
            await browser.close()
            return

        log.info("Main grid loaded")
        total_scraped = 0
        first_item = True

        for category in CATEGORIES:
            log.info(f"\n{'=' * 60}")
            log.info(f" Category: {category}")
            log.info(f"{'=' * 60}")

            if not await click_category(page, category):
                continue
            await asyncio.sleep(DELAY)

            combos = await scrape_popup_grid(page)

            for grade_name, grade_id, shape_name, row_idx, has_btn in combos:
                if not has_btn:
                    continue

                # Grade filter
                if not all_grades and grade_name not in GRADE_FILTER:
                    continue

                shape_upper = shape_name.upper().strip()
                shape_mapped = SHAPE_MAP.get(shape_upper)
                if shape_mapped is None:
                    log.info(f"  Skip unmapped shape: {shape_name}")
                    continue

                combo_key = (category, grade_name, shape_name)
                if combo_key in done_keys:
                    log.info(f"  Skip (done): {grade_name} / {shape_name}")
                    continue

                log.info(f"\n  -- {grade_name} / {shape_name} -> {shape_mapped} --")

                if not await click_shape(page, shape_name, row_idx):
                    await click_back(page)
                    await asyncio.sleep(DELAY)
                    continue

                await asyncio.sleep(DELAY)

                combo_count = 0
                def on_item_discovered(item):
                    nonlocal total_scraped, combo_count
                    all_items.append(item)
                    total_scraped += 1
                    combo_count += 1
                    progress["items"] = all_items
                    save_progress(progress)

                # Pass already-scraped DimA values so partial combos resume correctly
                already = saved_dim_a.get((grade_name, shape_mapped), set())

                items = await scrape_dims_panel(
                    page, grade_name, shape_name, shape_mapped,
                    save_discovery=first_item or discover,
                    on_item=on_item_discovered,
                    scraped_dim_a=already,
                )
                first_item = False

                log.info(f"  -> {combo_count} items  (total {total_scraped})")

                done_keys.add(combo_key)
                progress["completed"] = [list(k) for k in done_keys]
                save_progress(progress)

                await click_back(page)
                await asyncio.sleep(DELAY)

                if discover:
                    log.info("\nDiscovery done. Check: scripts/AlroCatalog/screenshots/")
                    await browser.close()
                    return

            await close_popup(page)
            await asyncio.sleep(DELAY)

        await browser.close()

    # ── Build output ──
    log.info(f"\n{'=' * 60}")
    log.info(f"Building catalog from {len(all_items)} items …")
    catalog = build_catalog(all_items)

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT_PATH.write_text(json.dumps(catalog, indent=2, ensure_ascii=False), encoding="utf-8")

    log.info(f"Written: {OUTPUT_PATH}")
    total_mats = sum(len(v) for v in catalog["materials"].values())
    total_stock = sum(len(m["stockItems"]) for v in catalog["materials"].values() for m in v)
    log.info(f"Materials: {total_mats}")
    log.info(f"Stock items: {total_stock}")
    for shape_key, mats in sorted(catalog["materials"].items()):
        if mats:
            log.info(f"  {shape_key}: {len(mats)}")


if __name__ == "__main__":
    asyncio.run(main())
