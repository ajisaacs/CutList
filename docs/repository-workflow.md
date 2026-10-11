# Repository workflow

## Primary repository

GitHub is the source of truth for CutList:

- Repository: https://github.com/ajisaacs/CutList
- Git remote: `https://github.com/ajisaacs/CutList.git`
- Default branch: `master`
- Push commits and tags, open and merge pull requests, track issues, and publish future releases on GitHub.

For an existing clone, run from its repository directory:

```bash
git remote set-url origin https://github.com/ajisaacs/CutList.git
git fetch origin
git remote -v
```

For a new clone:

```bash
git clone https://github.com/ajisaacs/CutList.git
```

## Gitea backup

https://git.thecozycat.net/aj/CutList is a read-only pull mirror of GitHub, configured to synchronize hourly. It backs up Git branches and tags, not GitHub issues, pull requests, comments, releases, release assets, or Actions configuration/secrets. Pushes to this mirror are not supported; all development belongs on GitHub.

The original Gitea repository is preserved read-only at https://git.thecozycat.net/aj/CutList-before-github-primary. Its history and repository metadata have not been deleted. On the 2026-10-02 cutover, all 252 existing commits on `master` were copied to GitHub without rewriting history; there were no tags, issues, pull requests, releases, or wiki pages to import. The original tip was `bf4297ab7ff47d51221b2df5f1188343918e5b2c`.

Do not re-enable a Gitea-to-GitHub push mirror: it could overwrite GitHub-side changes or remove GitHub-only branches. Restoring the old repository as writable would require an explicit, coordinated reversal of this workflow, not merely unarchiving it.

## Container publishing: GHCR

The active workflow is `.github/workflows/build-cutlist.yml`. It builds `CutList.Web/Dockerfile` with the repository root as the build context and publishes to GitHub Container Registry:

- `ghcr.io/ajisaacs/cutlist:latest`
- `ghcr.io/ajisaacs/cutlist:<full-commit-SHA>`

Images are **public**, explicitly requested by the owner so anyone can download them without credentials. The workflow uses the built-in `GITHUB_TOKEN` with only `contents: read` and `packages: write`; no custom registry secret is required. The source label links the package to this repository. Before uploading, the workflow verifies the existing package is public; it then publishes the commit tag and `latest`, reads back both manifests to confirm they match, and logs out before testing anonymous pulls.

Relevant pushes to `master` trigger publishing: web/core source, solution/build configuration, Docker context exclusions, the workflow, or its contract tests. Manual runs are also available:

```bash
gh workflow run build-cutlist.yml --repo ajisaacs/CutList --ref master
```

The publishing job refuses non-`master` refs and fork repositories. A serialized concurrency group prevents overlapping publishers. Checkout is pinned to a commit and does not persist credentials; `.dockerignore` excludes local credentials, Git/agent metadata, and build outputs.

### Pulling public images

No GitHub account, token, or registry login is required:

```bash
docker pull ghcr.io/ajisaacs/cutlist:latest
# For a repeatable rollout, choose the full commit-SHA tag instead.
```

Publishing does **not** change or restart the running CutList application, its database, or its deployment image reference. Existing deployments using `git.thecozycat.net/internal/cutlist` will remain on that registry until a separately verified rollout switches them. Keep the old registry images for rollback; do not delete them or change visibility/access to the old registry packages.

Package settings: https://github.com/users/ajisaacs/packages/container/cutlist/settings. If the GHCR package is removed and recreated, confirm its visibility is public in those settings before rerunning this workflow; the preflight deliberately fails closed when package metadata is unavailable or not public.

The legacy `.gitea/workflows/build-cutlist.yml` is retained as a historical reference. It does not run on the read-only mirror, and the original repository remains archived. The old Actions-secrets migration blocker no longer applies to publishing because GHCR uses the workflow token; pushing workflow changes still requires GitHub Workflows write permission.

