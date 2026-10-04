# Bundled material-size label correction

## Scope and verified catalog facts

This opening section documents the **seed-label correction for fresh/disposable imports**,
not an existing-database migration. That correction changes only reviewed
`materials.<group>[index].size` strings in `CutList.Web/Data/SeedData/oneals-catalog.json`;
it makes no schema, import-identity, production-data or deployment changes. The separately
invoked existing-database repair tool and its production gate are documented below.

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
records actual import summaries, persisted-ID snapshots and exports. The implementation
session's audit, semantic before/after files, logs and TRX evidence are preserved in
`/home/aj/extracted/2026-10-03/cutlist-screenshot-readiness/implementation-test-evidence.zip`
(with a companion manifest), not committed as production artifacts. Served-purpose
scratch audit projects are removed after verification.

## Existing databases: explicit guarded repair (Task 9)

**Do not import the corrected catalog over a database containing old labels to migrate it.**
`CatalogService` identifies a material by shape plus case-insensitive `Size`; a corrected
label no longer matches the old row. Re-importing can therefore create duplicate materials
and stock rows instead of renaming the existing IDs. Repeat-import idempotence in an empty
then corrected database does not prove safety over old or custom names.

`tools/CutList.MaterialRepair` is a separate, offline SQL Server operator tool. It does not
start the web host, read appsettings, run migrations, register startup maintenance, expose
an endpoint, insert/delete rows, or save jobs. **Implementing/testing this tool is not
production authorization. No production database was discovered or repaired for Task 9.**

### Eligibility, manifest and transaction contract

The default mode is dry-run. It reads all materials (active and inactive) and their dimension
rows in a SQL Server `SERIALIZABLE` transaction, verifies the actual connected identity,
and writes a reviewable JSON manifest. Each entry records stable material ID, shape/type/
grade, dimension ID/material ID/concrete type and exact invariant decimal values, old/new
`Size`, reason/status and baseline `UpdatedAt`. The header records version/kind, creation
time, actual `SERVERPROPERTY('ServerName')`, `DB_NAME()` and `service_broker_guid`.
The GUID is an additional stale-target guard, not proof of a backup or production approval.

Automatic eligibility is deliberately narrower than arbitrary catalog normalization:

- Only Round Tube, Square Tube, Rectangular Tube and Channel are inspected for repair.
- The old string must **exactly** equal the reconstructed `613064b` generator for that
  row's persisted numeric dimensions; the replacement is the current dimension generator.
  Tube walls retain exact decimals unless they are exact multiples of 1/16 inch. Channel
  dimensions use ordinary nearest-1/16 formatting. Nothing infers nominal gauges or units.
- There must be exactly one correctly typed dimension row. Dimensions must be positive;
  twice a tube wall must be smaller than the tube's smallest outer dimension, and a channel
  web must be smaller than both height and flange. Suspicious measurements are skipped,
  not reinterpreted. This can skip legacy supplier channels mentioned above; the bundled
  61-label correction is not a forecast of database repair counts.
- `repair` means proven old-generated label with a different current label; `unchanged`
  means already current or unsupported shape; `customname` preserves an authored/ambiguous
  string; `missingdimensions` preserves a missing, ambiguous, mismatched or invalid
  measurement; `collision` marks a proposed shape/size conflict. Reasons distinguish these
  cases. Skipped entries are never written.

Apply re-reads every entry retained in the reviewed manifest in **one `SERIALIZABLE`
transaction**, verifies connected server/database against the explicit expected strings
(case-sensitive ordinal comparison) and the full manifest target, then recomputes the proof.
ID, shape/type/grade, exact dimension identity/values, old label, baseline timestamp and
reason/status/new label must still match. A vanished/replaced dimension on a proposed
repair, any reviewed-entry drift, or any collision rejects the entire batch, without retry.
Already-skipped invalid dimensions do not make other proven entries repairable and are
left untouched. Newly added unrelated materials are not themselves reviewed candidates,
but are included in the collision check.

Collision checks use SQL Server equality/collation, including case/trailing-space behavior,
all existing rows regardless of type/grade/activity, and pairs of proposed new labels.
The key is shape plus size, not type or grade. A dry-run `collision` entry cannot be applied
unchanged. Do not change its status or proposed size to bypass rejection; resolve the cause
outside this tool and produce a new dry-run for review. If an operator deliberately limits
approval, they may remove whole entries from a copy of the manifest, not change their fields;
all remaining entries are revalidated and collisions against the whole database still apply.

