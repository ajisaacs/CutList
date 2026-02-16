# CLAUDE.md

> **IMPORTANT**: Always keep this document updated when functionality changes, entities are added/modified, new pages or services are created, or architectural patterns evolve.

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CutList is a 1D bin packing optimization application that helps users optimize material cutting. It calculates efficient bin packing solutions to minimize waste when cutting stock materials into required parts.

The solution contains three projects:

| Project | Framework | Purpose |
|---------|-----------|---------|
| **CutList** | .NET 8.0 Windows Forms | Original desktop UI (MVP pattern) |
| **CutList.Core** | .NET 8.0 Class Library | Domain models and packing algorithms (platform-agnostic) |
| **CutList.Web** | .NET 8.0 Blazor Server | Web-based UI with EF Core + SQL Server |

**Key Dependencies**: Math-Expression-Evaluator (input parsing), Newtonsoft.Json (serialization), Entity Framework Core (data access), Bootstrap 5 + Bootstrap Icons (UI)

## Build Commands

```bash
# Build entire solution
dotnet build CutList.sln

# Build specific projects
dotnet build CutList/CutList.csproj
dotnet build CutList.Core/CutList.Core.csproj
dotnet build CutList.Web/CutList.Web.csproj

# Run applications
dotnet run --project CutList/CutList.csproj        # WinForms
dotnet run --project CutList.Web/CutList.Web.csproj # Blazor

# EF Core migrations (always apply immediately after creating)
dotnet ef migrations add <Name> --project CutList.Web
dotnet ef database update --project CutList.Web

# Clean build
dotnet clean CutList.sln
```

## Architecture

### CutList.Core — Domain & Algorithms

**Key Domain Models**:
- **BinItem**: Item to be packed (label, length)
- **MultiBin**: Stock bin type with length, quantity (-1 = unlimited), priority
- **Bin**: Packed bin containing items, tracks remaining length
- **Tool**: Cutting tool with kerf/blade width

**Packing Engine**:
- `MultiBinEngine` coordinates packing across bin types
- `AdvancedFitEngine` performs 1D bin packing (first-fit decreasing with optimization)
- `PackingStrategy.AdvancedFit` is the standard strategy

**Unit Handling**:
- `ArchUnits` — Converts feet/inches/fractions to decimal inches (accepts "12'", "6\"", "12 1/2\"", etc.)
- `FormatHelper` — Converts decimals to mixed fractions for display
- Internal calculations use inches; format on display

**Patterns**:
- `Result<T>` for standardized error handling (Success/Failure instead of exceptions)
- `IEngineFactory` for swappable algorithm implementations
- Lower priority number = used first in bin selection

### CutList (WinForms) — Desktop UI

- MVP pattern: `MainForm` implements `IMainView`, `MainFormPresenter` orchestrates logic
- `Document` holds application state
- `CutListService` bridges UI models to core packing algorithms
- `DocumentService` handles JSON file persistence

### CutList.Web (Blazor Server) — Web UI

**Database**: SQL Server via Entity Framework Core (connection string: `DefaultConnection`)

**Service Registration** (Program.cs): All services registered as Scoped — MaterialService, SupplierService, StockItemService, JobService, CutListPackingService, ReportService, PurchaseItemService

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
- `MaterialId`, `LengthInches` (decimal), `QuantityOnHand` (int), `IsActive`
- **Unique constraint**: (MaterialId, LengthInches)
- **Relationships**: `Material`, `SupplierOfferings` (1:many), `Transactions` (1:many StockTransaction)

### StockTransaction
- `StockItemId`, `Quantity` (signed delta), `Type` (Received/Used/Adjustment/Scrapped/Returned)
- Optional: `JobId`, `SupplierId`, `UnitPrice`

### Supplier
- `Name` (required), `ContactInfo`, `Notes`, `IsActive`
- **Relationships**: `Offerings` (1:many SupplierOffering)