`CutList.Web` directly references `Microsoft.OpenApi` 2.7.5, the first patched 2.x release for [GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc), overriding Swashbuckle 10.1.3's vulnerable 2.4.1 transitive dependency without changing its major-version API. Keep this override until the parent package's dependency floor is patched. Verify upgrades with `dotnet list CutList.Web/CutList.Web.csproj package --vulnerable --include-transitive` and an isolated Development-mode Swagger document smoke test; Production must continue to keep Swagger disabled.

Next hardening: consider image vulnerability scanning. A clean NuGet audit does not cover base-image OS packages.

## Reusable drawing-set library (checkpoint 1)

`20261011033059_AddPartSetLibrary` is an additive migration creating only `PartSets` and `PartSetMembers`; no existing jobs, job parts, stock or catalog rows are rewritten. Its rollback drops members before sets and destroys library data, so it is not a routine production rollback command. The web host still does not apply migrations on startup.

- `PartSetService` is scoped and uses `IDbContextFactory<ApplicationDbContext>`. This checkpoint exposes in-process create/get/list/update/archive only: no UI, REST endpoints, Excel reader, job expansion or job-instance provenance yet.
- Drawing identity is `(DrawingKey, RevisionKey)`: the service trims drawing/revision and uppercases invariantly; null/blank revision becomes the empty key. SQL persists both required keys with `Latin1_General_100_BIN2` and a composite unique index, including archived sets. All library writes must go through the service to maintain canonical keys; the database index also protects competing inserts. Source item/file text is retained without normalization.
- Member length uses `decimal(10,4)`, matching job cut precision. The service rejects zero/negative, overflow and values that SQL would round; quantity must be positive. SQL CHECK constraints also enforce positive lengths and quantities. The service copies editable data rather than attaching caller-supplied IDs or navigations, validates active catalog material IDs, and saves the set/member graph atomically. Empty library drafts are currently allowed; they must not become usable job sets without the later expansion validation.
- Set-to-member deletion cascades. Member-to-material deletion is **NO ACTION**, with no connection to jobs and no new multiple-cascade path. Archiving hides a set from default lists without deleting members; exact-ID reads remain available. Fixture reset deletes members/sets before catalog rows.

Scaffold migrations from freshly built matching source, inspect `Up`, `Down`, designer and snapshot, generate/review idempotent SQL in private scratch storage, and run the SQL-backed suite (whose fixture calls `MigrateAsync` on disposable Testcontainers SQL Server). Do **not** treat `dotnet ef database update --project CutList.Web` as a safe default: it can resolve the implicit appsettings database. Production application requires separate named-target authorization, a verified backup, migration-history/schema review and post-application verification before an app rollout. Generating SQL or committing this checkpoint grants no deployment permission. Remove served-purpose scratch SQL and EF tooling's untracked Linux `CutList.Web/bin\\Debug/` output after inspection; preserve `.hermes/`.

## Atomic drawing-set job instances (checkpoint 2)

`20261011033829_AddJobPartSetInstances` adds `JobPartSetInstances` and nullable `JobParts.PartSetInstanceId`. Existing manual parts remain unlinked; no existing job or library content is rewritten. This is an in-process service checkpoint only: no new UI, REST surface or Excel handling yet.

