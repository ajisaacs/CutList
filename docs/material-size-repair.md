# Bundled material-size label correction

## Scope and verified catalog facts

This is a **seed-label correction for fresh/disposable imports**, not an existing-database
migration. It changes only reviewed `materials.<group>[index].size` strings in
`CutList.Web/Data/SeedData/oneals-catalog.json`. There are no service, schema, import-identity,
production-data or deployment changes.

The pre-edit file was parsed directly and checked against the baseline audit:

| Shape group | Entries | Corrected generated labels |
| --- | ---: | ---: |
| Angles | 98 | 0 |
| Channels | 65 | 15 |
| Flat bars | 177 | 0 |
| I-beams | 71 | 0 |
| Pipes | 30 | 0 |
| Rectangular tubes | 70 | 16 |
| Round bars | 30 | 0 |
| Round tubes | 16 | 15 |
| Square bars | 11 | 0 |
| Square tubes | 48 | 15 |
| **Total** | **616** | **61** |

These are bundled-file counts, **not assumed production-database counts**. The file also
retains all 810 stock entries. Normalized shape/size keys are unique before and after.

The 46 non-sixteenth tube walls now use the shared exact-decimal-or-fraction helper:
`0.0598`, `0.065`, `0.0673`, `0.0747`, `0.0897`, `0.1196`, `0.12`, `0.1345` and `0.293`
remain decimal wall measurements; exact sixteenth multiples keep simplified fractions.
For example, `1" x 0.0598" wall` and `1" x 0.0673" wall` are different square-tube sizes,
not the same rounded `1/16"` gauge. No gauge number is inferred.

## Generated-label review and semantic preservation

Each candidate's numeric dimensions were mapped to its actual dimension type. The old
`MaterialDimensions`, `ArchUnits` and `FormatHelper` implementations at `613064b` were
executed alongside the reviewed current generator. A label was eligible only when it
exactly equaled the old generated string and the current generated string differed.
The replacement was recomputed from those numeric dimensions, not globally substituted
from an old wall label.

The audit covered all ten shapes, including effects of the ordinary nearest-1/16 length
formatter. Outside the three tube groups, only 15 channel strings differed. Each matched
the old generator exactly; 14 correct truncation-to-nearest rounding and one restores the
`0-` prefix on a sub-inch remainder after feet. No differing authored/ambiguous labels were
found in this bundled file. Future authored/ambiguous labels must be left unchanged and
reported, not rewritten just because they differ from a generated name.

Some channel dimensions already produce unusual feet-based labels (for example, the
source `flange: 13.8` and `web: 40.0`). This change does **not** certify or reinterpret those
measurements, change units, or repair supplier data; it preserves the stored numbers and
only corrects the demonstrated generated formatting.

A pre-edit snapshot and recursive semantic diff establish that exactly the 61 reviewed
`size` fields changed. All numeric tokens, material types, grades, descriptions, stock
fields, legacy supplier/inventory fields, export metadata, property order and entry order
are retained. Raw file text outside the reviewed label tokens is also identical.
`CatalogSizeFormattingTests` freezes the preserved JSON as a SHA-256 assertion excluding
only those 61 paths, so unreviewed label or non-label changes cannot slip through a DTO
round-trip. Intentional future catalog edits require a new reviewed baseline, not blindly
updating that assertion.

## Fresh/disposable import regression

Run the real SQL Server regression from the repository root (Docker is required):

```bash
flock --close -w 180 "$TMPDIR/cutlist-build.lock" \
  dotnet test CutList.Web.Tests/CutList.Web.Tests.csproj \
  --filter FullyQualifiedName~CatalogSizeFormattingTests
```

The test uses a dedicated freshly migrated disposable SQL Server fixture and asserts the
material and stock tables are empty before posting the actual JSON to `/api/catalog/import`.
It verifies empty errors/warnings, 616 created materials and 810 created stock rows; reads
back every material through fresh EF contexts and `/api/materials/{id}`; checks exact labels,
dimension types/values, metadata, distinct wall-gauge row IDs, and exported labels. Repeating
the same corrected import creates zero materials and zero stock rows, updates the same 616
materials/810 stock rows, and preserves all material/dimension/stock IDs and measurements.

The original file first produced five expected failing test cases (three tube groups,
reviewed channels and persisted import labels); count/semantic-preservation characterizations
passed. The corrected file passes all seven cases. Optional `CUTLIST_TASK6_EVIDENCE_DIR`
records actual import summaries, persisted-ID snapshots and exports. Development audit,
semantic before/after files, logs and TRX results are kept under the scratch `cutlist-task6`
directory rather than committed as production artifacts.

## Existing databases: separate, gated repair (Task 9)

**Do not import the corrected catalog over a database containing old labels to migrate it.**
`CatalogService` identifies a material by shape plus case-insensitive `Size`; a corrected
label no longer matches the old row. Re-importing can therefore create duplicate materials
and stock rows instead of renaming the existing IDs. Repeat-import idempotence in an empty
then corrected database does not prove safety over old or custom names.

Existing-database repair is a separate Task 9 and must not run until its implementation,
dry-run manifest, review and explicit operator approval are complete. It must:

- Record a database backup and an exact source/target identity manifest before any mutation.
- Read live counts and stable material/dimension IDs; verify shape and exact dimensions,
  associated stock, job references and current labels against the reviewed candidates.
- Preserve authored/custom names. Never infer repair eligibility from a matching substring,
  nominal gauge, a rounded display, or bundled-file counts.
- Produce reviewed old/new snapshots keyed by stable material ID plus verified dimensions;
  detect case-insensitive identity collisions and ambiguous candidates before applying.
- Apply only approved label changes transactionally with compare-and-swap guards over the
  original label and identity/dimensions. Reject stale state; do not retry or partially apply.
- Preserve numeric dimensions, types/grades/descriptions, stocks, all references, saved plan
  JSON and job lock state. Verify these through fresh reads after repair.
- Keep a backup/rollback manifest with old and applied labels and reverse compare-and-swap
  guards; rollback must not overwrite a later custom edit.

Historical reports are not completely label-frozen: `SavedOptimizationResult.ToPackResultAsync`
loads the live `Material` by saved `MaterialId` and skips a saved material result if that row
is missing; it does not restore the stored `MaterialDisplayName`. Renaming that catalog row
can change the displayed/printed historical label,
including for a locked job, without changing its saved optimization JSON or lock state.
That display consequence must be disclosed and accepted before any existing-database repair.