### SupplierOffering
- Links Supplier to StockItem with optional `PartNumber`, `Price`, `Notes`
- **Unique constraint**: (SupplierId, StockItemId)

### CuttingTool
- `Name`, `KerfInches` (decimal), `IsDefault` (bool), `IsActive`
- **Seeded**: Bandsaw (0.0625"), Chop Saw (0.125"), Cold Cut Saw (0.0625"), Hacksaw (0.0625")

### Job
- `JobNumber` (auto-generated "JOB-#####", unique), `Name`, `Customer`, `CuttingToolId`, `Notes`
- `LockedAt` (DateTime?) — set when materials ordered; `IsLocked` computed property
- `OptimizationResultJson` (string?, nvarchar(max)) — serialized optimization results
- `OptimizedAt` (DateTime?) — when optimization was last run
- **Relationships**: `Parts` (1:many JobPart), `Stock` (1:many JobStock), `CuttingTool`

### JobPart
- `JobId`, `MaterialId`, `Name`, `LengthInches` (decimal), `Quantity` (int), `SortOrder`

### JobStock
- `JobId`, `MaterialId`, `StockItemId?`, `LengthInches`, `Quantity` (-1 = unlimited), `IsCustomLength`, `Priority` (lower = used first), `SortOrder`

### PurchaseItem
- `StockItemId`, `SupplierId?`, `JobId?`, `Quantity`, `Status` (Pending/Ordered/Received), `Notes`

## CutList.Web Services

### MaterialService
- CRUD with soft delete, dimension management (`CreateWithDimensionsAsync`, `UpdateWithDimensionsAsync`)
- Search by dimension (e.g., `SearchRoundBarByDiameterAsync`, `SearchAngleByLegAsync`) with tolerance
- `CreateDimensionsForShape(shape)` factory method

### StockItemService
- CRUD with soft delete
- Stock transactions: `AddStockAsync`, `UseStockAsync`, `AdjustStockAsync`, `ScrapStockAsync`
- `GetTransactionHistoryAsync`, `RecalculateQuantityAsync`
- Pricing: `GetAverageCostAsync`, `GetLastPurchasePriceAsync`

### SupplierService
- CRUD for suppliers and offerings
- `GetOfferingsForStockItemAsync` — all supplier options for a stock item

### JobService
- Job CRUD: `CreateAsync` (auto-generates JobNumber), `DuplicateAsync` (deep copy), `QuickCreateAsync`
- Lock/Unlock: `LockAsync(id)`, `UnlockAsync(id)` — controls job editability after ordering
- Parts: `AddPartAsync`, `UpdatePartAsync`, `DeletePartAsync` (all update job timestamp + clear optimization results)
- Stock: `AddStockAsync`, `UpdateStockAsync`, `DeleteStockAsync` (all clear optimization results)
- Optimization: `SaveOptimizationResultAsync`, `ClearOptimizationResultAsync`
- Cutting tools: full CRUD with single-default enforcement

### CutListPackingService
- `PackAsync(parts, kerfInches, jobStock?)` — runs optimization per material group
- Separates results into `InStockBins` (from inventory) and `ToBePurchasedBins`
- `GetSummary(result)` — calculates total bins, pieces, waste, efficiency %
- `SerializeResult(result)` / `LoadSavedResult(json)` — JSON round-trip via DTO layer (`SavedOptimizationResult` etc.)

### PurchaseItemService
- CRUD + `CreateBulkAsync` for batch creation from optimization results
- `UpdateStatusAsync(id, status)`, `UpdateSupplierAsync(id, supplierId)`

### ReportService
- `FormatLength(inches)`, `GroupItems(items)` for print report formatting

## CutList.Web Pages

