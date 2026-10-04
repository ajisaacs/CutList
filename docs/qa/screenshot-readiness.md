# Screenshot-readiness acceptance

Use this recipe before publishing job-editor or printed-report screenshots. It does not
replace the automated regressions, authorize production label repair, or deploy the app.
Do not use the unused `CutListReport.razor` as an alternate report renderer: Results and
its print stylesheet are the report under test.

## Automated gate

Run from the repository root with .NET 10, Docker and Node available. Node executes the
actual inline browser-print script in the Python regression suite.

```bash
dotnet test CutList.Core.Tests/CutList.Core.Tests.csproj
dotnet test CutList.Web.Tests/CutList.Web.Tests.csproj
python3 -B -m unittest discover -s tests -p 'test_*.py'
dotnet build CutList.Web/CutList.Web.csproj
dotnet build CutList.Mcp/CutList.Mcp.csproj
dotnet build tools/CutList.MaterialRepair/CutList.MaterialRepair.csproj
git diff --check
```

The Web suite starts its own guarded, disposable SQL Server fixture. Never substitute a
development/production connection if that fixture fails. When parallel workers share a
checkout, serialize builds with `flock --close -w 180 <scratch-lock> dotnet ...`; without
`--close`, persistent MSBuild workers can inherit the lock descriptor.

Do not claim WinForms runtime verification from Linux builds. The repair CLI's artifact
handling is deliberately Linux-only; see [material-size repair](../material-size-repair.md).

## Isolated browser environment

1. Record the exact source commit. Use a detached scratch worktree when other work is in
   progress, so the running app and its asset hashes correspond to a known commit.
2. Create a disposable SQL Server container with a unique name, a generated password kept
   only in a mode-0600 scratch file, and a port published on `127.0.0.1` only. Do not reuse
   any configured app database. Verify the container, port and actual database identity.
3. Restore/build the isolated Web project, apply its existing migrations only to the new
   database using an explicit `ConnectionStrings__DefaultConnection`, and publish/run the
   app bound to `127.0.0.1`. Keep connection strings out of command output and artifacts.
   Record the exact PID, container name, database and port for targeted cleanup.
4. Wait for SQL readiness and a successful application HTTP response; merely starting a
   process is not a health check. Verify the served CSS and declared SVG icon match the
   built application, including content types and hashes.
5. Import `CutList.Web/Data/SeedData/oneals-catalog.json` with `POST /api/catalog/import`.
   Inspect `errors` and `warnings`, not only status 200, then read back materials/stock.
   The corrected bundled catalog contains 616 materials and 810 stock entries. Repeating
   that same corrected import into this disposable database must create no new rows.
   The previously documented `scripts/ExportData` project is not present.
6. Create invented jobs, customers and notes through the real API. The job pack endpoint
   is a non-persisting preview; use the UI's Optimize action when saved/reloaded results
   are required. Never invent result JSON to claim a genuine algorithm outcome.

Corrected seed import is safe here because the database is fresh. It is **not** an
existing-database migration: renamed shape/Size keys can create duplicates over old data.

## Discriminating fixtures

Keep inputs and every relevant API response in the evidence directory.

- Mixed catalog/custom job with seven used bars, including the same material/length in
  both sources; unused configured stock must not appear in Material List.
- One used bar and one piece, catalog-only, and no stock with unplaced parts.
- Fractional part/stock lengths, whole-inch/feet carry examples, and a stored `0.0598`
  tube wall. Use measurements representable by the database's `(10,4)` precision for
  the persisted wall witness; a higher-precision request tests a different API/storage
  boundary and must not be silently treated as the same case.
- Two distinct material IDs with identical display names; they remain separate. Rows
  order by material display name/ID and descending raw numeric length, not formatted text.
- Genuine `Exhaustive (First Fit fallback)` output, saved and reloaded. Retain the input
  and engine response proving the fallback; do not relabel a normal run as a fallback.
