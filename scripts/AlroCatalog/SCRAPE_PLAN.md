# Alro Steel SmartGrid Scraper — Remaining Steps

## Status: Script is READY TO RUN

The scraper at `scripts/AlroCatalog/scrape_alro.py` is complete and tested. Discovery mode confirmed it works correctly against the live site.

## What's Done
1. Script written with correct ASP.NET control IDs (discovered via `--discover` mode)
2. Level 1 (main grid) navigation: working
3. Level 2 (popup grid) navigation: working
4. Level 3 (dims panel) scraping: working — uses cascading dropdowns `ddlDimA` → `ddlDimB` → `ddlDimC` → `ddlLength`
5. Grade filter: 11 common grades (A-36, 1018, 1045, 1144, 12L14, etc.)
6. Size string normalization: "1-1/2\"" matches O'Neal format
7. Progress save/resume: working
8. Discovery mode verified: A-36 Round bars → 27 sizes, 80 items (lengths include "20 FT", "Custom Cut List", "Drop/Remnant" — non-stock entries filtered out in catalog builder)

## Remaining Steps

### Step 1: Run the full scrape
```bash
cd C:\Users\aisaacs\Desktop\Projects\CutList
python scripts/AlroCatalog/scrape_alro.py
```
- This scrapes all 3 categories (Bars, Pipe/Tube, Structural) for 11 filtered grades
- Takes ~30-60 minutes (cascading dropdown selections with 1.5s delay each)
- Progress saved incrementally to `scripts/AlroCatalog/alro-scrape-progress.json`
- If interrupted, resume with `python scripts/AlroCatalog/scrape_alro.py --resume`
- To scrape ALL grades: `python scripts/AlroCatalog/scrape_alro.py --all-grades`

### Step 2: Review output
- Output: `CutList.Web/Data/SeedData/alro-catalog.json`
- Verify material counts, shapes, sizes
- Spot-check dimensions against myalro.com
- Compare shape coverage to O'Neal catalog

### Step 3: Post-scrape adjustments (if needed)

**Dimension mapping for Structural/Pipe shapes**: The `build_size_and_dims()` function handles all shapes but Structural (Angle, Channel, Beam) and Pipe/Tube shapes haven't been tested live yet. After scraping, check the screenshots in `scripts/AlroCatalog/screenshots/` to verify dimension mapping. The first item of each new shape gets a screenshot + HTML dump.

**Known dimension mapping assumptions:**
- Angle: DimA = leg size, DimB = thickness → `"leg1 x leg2 x thickness"` (assumes equal legs)
- Channel: DimA = height, DimB = flange → needs verification
- IBeam: DimA = depth, DimB = weight/ft → `"W{depth} x {wt}"`
- SquareTube: DimA = size, DimB = wall
- RectTube: DimA = width, DimB = height, DimC = wall
- RoundTube: DimA = OD, DimB = wall
- Pipe: DimA = NPS, DimB = schedule

**If dimension mapping is wrong for a shape**: Edit the `build_size_and_dims()` function in `scrape_alro.py` and re-run just the catalog builder:
```python
python -c "
import json
from scripts.AlroCatalog.scrape_alro import build_catalog
data = json.load(open('scripts/AlroCatalog/alro-scrape-progress.json'))
catalog = build_catalog(data['items'])
json.dump(catalog, open('CutList.Web/Data/SeedData/alro-catalog.json', 'w'), indent=2)
"
```

### Step 4: Part numbers (optional future enhancement)
The current scraper captures sizes and lengths but NOT part numbers. To get part numbers, the script would need to:
1. Select DimA + DimB + Length
2. Click the "Next >" button (`btnSearch`)
3. Capture part number from the results panel
4. Click Back

This adds significant time per item. The catalog works without part numbers — the supplierOfferings have empty partNumber/supplierDescription fields.

## Key Files
| File | Purpose |
|------|---------|
| `scripts/AlroCatalog/scrape_alro.py` | The scraper script |
| `scripts/AlroCatalog/alro-scrape-progress.json` | Incremental progress (resume support) |
| `scripts/AlroCatalog/screenshots/` | Discovery HTML/screenshots per shape |
| `CutList.Web/Data/SeedData/alro-catalog.json` | Final output (same schema as oneals-catalog.json) |
| `CutList.Web/Data/SeedData/oneals-catalog.json` | Reference format |

## Grade Filter (editable in script)
Located at line ~50 in `scrape_alro.py`. Current filter:
- A-36, 1018 CF, 1018 HR, 1044 HR, 1045 CF, 1045 HR, 1045 TG&P
- 1144 CF, 1144 HR, 12L14 CF, A311/Stressproof

To add/remove grades, edit the `GRADE_FILTER` set in the script.