- `JobService.AddPartSetAsync(jobId, partSetId, quantity)` reads the persisted unlocked job, requires a usable nonarchived/nonempty set and active materials, revalidates every cut, and calculates checked per-member quantities and appended sort orders before staging writes. It snapshots drawing number, revision, display name and assembly quantity on the instance, and material/name/length/quantity/order on independent ordinary job parts. Library edits/archive do not alter those snapshots. Stock must still be configured separately.
- Set expansion is limited to **10,000 total expanded pieces per job**, including existing manual rows and all existing set snapshot rows (counted once, not instance assembly quantities). `JobService.MaxExpandedJobPieces` defines the admission limit; `AddPartSetAsync` computes the persisted SQL aggregate and incoming products in `long` before staging any writes. Exactly 10,000 is accepted; requests exceeding the limit return a clear 400 without changing instances, cuts, timestamps or saved plans. This guards set admission, not concurrent unlocked-edit versioning or retroactive validation of legacy/manual jobs. Overview quantity aggregates cast to SQL `bigint`, with `long` total/recent-job piece counters, so pre-existing larger data remains readable without integer overflow.
- `PartSetService.CreateAsync`/`UpdateAsync` reject null input, null `Members`, and null member entries with `ArgumentException` before dereferencing/sorting/querying input; library and job data remain unchanged.
- Add and `RemovePartSetInstanceAsync(jobId, instanceId)` each save the entire mutation, parent timestamp and saved-plan invalidation atomically. Removal verifies the job/instance pair and explicitly deletes its linked copies before the instance; it never removes standalone parts or another instance. Missing job/instance is `KeyNotFoundException`; unusable sets, invalid quantities and overflow are `ArgumentException`.
- Both methods preserve persisted lock enforcement and the forced parent `LockedAt` concurrency-token update. Locked jobs and locks racing before save reject with the existing job-domain conflict types, with no partial rows or saved-plan changes. No automatic retries. This remains lock-state protection, not general versioning of concurrent unlocked edits.
- `UpdatePartAsync`/`DeletePartAsync` reject persisted set-created rows with a clear `ArgumentException`, even if the caller forges a null provenance link. Existing individual part PUT/DELETE routes return 400 with that explanation; lock conflicts remain 409. Customize with additional manual rows or remove/re-add the whole instance. Manual-add callers cannot attach instance links through input IDs or navigations.
- `DuplicateAsync` copies every instance's snapshot metadata with new IDs, maps only its own member copies to the new instance and keeps manual rows standalone. The duplicate is unlocked, without saved results; its multi-save operation is wrapped in one transaction. Job deletion cascades to both parts and instances, verified on SQL Server.
- SQL uses job-to-instance CASCADE, library-to-instance NO ACTION and part-to-instance NO ACTION (no second cascade route). The part link is a composite FK `(PartSetInstanceId, JobId)` to `(Id, JobId)`, preventing cross-job provenance even for direct SQL writes. An instance CHECK requires positive assembly quantity. A used library set cannot be hard-deleted through SQL; archive it instead.
- Disposable fixture reset deletes parts before instances, jobs and library/catalog rows; whole-job snapshots now include instance metadata and links. SQL-backed coverage includes expansion/removal, snapshot isolation, duplicate/deletion, forged inputs, validation/overflow, deterministic lock races and injected post-write rollback. Migration coverage executes the full generated idempotent SQL against a fresh throwaway database, replays it there and over populated fixture jobs, and checks migration history and unchanged job snapshots.

Review/apply this migration only under the separate named-target, backup-aware production procedure above. Its `Down` removes provenance and instance metadata (the ordinary cut rows survive), so rollback is not lossless. Checkpoint verification uses disposable Testcontainers SQL Server only; neither SQL generation nor a commit authorizes deployment.

## Drawing-set Blazor workflow (checkpoint 3)

The Library navigation now includes **Drawing Sets** (`/part-sets`), with search by drawing number/name, 25-row pagination and an optional archived-set view. Create at `/part-sets/new` or open `/part-sets/{Id}` to edit drawing/revision/name and individual cuts, catalog materials, architectural lengths, per-assembly quantities and optional source item/file text. The editor requires at least one cut; service validation remains authoritative. Removing a draft cut and archiving a set require confirmation. Archiving hides it from new-job choices but preserves existing snapshots.

On an unlocked job's **Parts** tab, select a drawing set and positive assembly count to add a new independent instance. Its heading shows snapshotted drawing/revision/name and assembly count; its cut table shows expanded quantities (per-set quantity × assemblies). Set-created rows have no individual edit/delete controls. A confirmed **Remove Drawing Set** deletes only that whole instance; ordinary manual parts remain separately editable, and stock selection remains independent. Changing the library later does not change existing job copies; to revise them, remove/re-add the instance or add manual customization rows.