Only `Materials.Size` and `Materials.UpdatedAt` are updated. Each repaired material gets a
UTC `UpdatedAt` recorded in its before-image. IDs, other metadata, dimensions, stock lengths,
references, jobs, saved-plan JSON, lock state and sort order are untouched. Shared/range locks
from the full catalog/dimension reads protect the recheck-to-write window; the tool may block
ordinary catalog writers or encounter a deadlock. There is no automatic retry or partial apply.
A subsequent dry-run proposes no already-applied repairs.

### Artifact safety and durability

The CLI currently requires **Linux**, .NET 10, SQL Server and an already-migrated schema.
Other operating systems fail closed instead of silently using a weaker artifact-durability
path. Use an operator-owned, private directory on a persistent filesystem with working file
and directory `fsync`; do not use disposable scratch space for production recovery artifacts.
The examples below use a new private directory beneath `$HOME`, not a shared temporary path.

All artifact paths must be absolute and canonical (no `.`/`..` or redundant separators), with
an existing parent directory. Symlink files/directories, special files and multiply hard-linked
inputs are rejected. New output paths must not exist. Linux directory components are opened
with `openat`/`O_NOFOLLOW`; input/output opens stay relative to those directory descriptors.
`O_EXCL` rejects concurrent output creation rather than overwriting it. Inputs are checked
on their open descriptor as regular files with one hard link; FIFO input cannot hang the tool.
New files have mode `0600` (subject to a more restrictive umask).

Before the first database UPDATE, the apply callback makes the artifact directory's ancestry
durable, serializes the rollback before-image, flushes its file to disk, closes it and `fsync`s
the **same directory descriptor** used to create it. Serialization/open/file-flush/directory-
flush failure aborts the transaction before any write. A failure before commit rolls back
the batch, but an already-created recovery file is deliberately retained; it is not proof
that the database commit succeeded. A connection failure during commit can leave an unknown
commit outcome. An I/O failure can leave a partial artifact. Do not overwrite/reuse that
path or blindly retry.

This is not protection against an administrator/operator deleting, editing or relocating an
artifact or its directory, storage hardware ignoring flushes, or a hostile same-user process.
Keep the directory and all ancestors under trusted control, exclude concurrent artifact
writers/movers, retain the reviewed file unchanged and copy verified recovery artifacts to
the approved backup location. The service API itself delegates durability to its callback;
use the CLI's implementation rather than a no-op callback for operational apply.

### Operator commands (disposable example, not production permission)

Build from the repository root. Supply the connection **only** through the named secret
environment variable using your approved secret-management mechanism; do not put its value
in a command argument, source file or transcript. The tool never falls back to
`DefaultConnection`/appsettings and does not echo credentials, unknown argument values,
paths, SQL errors or exception stacks. Do not enable shell tracing around secret setup.

```bash
flock --close -w 180 "$TMPDIR/cutlist-build.lock" \
  dotnet build tools/CutList.MaterialRepair/CutList.MaterialRepair.csproj

# CUTLIST_REPAIR_CONNECTION must already be exported by the approved secret mechanism.
# Set these to the separately verified disposable connection's actual SQL values.
# The server value is SERVERPROPERTY('ServerName'), NOT a host alias or host:port string.
: "${CUTLIST_REPAIR_EXPECTED_SERVER:?set the reviewed actual SQL Server name}"
: "${CUTLIST_REPAIR_EXPECTED_DATABASE:?set the reviewed actual database name}"
: "${CUTLIST_REPAIR_CONNECTION:?load the explicit connection secretly}"

ARTIFACT_DIR="$HOME/cutlist-material-repair-disposable-review"
(umask 077; mkdir -- "$ARTIFACT_DIR")  # new directory; never overwrite an earlier review
CLI="tools/CutList.MaterialRepair/bin/Debug/net10.0/CutList.MaterialRepair.dll"

# Default dry-run: database unchanged; refuses to overwrite reviewed.json.
dotnet "$CLI" \
  --connection-env CUTLIST_REPAIR_CONNECTION \
  --expected-server "$CUTLIST_REPAIR_EXPECTED_SERVER" \
  --expected-database "$CUTLIST_REPAIR_EXPECTED_DATABASE" \
  --manifest "$ARTIFACT_DIR/reviewed.json"

# --dry-run can be supplied explicitly instead. Inspect/review the manifest before apply.
# APPLY ONLY after the named target, manifest and verified backup are explicitly approved.
dotnet "$CLI" \
  --connection-env CUTLIST_REPAIR_CONNECTION \
  --expected-server "$CUTLIST_REPAIR_EXPECTED_SERVER" \
  --expected-database "$CUTLIST_REPAIR_EXPECTED_DATABASE" \
  --apply "$ARTIFACT_DIR/reviewed.json" \
  --before-image "$ARTIFACT_DIR/before-image.json"

# Fresh read-only review after success; use a NEW output name.
dotnet "$CLI" \
  --connection-env CUTLIST_REPAIR_CONNECTION \
  --expected-server "$CUTLIST_REPAIR_EXPECTED_SERVER" \
  --expected-database "$CUTLIST_REPAIR_EXPECTED_DATABASE" \
  --dry-run --manifest "$ARTIFACT_DIR/post-apply.json"

# Separate explicit recovery decision; pass the rollback artifact, not the dry-run manifest.
dotnet "$CLI" \
  --connection-env CUTLIST_REPAIR_CONNECTION \
  --expected-server "$CUTLIST_REPAIR_EXPECTED_SERVER" \
  --expected-database "$CUTLIST_REPAIR_EXPECTED_DATABASE" \
  --rollback "$ARTIFACT_DIR/before-image.json"
```