- Locked saved job with a fresh full job/parts/stock snapshot before and after read-only
  render, unit toggles and print attempts. Locking itself is a separate expected change.
- Long Material List, enough cut rows to continue one material onto another page, and
  multiline notes spanning pages with an explicit final marker. Include a notes-free job.

## Screen acceptance

Use Chromium/Playwright with viewport captures at 1400×900, a narrower desktop viewport,
and 390 pixels wide. Save per-route/tab HTML or DOM text, screenshot, computed styles,
console errors and failed resource URLs. Reset error collection per page; one earlier
failure must not be attributed to every subsequent route.

Check Parts, Stock and Results:

- No padded secondary decimals such as `144.0000`; exact secondary values are not rounded.
- Literal counts use `1 bar`, `1 piece` and `1 item`; plural cases retain plural wording.
- Catalog/From Catalog copy replaces job-Stock inventory terminology.
- Toolbar controls wrap intact, Engine stays with at least the first value word, the
  longest fallback name fits, and the native help disclosure works from the keyboard.
  Test actual bounding boxes; source selectors alone do not prove responsive behavior.
- Material List quantities equal all used cut bars, grouped by identity and exact length;
  no unused stock or duplicate source-partition counting. An empty list still preserves
  the unplaced warning. Validate sums and waste programmatically from actual results.
- The tube editor's stored wall and generated label agree after blur; blur emits no new
  rounded value. Ordinary length inputs retain their separate rounding policy.
- The exact declared favicon URL returns SVG successfully; no unexplained console or
  network errors remain. Unrelated narrow data-table redesign is outside this pass.

## Print/PDF acceptance

The print contract is Results and its Print Report button; printing a different tab is
not silently redirected to Results.

1. In a real headed Chromium session (Xvfb is suitable on Linux), use Print Report twice,
   cancel each attempt, and exercise native Ctrl+P from Results. Record fresh timestamp
   values and unchanged Last optimized, document-title restoration after cancellation,
   and the locked job's unchanged persisted snapshot. A mocked `window.print` or manual
   `beforeprint` dispatch is useful unit coverage, not proof of a native dialog.
2. Compare screen/print computed visibility. Print includes one job name/number, optional
   nonblank customer, actual saved engine, Last optimized, browser-local Printed time
   with timezone, tool/kerf, complete Material List and safely encoded multiline notes.
   Blank customer/notes blocks are omitted. `dateTime` is ISO; visible print time is local.
3. Generate real Letter PDFs with Chromium without adding automation-only margins:
   the application's print CSS must supply the half-inch `@page` margins. Also check
   a sample on A4 so the CSS does not silently force a paper size. Extract text
   (`pdftotext -layout`) and render **every** page, enumerate/count the image manifest,
   and visually inspect all pages as well as comparing expected text/rows.
4. Measure the physical inset of text **and drawing/border bounds** on every PDF page,
   including continuations: at least 36 points (0.5 inch) per side, allowing at most
   half a point of browser/PDF coordinate rounding. Reject outer rules that reach the
   page edges. Bootstrap row gutters must not bleed into the page margins; inner cell
   padding is not a substitute for physical margins. Then check lowercase dimension
   `x`, black cut-badge text/dividers, repeating material
   headers, and intact individual summary/cut rows. Whole lists and long notes must
   paginate rather than clip. A repeated table footer is the whole-report total, not a
   per-page subtotal. Check continuity against all pages before diagnosing missing rows.
5. Require every expected material-list row, cut row and final notes marker in extracted
   text. Compare totals to saved/API results, not mental arithmetic. Record any rendering
   limitation honestly instead of substituting source-only evidence.

## Evidence, cleanup and publication boundary