Add/remove use the existing `TryJobChangeAsync` lock recovery: a stale or racing lock closes confirmation dialogs, reloads persisted job/results and displays `#job-conflict-alert`, without retries or partial rows. Locked jobs show snapshot groups but no add/remove controls. A successful mutation clears the displayed saved plan after the service saves; optimization and the shared screen/print results continue to use actual expanded cut names and quantities rather than library rows or assembly labels.

bUnit coverage exercises library create/edit/search/archive, N=2 expansion, independent library edits/archive, saved-plan cut quantities, whole-instance removal, manual/stock preservation, locked presentation and deterministic stale/racing locks for both add and remove. Existing job locking, presentation and packing-engine components register the new library service dependency. This checkpoint adds no REST endpoints, Excel handling, migration, production SQL or deployment.

## Drawing-set REST surface (checkpoint 4)

The LAN-only API now exposes the same reviewed manual library and snapshot operations as Blazor:

| Method/path | Request / response |
| --- | --- |
| `GET /api/part-sets?search=...&includeArchived=false` | Ordered summaries; drawing/name search; archived hidden by default |
| `POST /api/part-sets` | Editable set fields below; 201 with detail and readback Location |
| `GET /api/part-sets/{id}` | Detail including ordered members, even when archived |
| `PUT /api/part-sets/{id}` | Full replacement of editable fields/members; 200 detail |
| `POST /api/part-sets/{id}/archive` | No body; 204; idempotent, never deletes snapshots |
| `POST /api/jobs/{id}/part-set-instances` | `{ "partSetId": 123, "quantity": 2 }`; 201 snapshotted instance with expanded parts and job-detail Location |
| `DELETE /api/jobs/{id}/part-set-instances/{instanceId}` | 204; removes only that job's whole instance |

Create/update use `drawingNumber`, optional `revision`, `displayName`, and `members`. Each member accepts only `name`, optional `sourceItem`/`sourceFile`, `materialId`, decimal `lengthInches`, integer per-assembly `quantity`, and `sortOrder`. Member IDs, canonical keys, archive state, job ownership, provenance, locks and optimization results are server-owned and not input fields. Unknown JSON properties cannot inject these values. Empty library drafts remain allowed but cannot be expanded; null lists/rows and invalid cuts are rejected. PUT replaces the entire member list, not a patch or revision propagation operation.

`GET /api/jobs/{id}` adds `partSetInstances` with snapshotted drawing/revision/name/assembly quantity and grouped expanded `parts`. The existing flat `parts` array and `/api/jobs/{id}/parts` remain; their only additive field is nullable `partSetInstanceId` (null for manual rows). Consumers must not sum flat and grouped arrays together. Library editing/archiving never updates captured job rows, and stock remains independent.

Invalid input, duplicate canonical drawing keys, unusable sets and checked expansion overflow return 400; missing resources or a job/instance ownership mismatch return 404 (including a wrong locked job). Both controller and service verify ownership. Existing persisted/racing lock and deleted-job conflicts retain 409 `application/problem+json` with `job_locked` / `job_changed`; rejected writes preserve instance rows, timestamps and saved plans. No automatic retries, lock input or new lock endpoint. Individual set-created part PUT/DELETE remain 400; ordinary manual clients remain supported.

This checkpoint has no schema change, Excel parsing, MCP tools, production SQL or deployment. Verification uses the disposable migrated SQL fixture, HTTP readback, full aggregate snapshots and deterministic competing saves. Production rollout still requires the separate backup-aware authorization above.

## Drawing-set MCP workflow (checkpoint 5)

The stdio MCP server now exposes `list_part_sets`, `get_part_set`, `create_part_set`, `update_part_set`, `archive_part_set`, `add_job_part_set`, and `remove_job_part_set_instance`. Every operation calls the REST API through `ApiClient`; there is no direct database access. Library tools return `{ success, error, data }`; job-instance mutations return `{ success, error, job }` with full persisted job readback. Create/update/archive and instance add/remove read back their exact target before reporting success. A readback failure can follow a committed write: inspect the target before retrying, never retry automatically.

