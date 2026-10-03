# CLAUDE.md

> **IMPORTANT**: Always keep this document updated when functionality changes, entities are added/modified, new pages or services are created, or architectural patterns evolve.

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Workflow

- GitHub is the primary repository: `https://github.com/ajisaacs/CutList.git` (`master`). Push changes and open/merge pull requests on GitHub.
- Gitea `aj/CutList` is a read-only hourly pull mirror of GitHub, for code and tags only. Do not push to Gitea or treat it as the issue/PR/release tracker.
- The original Gitea repository is retained, archived, as `aj/CutList-before-github-primary`; do not delete it.
- GitHub Actions `.github/workflows/build-cutlist.yml` publishes public `ghcr.io/ajisaacs/cutlist` images (`latest` and full commit-SHA tags) for relevant `master` pushes or a manual run on `master`. Anyone can pull the images without credentials. Publishing does not redeploy the application.
- The legacy `.gitea/workflows/build-cutlist.yml` is retained as a historical reference and does not run on the backup mirror.
- See `docs/repository-workflow.md` for clone setup, backup boundaries, and container publishing/pull requirements.

## Project Overview

CutList is a 1D bin packing optimization application that helps users optimize material cutting. It calculates efficient bin packing solutions to minimize waste when cutting stock materials into required parts.

The solution contains four application projects plus test projects:

| Project | Framework | Purpose |
|---------|-----------|---------|
| **CutList** | .NET 10.0 Windows Forms | Original desktop UI (MVP pattern) |
| **CutList.Core** | .NET 10.0 Class Library | Domain models and packing algorithms (platform-agnostic) |
| **CutList.Web** | .NET 10.0 Blazor Server | Web-based UI + REST API, EF Core + SQL Server |
| **CutList.Mcp** | .NET 10.0 Console (stdio) | MCP server exposing CutList.Web's REST API as tools for Claude |
| **CutList.Core.Tests** | .NET 10.0 xUnit | Core formatting/packing tests |
| **CutList.Web.Tests** | .NET 10.0 xUnit + bUnit | Web/REST/MCP/Blazor integration tests against a disposable SQL Server container |

**Key Dependencies**: Math-Expression-Evaluator (input parsing), Newtonsoft.Json (serialization), Entity Framework Core (data access), Bootstrap 5 + Bootstrap Icons (UI), ModelContextProtocol SDK (CutList.Mcp)

## Build Commands

```bash
# Build entire solution
dotnet build CutList.sln

# Build specific projects
dotnet build CutList/CutList.csproj
dotnet build CutList.Core/CutList.Core.csproj
dotnet build CutList.Web/CutList.Web.csproj
dotnet build CutList.Mcp/CutList.Mcp.csproj

# Run applications
dotnet run --project CutList/CutList.csproj        # WinForms
dotnet run --project CutList.Web/CutList.Web.csproj # Blazor + REST API (default http://localhost:5270)

# EF Core migrations (always apply immediately after creating)
dotnet ef migrations add <Name> --project CutList.Web
dotnet ef database update --project CutList.Web

# Tests (Linux: build/test projects individually; the WinForms project is Windows-only)
dotnet test CutList.Core.Tests/CutList.Core.Tests.csproj
dotnet test CutList.Web.Tests/CutList.Web.Tests.csproj   # needs a Docker daemon
python3 -B -m unittest discover -s tests -p 'test_*.py'

# Clean build
dotnet clean CutList.sln
```

### Container deployment

CutList.Web images are built from `CutList.Web/Dockerfile` with the repository root as the Docker context and published publicly to `ghcr.io/ajisaacs/cutlist`, as explicitly requested by the owner. The workflow uses the built-in `GITHUB_TOKEN` (contents read, packages write), checks public visibility before uploading, reads back both tags to verify matching manifests, and logs out before verifying anonymous pulls. It does not restart services, change deployment configuration, or apply database migrations. See `docs/repository-workflow.md` for pull commands and rollout boundaries.

### Publishing CutList.Mcp

CutList.Mcp is an stdio MCP server, not a hosted service — it's published to `~/.claude/mcp/CutList.Mcp/` and registered in `~/.claude/settings.local.json` (see global `CLAUDE.md` MCP Server Publishing table). It talks to CutList.Web's REST API at `http://localhost:5270`, so CutList.Web must be running (dev `dotnet run` or a Docker container reachable at that URL) for the MCP tools to work.

