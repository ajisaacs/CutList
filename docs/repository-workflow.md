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

## Fresh SQL Server deployments

The web host does not migrate or populate the database on startup. Apply EF migrations explicitly before starting it; for an offline SQL Server target, generate an idempotent SQL script with `dotnet ef migrations script --idempotent --project CutList.Web/CutList.Web.csproj --output <private-path>`. The generated file has a UTF-8 BOM; strip it when piping through `sqlcmd` stdin. Historical data-copy migrations defer SQL Server's column binding with `EXEC(N'...')` so the generated script works on a fresh database as well as on replay. Test the script against only the intended fresh database and verify `__EFMigrationsHistory`, schema reads, and an idempotent replay before deploying an app image. Do not apply this script blindly to an existing production database.

The bundled O'Neal catalog is not imported on startup. On a verified empty material catalog, POST `CutList.Web/Data/SeedData/oneals-catalog.json` to `/api/catalog/import`; inspect `errors` and `warnings` even on HTTP 200, then read back `/api/catalog/export`. Never re-import the corrected catalog over legacy labels as a repair: shape/size identity can create duplicate materials. See `docs/material-size-repair.md`. The Barge Compose declaration and its target-specific setup/recovery instructions live in the `docker-barge` repository under `cutlist/`.