On Hermes, save raw material under
`/home/aj/extracted/<local-date>/cutlist-screenshot-readiness/`; do not overwrite the
original `cutlist-readme-screenshot-qa` findings. Include exact app commit/browser version,
asset responses/hashes, synthetic inputs/readbacks, per-page errors, screen/layout facts,
PDFs/text/every-page renders, automated logs and a criterion-by-criterion acceptance report.
Keep credentials and machine-private launch configuration out of evidence and Git.

After acceptance, stop only the recorded disposable application/browser/Xvfb processes,
remove only the uniquely named disposable SQL container, remove the scratch worktree and
served-purpose scripts/secret files, and verify the port/container are gone. Preserve
original evidence, unrelated work and `.hermes/` planning files. Commit this recipe and
verified outcomes, push the task branch, read back its exact remote SHA and inspect CI.
The repository's image publisher runs on relevant `master` pushes, not this task branch;
no branch CI run is not a successful publishing run. Publishing an image is not deployment.

Production label repair, application deployment and README image publication remain
separate actions. Follow the guarded repair document for named-target approval, verified
backup, reviewed manifest and post-write readback; no screenshot task authorizes them.

## Verification record

Verified on 2026-10-03 with Chromium **154.0.8037.97**. Browser/PDF capture used isolated
app commit `ca26db5810c6340597435ac8a96f75d08872a86a`; subsequent repair-only commit
`25ad84a` does not change the rendered application paths. The final automated gate at
`25ad84a` passed **220 Core, 331 Web and 34 Python tests**, with zero failed/skipped
.NET tests, plus Web/MCP/repair builds and `git diff --check`.

- All 24 planned continued browser/PDF criteria passed: native Print Report twice and
  genuine X11 Ctrl+P opened Chrome print previews; Escape cancelled each, titles were
  restored, timestamps refreshed and Last optimized stayed unchanged.
- Locked job/parts/stock/saved-plan snapshots were identical before/after printing and
  final SQL readback. The stored `0.0598` wall matched field, Size, title, preview and
  SQL/API values after blur.
- Six saved plans reconciled list quantities, actual cut rows, pieces, kerf, waste and
  efficiency. The mixed plan used seven bars; the long plan used 132 bars in 72 list rows.
- Five real Letter PDFs produced **24 rendered pages**, all individually visually
  inspected. The 19-page long report retained all 72 list rows, 132 pieces, 100 numbered
  notes and its final marker. No visible clipped or split individual rows were found.
- Toolbar bounds and engine-prefix integrity passed at 1400, 1000 and 390 pixels.
  The favicon and served assets matched the built app. Nine independent route checks
  had no console/page/HTTP/request failures. Earlier aborted Blazor disconnect beacons
  from shared-page navigation were retained and explained, not hidden as clean captures.

**Additional exploratory failure, not fixed by this plan:** posting wall `0.083001`
returns/generates that exact Size label before SQL's `decimal(10,4)` persists wall
`0.083`. The original higher-precision fixture and mismatch evidence remain intact;
this is an API/storage precision-normalization follow-up, not a failure of the required
stored `0.0598` presentation case. Thus the full exploratory report honestly records
**24 PASS / 1 FAIL**; do not describe it as zero outstanding defects or use that mismatched
fixture for README publication. Input precision validation and generated-name timing
need a separately tested API/storage change.

Evidence is at `/home/aj/extracted/2026-10-03/cutlist-screenshot-readiness/`:
`task10-remaining-acceptance.json`, `parent-acceptance-verification.json`,
`parent-native-and-final-lock-proof.json`, `parent-visual-review.json`,
`automated-gate/summary.json`, PDFs/text/page manifest, and the implementation-test
archive/manifest. The disposable app and SQL container are stopped/removed, their port
is closed, and the isolated worktree (including EF-generated backslash build debris)
is removed. At that verification checkpoint, production repair/deployment and README
publication had not been performed; the later README publication is recorded below.

## Published README example