## Architecture

### CutList.Core — Domain & Algorithms

**Key Domain Models**:
- **BinItem**: Item to be packed (label, length)
- **MultiBin**: Stock bin type with length, quantity (-1 = unlimited), priority
- **Bin**: Packed bin containing items, tracks remaining length
- **Tool**: Cutting tool with kerf/blade width

**Packing Engine**:
- `IPackingEngine` packs one stock length (`PackingRequest` -> `PackResult`); `MultiBinPacker(IPackingEngine)` (not itself an engine) runs that engine across stock types in priority order (then shortest first)
- Built-in engines (`BuiltInPackingEngines.All`, the single list; the first entry is the built-in default, used by WinForms and the MCP `create_cutlist` tools): `exhaustive` (`ExhaustiveSearchEngine`, default): groups parts by length and searches cut patterns for the fewest bars (`CutList.Core/Nesting/Search/`: `CutDemand` with the Martello-Toth L2 bound, `CutPatterns`, `MinBarsSearch`), starting from the First Fit plan, within `DefaultSearchBudget` steps per stock length; no part cap. When limited stock cannot hold every part, or the budget runs out before it beats First Fit, it returns First Fit's plan with `PackResult.FallbackEngine` set; `MultiBinPacker` carries that up from any stock length, and every reported engine name becomes `PackingEngineInfo.RunName` = "Exhaustive (First Fit fallback)" while the id stays `exhaustive`. The others: `firstfit` (`FirstFitEngine`, first-fit decreasing; when bar quantity is limited, a swap pass fills those bars tighter) and `bestfit` (`BestFitEngine`)
- `PackingEngineCatalog` (`IPackingEngineCatalog`) resolves ids case-insensitively, treats null/blank as the default, creates a fresh engine per run, and rejects unknown ids with `UnknownPackingEngineException` (message lists valid ids). Callers never reference concrete engine classes
- **Adding an engine**: implement `IPackingEngine` and add one `PackingEngineRegistration` to `BuiltInPackingEngines.All`. `CutList.Core.Tests/Nesting/EngineContractTests` then runs it through the shared contract (every part placed or reported as not placed, including same-named copies of a part, compared by reference; no overfilled bar, finite stock respected, `-1` = unlimited)
- `PackingRequest.MaxBinCount`: negative values (e.g. `MultiBin` quantity `-1`) are normalized to unlimited (`int.MaxValue`)

**Unit Handling**:
- `ArchUnits` — Converts feet/inches/fractions to decimal inches (accepts "12'", "6\"", "12 1/2\"", etc.)
- `FormatHelper` — Converts decimals to mixed fractions for display
- Internal calculations use inches; format on display

**Patterns**:
- `Result<T>` for standardized error handling (Success/Failure instead of exceptions)
- `IPackingEngineCatalog` for swappable algorithm implementations (see Packing Engine)
- Lower priority number = used first in bin selection

### CutList (WinForms) — Desktop UI

- MVP pattern: `MainForm` implements `IMainView`, `MainFormPresenter` orchestrates logic
- `Document` holds application state
- `CutListService` bridges UI models to core packing algorithms
- `DocumentService` handles JSON file persistence

### CutList.Web (Blazor Server) — Web UI + REST API

**Database**: SQL Server via Entity Framework Core (connection string: `DefaultConnection`)

**Service Registration** (Program.cs): All services registered as Scoped — MaterialService, StockItemService, JobService, CutListPackingService, ReportService, CatalogService. `IDbContextFactory<ApplicationDbContext>` is used (not a scoped `DbContext` directly) for Blazor Server circuit safety. `IPackingEngineCatalog` is a singleton built from `BuiltInPackingEngines.All` with the default from `Packing:DefaultEngine` (appsettings, `exhaustive`); an unknown configured id fails startup.