Expected success output is a dry-run status/count summary, `applied: rows=...`, or
`rolled-back: rows=...`. Exit `0` means the requested operation completed; `2` is option/
artifact-policy failure; `3` is target/manifest/CAS/collision rejection; `4` is a suppressed
SQL/JSON/I/O failure. Unknown/repeated options and mixed modes are rejected. An error during
or after commit must be reconciled through fresh database reads and the retained artifact
before considering another operation; never treat a nonzero exit as permission to retry.
The tool has no backup, rollout, schema-discovery, permission-escalation or approval feature.

Rollback validates the connected target and every before-image entry, then checks that the
current label is exactly the applied new label, the current `UpdatedAt` is exactly the recorded
applied timestamp, and shape/type/grade plus dimension identity/values still match the proven
baseline. Restoring old labels must also be collision-free under SQL Server semantics. Any
failure rejects the **whole** rollback, preserving subsequent human edits. Success restores
both original `Size` and original nullable `UpdatedAt` in one transaction. A second rollback
of the same nonempty artifact fails CAS; it is not an idempotent force-restore command.

### Separate production gate and readback

Before an operational production apply, obtain a **new explicit approval for the named
server/database and data update** after showing its actual dry-run manifest and repair/
collision/custom/skipped counts. Independently verify the target identity, take and verify
a recoverable SQL Server backup, and record backup, reviewed manifest and rollback locations.
Quiesce catalog writers for the maintenance window; the tool's locks are not a rollout plan.
Do not discover/contact production just to run the automated tests below.

Before/after snapshots must cover exact material/dimension IDs and measurements, non-label
metadata, stocks and lengths, job references, saved JSON and lock state. After apply, read
back every repaired ID's label and applied timestamp, compare the preserved snapshots, run
a fresh dry-run, and inspect affected jobs' rendered labels. After rollback, similarly verify
the original labels/timestamps and preserved state. Persist the operator outcome separately;
this tool does not emit a post-commit database verification report or create a database backup.

Historical reports are not completely label-frozen: `SavedOptimizationResult.ToPackResultAsync`
loads the live `Material` by saved `MaterialId` and skips a saved material result if that row
is missing; it does not restore the stored `MaterialDisplayName`. Renaming that catalog row
can change the displayed/printed historical label,
including for a locked job, without changing its saved optimization JSON or lock state.
That display consequence must be disclosed and accepted before any existing-database repair.

### Disposable regression

```bash
flock --close -w 180 "$TMPDIR/cutlist-build.lock" \
  dotnet test CutList.Web.Tests/CutList.Web.Tests.csproj \
  --filter FullyQualifiedName~MaterialSizeRepairTests
```

These tests use the production relational provider in a disposable SQL Server container,
never appsettings or production. They verify all four supported shapes, preservation of
whole-catalog/job/dimension snapshots, default dry-run and all three real subprocess CLI
modes, strict option/target/path failures, stale and forged manifests, SQL-collation and
proposal collisions, vanished dimensions, failed before-image callbacks, failure after
actual SQL writes, rollback drift, repeat dry-run and concurrent catalog writes blocked
inside the recheck-to-write window. Native artifact handling is exercised on Linux;
power-loss/storage hardware behavior and operational production backup/approval remain
operator responsibilities. Raw development evidence stays in scratch `cutlist-task9`, not
in the committed operator manifests.