The README screen captures use a separate, fictional workshop-frame job captured from
`b42e0aff1fee6c30d62b1ab608ea5d7ba8fd9356` with Chromium 154.0.8037.97 on 2026-10-03.
The PDF and its preview were subsequently regenerated with the margin correction below.
These assets do not reuse the higher-precision mixed-material fixture described above.

- Material: Steel/A500 Square Tube, size 2 inches and wall 0.125 inches, created through
  the material API and read back as `2" x 1/8" wall`.
- Parts: four each of RAIL 47-7/8 inches, LEG 29-7/8 inches, and BRACE 17-7/8 inches.
- Job stock: one custom 96-inch bar at priority 0; unlimited catalog 144-inch bars at
  priority 1; two catalog 240-inch bars at priority 2. Bandsaw kerf is 1/16 inch.
- Persist through the real UI's Exhaustive Optimize action, then reload Results before
  capturing. The saved result has 3 used bars (two 144-inch and one 96-inch), 12 pieces,
  no unplaced parts, and 3/4 inch remaining waste. The unused 240-inch stock is absent
  from Material List. Reported 99.8% efficiency includes kerf in consumed length;
  the Results-tab badge rounds that same efficiency to a whole percent.
- Capture Results at 1400×1080 and Stock at 1400×900 without changing rendered content.
  Generate a real one-page Letter PDF from Results and render it at 1400 pixels wide.
  Verify all three rows, twelve cut badges, notes, and fresh print timestamp.
- Keep losslessly compressed, pixel-identical PNGs in `docs/images/` (each below 300 KB)
  and the actual report in `docs/examples/sample-cut-list.pdf`. README links use relative
  paths, meaningful alt text, full-size image targets, and a direct PDF link.
- Recheck every image and the PDF for privacy, clipping, and inconsistent labels before
  replacing binaries. Verify README anchor/file links and remote asset hashes after push.

Capture/API/PDF evidence is under
`/home/aj/extracted/2026-10-03/cutlist-readme-publication/`. Only the disposable loopback
SQL/app environment was used; no production catalog, credentials, or customer data were
accessed. This publication does not apply the production material-label repair or deploy
an application change.

### Physical-margin correction

The first published sample passed content checks but incorrectly had no physical page
margin. Its drawing bounds extended from -8.5 to 620.5 points on a 612-point-wide Letter
page: the missing `@page` inset and Bootstrap summary-row negative gutters both mattered.
That earlier visual review did not catch the page-perimeter defect.

The corrected `report.css` applies `@page { margin: 0.5in; }` in print media, without a
fixed paper size, and resets the summary row's margins. Two new regressions failed on
the previous CSS; all 36 Python regressions now pass. The actual Web app was published
and run against a new loopback-only disposable SQL database, then the same synthetic
job was saved/reloaded and printed without Playwright margin overrides. The sample PDF
and its README preview were regenerated; the two screen captures are unchanged.

Verified on Chromium 154.0.8037.97 against source base `0bc642c` plus the recorded margin
patch: one-page Letter and A4 samples, plus a five-page report with 24 material-length
rows, 24 individually named cut rows, and 20 notes. Every one of the seven rendered
pages was visually reviewed and its text/drawing bounds measured. Letter side margins
are 36 points; the A4 right edge is 35.67 points due to browser coordinate rounding.
Every page stays within the half-inch target using the documented half-point tolerance.
All sample cut labels, long-report rows, repeating headings, and the final notes marker
remain present; no split cut rows or clipped text were found.

Evidence: `/home/aj/extracted/2026-10-03/cutlist-print-margins/`, including the source diff,
API fixture/readbacks, raw PDFs, all page renders, `margin-verification.json`, and
updated-asset hashes. The original failed sample remains in the prior publication
archive. Production repair and deployment are not part of this correction.

Next hardening: the documented API precision boundary, general catalog import identity,
nominal dimensions/pipe-wall precision policy, removal of the unused report component,
and consistent server/browser timestamp conventions. These are not silently bundled
into the accepted presentation and guarded-repair work.