**REST API** (`Controllers/`): `JobsController`, `MaterialsController`, `StockItemsController`, `CuttingToolsController`, `PackingController`, `CatalogController` — Swagger/OpenAPI enabled in Development. This API is the integration surface `CutList.Mcp` calls into; the Blazor UI talks to the services directly and does not go through it. Packing engines: `GET /api/packing/engines` lists `{id, name, description, isDefault}`; `POST /api/jobs/{id}/pack` accepts an optional `Engine`; `POST /api/packing/optimize` accepts `Engine` (legacy alias `Strategy`). Both responses report the engine used, and an unknown id returns 400 listing the valid ids.

**Error handling**: `UseExceptionHandler("/Error", ...)` in non-Development environments routes to `Components/Pages/Error.razor`. `Controllers/JobMutationExceptionFilter` (registered globally in `AddControllers`) maps only the two job-domain conflicts to 409 `application/problem+json` (see Job locking below); all other exceptions are untouched.

**Table actions**: Every row-level action cell uses the shared `table-actions` class from `wwwroot/css/app.css`. It is an `inline-flex` non-wrapping control group, so Edit/Delete/Copy buttons remain side by side and do not increase table-row height.

**Job title**: The job-editor page uses the `job-title` class to keep long job names at a compact, readable heading size without changing the larger dashboard hero typography.

**Overview**: The root page uses `OverviewService` to show recently created jobs, headline planning counts, and the five stock configurations most frequently specified across job stock. The stock ranking is a planning-demand signal (distinct jobs configured for a material/length), not a count of on-hand inventory.

**Material list semantics**: The Results tab labels lengths not covered by the job's configured stock as a **Material List**, not a purchase list. It identifies required material; purchasing remains a separate decision outside the cut-list result.

### CutList.Mcp — MCP Server

Stdio-transport MCP server (`ModelContextProtocol` SDK) exposing CutList.Web's REST API as tools for Claude Code. Registers tools via `WithToolsFromAssembly`; logging is disabled entirely so it doesn't interfere with the stdio transport.

- `ApiClient.cs` — typed `HttpClient` wrapper for CutList.Web's REST API (`BaseAddress` hardcoded to `http://localhost:5270`). Job mutations use `EnsureJobSuccessAsync`: a `job_locked`/`job_changed` problem response becomes an `HttpRequestException` with the server's detail and 409 status; other failures keep the generic status error. Duplicate material/stock 409s still raise `ApiConflictException`.
- `JobTools.cs` — job CRUD, parts/stock, optimization (`OptimizeJob`), cutting tools. `list_jobs`/`get_job` expose `IsLocked`/`LockedAt`; mutation tools return `Success = false` with the lock explanation; `add_job_parts` stops at the first job conflict and reports how many parts were really added. `optimize_job` is a non-persisting preview (works on locked jobs). There is deliberately no lock/unlock tool and no automatic retry. `optimize_job` accepts an optional `engine` id and reports `EngineId`/`EngineName`; `list_packing_engines` returns the server's engine list (`GET /api/packing/engines`).
- `InventoryTools.cs` — materials, stock items (`add_stock`, etc.)
- `CutListTools.cs` — `create_cutlist` / `create_cutlist_report`: pack in-process with CutList.Core's built-in engine catalog (optional `strategy` = engine id; default is the built-in default, not CutList.Web's `Packing:DefaultEngine`; unknown ids return an error listing valid ids; results report `EngineId` and `EngineName`, which notes a fallback), plus shared conversion helpers
- `Models.cs` — shared DTOs distinct from CutList.Web's own DTOs (kept intentionally thin for MCP tool responses)

## CutList.Web Entities

### Material
- `Shape` (MaterialShape enum): Round Bar, Round Tube, Flat Bar, Square Bar, Square Tube, Rectangular Tube, Angle, Channel, I-Beam, Pipe
- `Type` (MaterialType enum): Steel, Aluminum, Stainless, Brass, Copper
- `Grade` (string?), `Size` (string), `Description` (string?), `IsActive` (bool), `SortOrder` (int)
- **DisplayName**: "{Shape} - {Size}"
- **Relationships**: `Dimensions` (1:1 MaterialDimensions), `StockItems` (1:many), `JobParts` (1:many)

### MaterialDimensions (TPC Inheritance)
Abstract base with TPC (Table Per Concrete type) mapping — each shape gets its own standalone table (`DimAngle`, `DimChannel`, `DimFlatBar`, `DimIBeam`, `DimPipe`, `DimRectangularTube`, `DimRoundBar`, `DimRoundTube`, `DimSquareBar`, `DimSquareTube`) with no base table. Each table has its own `Id` (shared sequence) and `MaterialId` FK. Each generates its own `SizeString` and `SortOrder`.