1. Use `list_materials` (or explicitly search the catalog), then have the operator review **each** material ID, cut name, exact decimal inch length, positive integer per-assembly quantity and optional source text. No automatic material matching or inference.
2. Call `create_part_set` with `drawingNumber`, optional `revision`, `displayName`, explicit `members`, and required `operatorReviewed: true`. Each member has `materialId`, `name`, `lengthInches` (decimal, not architectural text), `quantity` (integer), optional `sourceItem`/`sourceFile`, and `sortOrder`. Review must actually occur; the flag is an acknowledgement, not permission to invent values. Unreviewed submissions are refused before HTTP. The service orders by supplied `sortOrder` and compacts stored order to zero-based indices; no lengths are rounded.
3. Example reviewed input: `{"drawingNumber":"DR-100","revision":"A","displayName":"Frame","operatorReviewed":true,"members":[{"materialId":123,"name":"Rail","lengthInches":24.125,"quantity":3,"sourceItem":"1","sourceFile":"DR-100.dwg","sortOrder":0}]}`. `123` is illustrative only; choose a verified active catalog ID, never copy it blindly.
4. Use `add_job_part_set(jobId, partSetId, quantity)` with a positive integer assembly count. A count of 2 with per-assembly quantity 3 produces 6 cut pieces. Stock must still be configured separately via `add_job_stock`.
5. `get_job` exposes `partSetInstances` (snapshot metadata and grouped expanded parts) plus nullable `partSetInstanceId` and order on flat `parts`. Both views contain the **same** cuts; do not sum them. Remove with `remove_job_part_set_instance(jobId, instanceId)`, using the instance ID, not the library set ID. Manual rows and other instances remain.
6. `update_part_set` replaces all editable fields and the full member list; it is not a patch. Existing job snapshots never change. Archive preserves exact-ID reads and existing instances; `list_part_sets(includeArchived: true)` includes archived sets. To revise a job snapshot, remove/re-add its whole instance or add separate manual rows.

All new tools return `success: false` on HTTP failures. Validation explanations (400), missing/ownership mismatch (404), and existing `job_locked`/`job_changed` conflict details (409) remain visible. No lock/unlock tool, automatic retry or partial expansion. Empty drafts follow REST semantics and cannot be added to jobs. SQL-backed MCP tests exercise the real client/tools against the disposable WebApplicationFactory database, including review refusal, invalid precision/materials/quantities, overflow, lock conflicts, ownership, snapshots and readback.

**No-parser boundary:** this feature has no Excel BOM parser, spreadsheet upload, import preview, file-reader tool or automatic material inference. Source item/file fields are operator-supplied provenance text only; they do not open or parse a file. Reviewed values are entered via MCP (or the existing manual UI/REST workflow). This checkpoint changes no schema and authorizes no production database access, deployment or push.

## Fresh SQL Server deployments

The web host does not migrate or populate the database on startup. Apply EF migrations explicitly before starting it; for an offline SQL Server target, generate an idempotent SQL script with `dotnet ef migrations script --idempotent --project CutList.Web/CutList.Web.csproj --output <private-path>`. The generated file has a UTF-8 BOM; strip it when piping through `sqlcmd` stdin. Historical data-copy migrations defer SQL Server's column binding with `EXEC(N'...')` so the generated script works on a fresh database as well as on replay. Test the script against only the intended fresh database and verify `__EFMigrationsHistory`, schema reads, and an idempotent replay before deploying an app image. Do not apply this script blindly to an existing production database.

The bundled O'Neal catalog is not imported on startup. On a verified empty material catalog, POST `CutList.Web/Data/SeedData/oneals-catalog.json` to `/api/catalog/import`; inspect `errors` and `warnings` even on HTTP 200, then read back `/api/catalog/export`. Never re-import the corrected catalog over legacy labels as a repair: shape/size identity can create duplicate materials. See `docs/material-size-repair.md`. The Barge Compose declaration and its target-specific setup/recovery instructions live in the `docker-barge` repository under `cutlist/`.