| Route | Page | Purpose |
|-------|------|---------|
| `/` | Home | Welcome page with feature cards and workflow guide |
| `/jobs` | Jobs/Index | Job list with pagination, lock icons, Quick Create, Duplicate, Delete |
| `/jobs/new` | Jobs/Edit | New job form (details only) |
| `/jobs/{Id}` | Jobs/Edit | Tabbed editor (Details, Parts, Stock, Results); locked jobs show banner + disable editing |
| `/materials` | Materials/Index | Material list with MaterialFilter, pagination |
| `/materials/new`, `/materials/{Id}` | Materials/Edit | Material + dimension form (varies by shape) |
| `/stock` | Stock/Index | Stock items with MaterialFilter, quantity badges |
| `/stock/new`, `/stock/{Id}` | Stock/Edit | Stock item form |
| `/orders` | Orders/Index | Tabbed (Pending/Ordered/All), supplier assignment, status transitions |
| `/orders/add` | Orders/Add | Manual purchase item creation |
| `/suppliers` | Suppliers/Index | Supplier list with CRUD |
| `/suppliers/{Id}` | Suppliers/Edit | Supplier + offerings management |
| `/tools` | Tools/Index | Cutting tools CRUD |

## Shared Components

| Component | Purpose |
|-----------|---------|
| `ConfirmDialog` | Modal confirmation for destructive actions (Show/Hide methods, OnConfirm callback) |
| `LengthInput` | Architectural unit input — parses "12'", "6\"", "12 1/2\""; reformats on blur; two-way binding via `Value` or `NullableValue` |
| `Pager` | Pagination with "Showing X-Y of Z", prev/next, smart page window with ellipsis |
| `MaterialFilter` | Reusable filter: Shape, Type, Grade dropdowns + search text; used on Materials, Stock, Orders pages |

## Key Patterns & Conventions

- **Nullable reference types enabled** — handle nulls explicitly
- **Soft deletes** — Materials, Suppliers, StockItems, CuttingTools use `IsActive` flag
- **Job locking** — `LockedAt` timestamp set when materials ordered; Edit page disables all modification via `<fieldset disabled>`, hides add/edit/delete buttons; Unlock button to re-enable editing
- **Pagination** — All list pages use `Pager` with `pageSize = 25`
- **ConfirmDialog** — All destructive actions use the shared `ConfirmDialog` component
- **Material selection flow** — Shape dropdown -> Size dropdown -> Length input -> Quantity (conditional dropdowns)
- **Stock priority** — Lower number = used first; `-1` quantity = unlimited
- **Job stock** — Jobs can use auto-discovered inventory OR define custom stock lengths
- **Optimization persistence** — Results saved as JSON in `Job.OptimizationResultJson`; DTO layer (`SavedOptimizationResult` etc.) handles serialization since Core types use encapsulated collections; results auto-cleared when parts, stock, or cutting tool change
- **Purchase flow** — Optimize job -> "Add to Order List" creates PurchaseItems + locks job -> Orders page manages status (Pending -> Ordered -> Received)
- **Timestamps** — `CreatedAt` defaults to `GETUTCDATE()`; `UpdatedAt` set on modifications
- **Collections** — Encapsulated in Core; use `AsReadOnly()`, access via `Add*` methods
- **Priority system** — Lower priority bins used first in packing algorithm

## Key Files

| File | Purpose |
|------|---------|
| `CutList.Core/Nesting/AdvancedFitEngine.cs` | Core 1D bin packing algorithm |
| `CutList.Core/Nesting/MultiBinEngine.cs` | Multi-bin type orchestration |
| `CutList.Core/ArchUnits.cs` | Architectural unit parsing/conversion |
| `CutList.Core/Formatting/FormatHelper.cs` | Display formatting |
| `CutList.Web/Data/ApplicationDbContext.cs` | EF Core context with all DbSets and configuration |
| `CutList.Web/Services/JobService.cs` | Job orchestration (CRUD, parts, stock, tools, lock/unlock) |
| `CutList.Web/Services/CutListPackingService.cs` | Bridges web entities to Core packing engine |
| `CutList.Web/Components/Pages/Jobs/Edit.razor` | Job editor (tabbed: Details, Parts, Stock, Results) |
| `CutList/Presenters/MainFormPresenter.cs` | WinForms business logic orchestrator |