### StockItem
- `MaterialId`, `LengthInches` (decimal), `IsActive`
- **Unique constraint**: (MaterialId, LengthInches)
- **Relationships**: `Material`
- No quantity tracking — a StockItem represents a length of material you can cut from, not a counted inventory record

### CuttingTool
- `Name`, `KerfInches` (decimal), `IsDefault` (bool), `IsActive`
- **Seeded**: Bandsaw (0.0625"), Chop Saw (0.125"), Cold Cut Saw (0.0625"), Hacksaw (0.0625")

### Job
- `JobNumber` (auto-generated "JOB-#####", unique), `Name`, `Customer`, `CuttingToolId`, `Notes`
- `LockedAt` (DateTime?, UTC) — set when materials ordered; `IsLocked` computed property. Configured as an EF **concurrency token** (metadata-only migration `JobLockedAtConcurrencyToken`), so job UPDATE/DELETE statements carry `WHERE LockedAt = <value read>`
- `OptimizationResultJson` (string?, nvarchar(max)) — serialized optimization results
- `OptimizedAt` (DateTime?) — when optimization was last run
- **Relationships**: `Parts` (1:many JobPart), `Stock` (1:many JobStock), `CuttingTool`

### JobPart
- `JobId`, `MaterialId`, `Name`, `LengthInches` (decimal), `Quantity` (int), `SortOrder`

### JobStock
- `JobId`, `MaterialId`, `StockItemId?`, `LengthInches`, `Quantity` (-1 = unlimited), `IsCustomLength`, `Priority` (lower = used first), `SortOrder`

## CutList.Web Services

### MaterialService
- CRUD with soft delete, dimension management (`CreateWithDimensionsAsync`, `UpdateWithDimensionsAsync`)
- Search by dimension (e.g., `SearchRoundBarByDiameterAsync`, `SearchAngleByLegAsync`) with tolerance
- `CreateDimensionsForShape(shape)` factory method

### StockItemService
- CRUD with soft delete

### JobService
- Job CRUD: `CreateAsync` (auto-generates JobNumber), `DuplicateAsync` (deep copy into a new unlocked job without saved results), `QuickCreateAsync`
- `UpdateAsync(job)` reloads the persisted job and copies only `Name`, `Customer`, `CuttingToolId`, `Notes` (never lock state, ids, timestamps, results, or child/navigation collections from the caller's object); clears optimization results
- Lock/Unlock: `LockAsync(id)` (idempotent; keeps the original lock time), `UnlockAsync(id)` (keeps the saved result) — the only lock transitions; neither invalidates results
- Parts: `AddPartAsync`, `UpdatePartAsync`, `DeletePartAsync`; Stock: `AddStockAsync`, `UpdateStockAsync`, `DeleteStockAsync` — load the stored child and its persisted owner, refuse to move a child to another job (`ArgumentException`), copy only editable scalars, and save the child together with the parent timestamp + result invalidation in one `SaveChanges`. Add methods copy the generated `Id`/`SortOrder` back to the caller's object
- Optimization: `SaveOptimizationResultAsync`, `ClearOptimizationResultAsync` (do not change `UpdatedAt`)
- Missing resources: add/update with a missing job or child throws `KeyNotFoundException`; delete/lock/unlock/save/clear of a missing row is a no-op (controllers return 404 before calling)
- Cutting tools: full CRUD with single-default enforcement

### CutListPackingService
- `PackAsync(parts, kerfInches, jobStock?, engineId?)` — runs optimization per material group with the requested engine (null = configured default); an unknown id throws `UnknownPackingEngineException` before any database work
- `Engines` / `DefaultEngine` expose the engine catalog for selection UIs
- Results record `EngineId`/`EngineName` (the name notes an engine fallback in any material group); `SavedOptimizationResult` persists them inside `OptimizationResultJson` (no schema change). Results saved before engine selection have no engine fields and load as First Fit, the only engine the job path used then. Plans saved under the retired id `advanced` (the same algorithm) keep their stored engine name; the Results-tab picker then starts on the configured default
- Separates results into `InStockBins` (from catalog-sourced job stock) and `ToBePurchasedBins`
- `GetSummary(result)` — calculates total bins, pieces, waste, efficiency %
- `SerializeResult(result)` / `LoadSavedResult(json)` — JSON round-trip via DTO layer (`SavedOptimizationResult` etc.)

### ReportService
- `FormatLength(inches)`, `GroupItems(items)` for print report formatting

### CatalogService
- `ExportAsync()` — dumps cutting tools and materials (with dimensions + stock items) into a shape-grouped `CatalogData` DTO for bulk export/import tooling
- Backs the `CatalogController` REST endpoint and the `scripts/ExportData` data-loading workflow

## CutList.Web Pages

| Route | Page | Purpose |
|-------|------|---------|
| `/` | Home | Welcome page with feature cards and workflow guide |
| `/jobs` | Jobs/Index | Job list with pagination, lock icons, Quick Create, Duplicate, Delete |
| `/jobs/new` | Jobs/Edit | New job form (details only) |
| `/jobs/{Id}` | Jobs/Edit | Tabbed editor (Details, Parts, Stock, Results); locked jobs show banner + disable editing; Results tab has an engine picker (defaults to the saved plan's engine, else the configured default) and shows which engine produced the plan |
| `/materials` | Materials/Index | Material list with MaterialFilter, pagination |
| `/materials/new`, `/materials/{Id}` | Materials/Edit | Material + dimension form (varies by shape) |
| `/stock` | Stock/Index | Stock items with MaterialFilter, pagination |
| `/stock/new`, `/stock/{Id}` | Stock/Edit | Stock item form |
| `/tools` | Tools/Index | Cutting tools CRUD |
| `/Error` | Error | Unhandled exception page (registered via `UseExceptionHandler`) |

## Shared Components

| Component | Purpose |
|-----------|---------|
| `ConfirmDialog` | Modal confirmation for destructive actions (Show/Hide methods, OnConfirm callback) |
| `LengthInput` | Architectural unit input — parses "12'", "6\"", "12 1/2\""; reformats on blur; two-way binding via `Value` or `NullableValue` |
| `Pager` | Pagination with "Showing X-Y of Z", prev/next, smart page window with ellipsis |
| `MaterialFilter` | Reusable filter: Shape, Type, Grade dropdowns + search text; used on Materials, Stock pages |

## Key Patterns & Conventions

- **Nullable reference types enabled** — handle nulls explicitly
- **Soft deletes** — Materials, StockItems, CuttingTools use `IsActive` flag
- **Job locking** — `LockedAt` timestamp set via a manual Lock Job action (always available, regardless of whether the job needs purchases). **Enforcement lives in `JobService`**, not the UI: `UpdateAsync`, `DeleteAsync`, the six part/stock methods, `SaveOptimizationResultAsync` and `ClearOptimizationResultAsync` throw `JobLockedException` (`JobId`, persisted `LockedAt`) for a locked job, even for no-op requests and stale/forged caller objects. Allowed while locked: reads, printing, `DuplicateAsync`, `LockAsync`/`UnlockAsync`, `/api/jobs/{id}/pack` and `/api/packing/optimize` previews (they never persist), and global catalog/tool maintenance. Every guarded mutation reads the parent in its own context and forces the parent UPDATE (or DELETE) into the same save, so a lock committed between read and save fails the `LockedAt` concurrency check and rolls the whole save back; the service then re-reads and throws `JobLockedException`, or `JobMutationConflictException` if the job is not locked (e.g. deleted). Nothing is retried. Concurrent same-direction lock/unlock is idempotent; an unlock never overwrites a newer lock. This guards lock state only — it is not general optimistic versioning of unlocked edits, and separate sequential requests (e.g. batch adds) are not atomic as a group
- **Lock conflicts over HTTP** — 409 `application/problem+json` with `title`, `detail`, `code` (`job_locked` or `job_changed`), `jobId`, and for `job_locked` the persisted UTC `lockedAt`. 404/400 behavior is unchanged. `JobDto`/`JobDetailDto` responses include read-only `IsLocked` and `LockedAt`; request DTOs cannot set lock state
- **Lock conflicts in Blazor** — the Edit page disables/hides controls for locked jobs (presentation only) and routes every persisted change through `TryJobChangeAsync`: on a conflict it closes stale modals, reloads the persisted job and saved result, and shows `#job-conflict-alert`; row/import loops stop and report rows actually saved. `RunOptimization` publishes a new plan only after it is saved. The Jobs index disables Delete for locked rows (Copy stays available) and reports a delete rejected after the confirmation opened
- **Pagination** — All list pages use `Pager` with `pageSize = 25`
- **ConfirmDialog** — All destructive actions use the shared `ConfirmDialog` component
- **Material selection flow** — Shape dropdown -> Size dropdown -> Length input -> Quantity (conditional dropdowns)
- **Stock priority** — Lower number = used first; `-1` quantity = unlimited
- **Job stock** — Jobs must have stock explicitly configured (catalog-sourced `StockItem` rows or custom-length rows); there is no fallback to auto-discovered inventory
- **Optimization persistence** — Results saved as JSON in `Job.OptimizationResultJson`, including the engine used; DTO layer (`SavedOptimizationResult` etc.) handles serialization since Core types use encapsulated collections; results auto-cleared when parts, stock, or cutting tool change
- **Job lock flow** — Optimize job -> review/print results -> Lock Job (manual action beside Print Report on the Results tab, available whether or not purchases are needed) -> job becomes read-only until Unlock
- **Printed cut badges** — Print styles intentionally remove color fills; cut badges therefore force black text and a black part-number/length divider so both remain legible on paper. The print-only tool line shows the selected cut method and kerf immediately below the summary.
- **Timestamps** — `CreatedAt` defaults to `GETUTCDATE()`; `UpdatedAt` set on modifications
- **Collections** — Encapsulated in Core; use `AsReadOnly()`, access via `Add*` methods
- **Priority system** — Lower priority bins used first in packing algorithm
- **UI ↔ MCP split** — The Blazor UI calls services directly (in-process); CutList.Mcp and any other external integration go through the REST API in `Controllers/`. Keep both paths in sync when changing service method signatures used by controllers.

## Supporting Scripts (`scripts/`)

- `ExportData/` — standalone console project that exercises `CutList.Web`'s data layer to import/export catalog seed data (e.g. `Data/SeedData/oneals-catalog.json`)

## Key Files

| File | Purpose |
|------|---------|
| `CutList.Core/Nesting/FirstFitEngine.cs` | Default 1D bin packing algorithm (first-fit decreasing) |
| `CutList.Core/Nesting/MultiBinPacker.cs` | Multi-bin type orchestration |
| `CutList.Core/Nesting/BuiltInPackingEngines.cs` | The list of selectable packing engines |
| `CutList.Core/Nesting/PackingEngineCatalog.cs` | Engine id resolution and creation |
| `CutList.Core/ArchUnits.cs` | Architectural unit parsing/conversion |
| `CutList.Core/Formatting/FormatHelper.cs` | Display formatting |
| `CutList.Web/Data/ApplicationDbContext.cs` | EF Core context with all DbSets and configuration |
| `CutList.Web/Services/JobService.cs` | Job orchestration (CRUD, parts, stock, tools, lock/unlock) and lock enforcement |
| `CutList.Web/Controllers/JobMutationExceptionFilter.cs` | Maps `JobLockedException`/`JobMutationConflictException` to 409 problem responses |
| `CutList.Web.Tests/Infrastructure/` | Testcontainers SQL Server fixture (real migrations, never the production DB), `WebApplicationFactory` host, whole-DB `JobSnapshot`, `MutationSaveGate` (deterministic lock-before-save races), `SqlCommandRecorder` (SQL capture / rollback fault injection) |
| `CutList.Web/Services/CutListPackingService.cs` | Bridges web entities to Core packing engine |
| `CutList.Web/Components/Pages/Jobs/Edit.razor` | Job editor (tabbed: Details, Parts, Stock, Results) |
| `CutList.Mcp/ApiClient.cs` | HTTP client the MCP server uses to call CutList.Web's REST API |
| `CutList.Mcp/Program.cs` | MCP server entrypoint (stdio transport, tool registration) |
| `CutList/Presenters/MainFormPresenter.cs` | WinForms business logic orchestrator |
