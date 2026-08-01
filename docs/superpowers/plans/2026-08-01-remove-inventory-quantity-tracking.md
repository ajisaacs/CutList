# Remove Inventory Quantity Tracking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove `StockItem.QuantityOnHand` and the entire `StockTransaction` ledger from CutList so a job's stock is always exactly what the user explicitly enters (with `-1` meaning unlimited, everywhere that option exists) — no disconnected inventory-quantity system for users to think about.

**Architecture:** This is a subtractive refactor across every layer that touches `StockItem`/`StockTransaction`: entities, EF Core config + migration, services, controllers/DTOs, the packing engine's now-removed auto-discovery fallback, the Blazor UI, and the MCP server. Work proceeds "leaves before root" — UI and API surface are trimmed first while the underlying field/entity still exist (so every intermediate task still compiles), and the actual `StockItem.QuantityOnHand` column drop / `StockTransaction` table drop happens last, once nothing references it.

**Tech Stack:** .NET 8 Blazor Server (CutList.Web), .NET 8 EF Core + SQL Server, .NET 10 MCP stdio server (CutList.Mcp).

## Global Constraints

- No automated test suite exists in this repo today (confirmed during spec review). Verification for every task is: the solution builds clean, `get_diagnostics` on the touched project(s) shows no new errors, and — for behavior-affecting tasks — a manual pass through the running app (per the project's own guidance: "For UI or frontend changes, start the dev server and use the feature in a browser before reporting the task as complete").
- Per user's global CLAUDE.md: use Roslyn Bridge MCP tools (`mcp__roslyn-bridge__*`) as the primary method for C# code exploration/diagnostics rather than Glob/Grep where a Roslyn Bridge tool fits.
- Per project CLAUDE.md: after creating an EF Core migration, apply it immediately with `dotnet ef database update --project CutList.Web` — do not leave it pending.
- Per project CLAUDE.md: CutList.Mcp is republished after any change, to `~/.claude/mcp/CutList.Mcp/`, via `dotnet publish CutList.Mcp/CutList.Mcp.csproj -c Release -o "$USERPROFILE/.claude/mcp/CutList.Mcp"`.
- Commit after each task with a message describing why, not just what (repo convention — see recent commits like `b77d0d9`, `f75c205`).

---

### Task 1: Strip QuantityOnHand display from the Blazor UI

**Files:**
- Modify: `CutList.Web/Components/Pages/Stock/Index.razor`
- Modify: `CutList.Web/Components/Pages/Stock/Edit.razor`
- Modify: `CutList.Web/Components/Pages/Jobs/Edit.razor`

**Interfaces:**
- Consumes: `StockItem.QuantityOnHand` (still present on the entity at this point — removed in Task 6). `StockItemService.AddStockAsync/AdjustStockAsync/ScrapStockAsync/GetTransactionHistoryAsync` (still present — removed in Task 2).
- Produces: nothing new; this task only deletes UI surface. No other task depends on what this one produces.

This task only removes *display and interaction* of quantity/transactions from the UI. The underlying service methods and entity field are removed in later tasks — leaving them in place for now means the app still builds and runs after this task, even though the "Add/Adjust Stock" UI is now gone.

- [ ] **Step 1: Remove the "On Hand" column from `/stock` (Index)**

In `CutList.Web/Components/Pages/Stock/Index.razor`, remove the `<th>On Hand</th>` header and its matching `<td>` cell:

```razor
                    <th>Length</th>
                    <th>On Hand</th>
                    <th style="width: 100px;">Actions</th>
```
becomes:
```razor
                    <th>Length</th>
                    <th style="width: 100px;">Actions</th>
```

and:
```razor
                        <td>@ArchUnits.FormatFromInches((double)item.LengthInches)</td>
                        <td>
                            @if (item.QuantityOnHand > 0)
                            {
                                <span class="badge bg-success">@item.QuantityOnHand</span>
                            }
                            else
                            {
                                <span class="badge bg-secondary">0</span>
                            }
                        </td>
                        <td>
```
becomes:
```razor
                        <td>@ArchUnits.FormatFromInches((double)item.LengthInches)</td>
                        <td>
```

Also update the intro paragraph — it currently says stock items track "how many pieces you have on hand":
```razor
<p class="text-muted mb-4">
    Stock items represent the specific lengths of material you have available for cutting. Each stock item links
    a material to a length and tracks how many pieces you have on hand.
</p>
```
becomes:
```razor
<p class="text-muted mb-4">
    Stock items represent the specific lengths of material you can cut from. Add the lengths you typically
    work with here, then pick from them when adding stock to a job.
</p>
```

- [ ] **Step 2: Replace `Stock/Edit.razor` with the quantity/transaction UI removed**

Replace the full contents of `CutList.Web/Components/Pages/Stock/Edit.razor` with:

```razor
@page "/stock/new"
@page "/stock/{Id:int}"
@inject StockItemService StockItemService
@inject MaterialService MaterialService
@inject NavigationManager Navigation
@using CutList.Core.Formatting

<PageTitle>@(IsNew ? "Add Stock Item" : "Edit Stock Item")</PageTitle>

<h1>@(IsNew ? "Add Stock Item" : $"{stockItem.Material?.DisplayName} - {ArchUnits.FormatFromInches((double)stockItem.LengthInches)}")</h1>

@if (loading)
{
    <p><em>Loading...</em></p>
}
else
{
    <div class="row">
        <div class="col-lg-6 mb-4">
            <div class="card">
                <div class="card-header">
                    <h5 class="mb-0">Stock Item Details</h5>
                </div>
                <div class="card-body">
                    <EditForm Model="stockItem" OnValidSubmit="SaveStockItemAsync">
                        <DataAnnotationsValidator />

                        <div class="mb-3">
                            <label class="form-label">Material</label>
                            <select class="form-select" @bind="stockItem.MaterialId" disabled="@(!IsNew)">
                                <option value="0">-- Select Material --</option>
                                @foreach (var material in materials)
                                {
                                    <option value="@material.Id">@material.DisplayName</option>
                                }
                            </select>
                        </div>

                        <div class="mb-3">
                            <label class="form-label">Length</label>
                            <LengthInput @bind-Value="stockItem.LengthInches" />
                        </div>

                        <div class="mb-3">
                            <label class="form-label">Name (optional)</label>
                            <InputText class="form-control" @bind-Value="stockItem.Name" placeholder="Custom display name" />
                        </div>

                        <div class="mb-3">
                            <label class="form-label">Notes (optional)</label>
                            <InputText class="form-control" @bind-Value="stockItem.Notes" placeholder="Internal notes" />
                        </div>

                        @if (!string.IsNullOrEmpty(errorMessage))
                        {
                            <div class="alert alert-danger">@errorMessage</div>
                        }

                        <div class="d-flex gap-2">
                            <button type="submit" class="btn btn-primary" disabled="@saving">
                                @if (saving)
                                {
                                    <span class="spinner-border spinner-border-sm me-1"></span>
                                }
                                @(IsNew ? "Create Stock Item" : "Save Changes")
                            </button>
                            <a href="stock" class="btn btn-outline-secondary">@(IsNew ? "Cancel" : "Back to List")</a>
                        </div>
                    </EditForm>
                </div>
            </div>
        </div>
    </div>
}

@code {
    [Parameter]
    public int? Id { get; set; }

    private StockItem stockItem = new();
    private List<Material> materials = new();
    private bool loading = true;
    private bool saving;
    private string? errorMessage;

    private bool IsNew => !Id.HasValue;

    protected override async Task OnInitializedAsync()
    {
        materials = await MaterialService.GetAllAsync();

        if (Id.HasValue)
        {
            var existing = await StockItemService.GetByIdAsync(Id.Value);
            if (existing == null)
            {
                Navigation.NavigateTo("stock");
                return;
            }
            stockItem = existing;
        }
        loading = false;
    }

    private async Task SaveStockItemAsync()
    {
        errorMessage = null;
        saving = true;

        try
        {
            if (stockItem.MaterialId == 0)
            {
                errorMessage = "Please select a material";
                return;
            }

            if (stockItem.LengthInches <= 0)
            {
                errorMessage = "Length must be greater than zero";
                return;
            }

            var exists = await StockItemService.ExistsAsync(
                stockItem.MaterialId,
                stockItem.LengthInches,
                IsNew ? null : stockItem.Id);

            if (exists)
            {
                errorMessage = "A stock item with this material and length already exists";
                return;
            }

            try
            {
                if (IsNew)
                {
                    var created = await StockItemService.CreateAsync(stockItem);
                    Navigation.NavigateTo($"stock/{created.Id}");
                }
                else
                {
                    await StockItemService.UpdateAsync(stockItem);
                }
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                errorMessage = "A stock item with this material and length already exists (it may have been previously deleted).";
            }
        }
        finally
        {
            saving = false;
        }
    }
}
```

Notes on what changed: dropped the `@using CutList.Web.Data.Entities` (no longer needed — `StockTransaction`/`StockTransactionType` aren't referenced), dropped the entire right-hand "Inventory" card and its `@code` support (`transactions`, `showStockForm`, `savingStockTransaction`, `stockTransactionType`, `stockQuantity`, `stockNotes`, `stockFormErrorMessage`, `GetTransactionBadgeClass`, `ShowStockForm`, `CancelStockForm`, `SaveStockTransactionAsync`), and dropped the `transactions = await StockItemService.GetTransactionHistoryAsync(...)` call from `OnInitializedAsync`.

- [ ] **Step 3: Remove QuantityOnHand references from `Jobs/Edit.razor`**

In the "Import from Inventory" candidates table, remove the "On Hand" column:
```razor
                                <thead>
                                    <tr>
                                        <th style="width: 40px;"></th>
                                        <th>Length</th>
                                        <th>On Hand</th>
                                        <th style="width: 120px;">Qty to Use</th>
                                        <th style="width: 100px;">Priority</th>
                                    </tr>
                                </thead>
```
becomes:
```razor
                                <thead>
                                    <tr>
                                        <th style="width: 40px;"></th>
                                        <th>Length</th>
                                        <th style="width: 120px;">Qty to Use</th>
                                        <th style="width: 100px;">Priority</th>
                                    </tr>
                                </thead>
```

and remove the matching data cell:
```razor
                                            <td>@ArchUnits.FormatFromInches((double)candidate.StockItem.LengthInches)</td>
                                            <td>@candidate.StockItem.QuantityOnHand</td>
                                            <td>
```
becomes:
```razor
                                            <td>@ArchUnits.FormatFromInches((double)candidate.StockItem.LengthInches)</td>
                                            <td>
```

In the "Add Stock from Inventory" length dropdown, drop the "(N available)" suffix:
```razor
                            <option value="@stock.Id">@ArchUnits.FormatFromInches((double)stock.LengthInches) (@stock.QuantityOnHand available)</option>
```
becomes:
```razor
                            <option value="@stock.Id">@ArchUnits.FormatFromInches((double)stock.LengthInches)</option>
```

- [ ] **Step 4: Build and check diagnostics**

Run:
```bash
mcp__RoslynBridge__get_diagnostics_summary  # for CutList.Web
dotnet build CutList.Web/CutList.Web.csproj
```
Expected: build succeeds with 0 errors (unused-using warnings for `CutList.Web.Data.Entities` in `Stock/Edit.razor`, if any, are fine — this repo doesn't treat warnings as errors).

- [ ] **Step 5: Manual check**

Start the app (`dotnet run --project CutList.Web/CutList.Web.csproj`), open `/stock`, confirm the list renders without an On Hand column and the intro copy reads correctly; open an existing stock item's edit page and confirm there's no Inventory card; open a job's Stock tab and confirm the "Add Stock from Inventory" dropdown and "Import from Inventory" modal both render without quantity figures.

- [ ] **Step 6: Commit**

```bash
git add CutList.Web/Components/Pages/Stock/Index.razor CutList.Web/Components/Pages/Stock/Edit.razor CutList.Web/Components/Pages/Jobs/Edit.razor
git commit -m "$(cat <<'EOF'
refactor: remove QuantityOnHand display from stock and job UI

First step of removing inventory quantity tracking (see
docs/superpowers/specs/2026-08-01-remove-inventory-quantity-tracking-design.md).
UI no longer shows on-hand counts or offers transaction entry; the
underlying field/service methods are removed in follow-up commits.
EOF
)"
```

---

### Task 2: Remove stock-transaction service methods, controller endpoints, and DTOs

**Files:**
- Modify: `CutList.Web/Services/StockItemService.cs`
- Modify: `CutList.Web/Controllers/StockItemsController.cs`
- Modify: `CutList.Web/Controllers/JobsController.cs:297-316`
- Modify: `CutList.Web/DTOs/StockItemDtos.cs`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `StockItemDto` with no `QuantityOnHand` property, `CreateStockItemDto` with no `QuantityOnHand` property — Task 5 (MCP client) must match this shape.

- [ ] **Step 1: Remove transaction methods from `StockItemService`**

In `CutList.Web/Services/StockItemService.cs`, delete the entire `// Stock transaction methods` region — everything from `public async Task<StockTransaction> AddStockAsync(...)` through the end of `RecalculateQuantityAsync`, i.e. delete lines 112-253 (from the `// Stock transaction methods` comment through the closing brace of `RecalculateQuantityAsync`), leaving the file ending after `ExistsAsync`:

```csharp
    public async Task<bool> ExistsAsync(int materialId, decimal lengthInches, int? excludeId = null)
    {
        await using var context = _factory.CreateDbContext();

        var query = context.StockItems.Where(s =>
            s.MaterialId == materialId &&
            s.LengthInches == lengthInches &&
            s.IsActive);

        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}
```

- [ ] **Step 2: Remove transaction endpoints from `StockItemsController`**

In `CutList.Web/Controllers/StockItemsController.cs`, delete these six action methods entirely: `GetTransactions` (`GET {id}/transactions`), `ReceiveStock` (`POST {id}/receive`), `UseStock` (`POST {id}/use`), `AdjustStock` (`POST {id}/adjust`), `ScrapStock` (`POST {id}/scrap`), `RecalculateStock` (`POST {id}/recalculate`) — i.e. everything between the end of `GetByMaterial` and the start of `MapToDto`.

Update `Create`, which currently sets `QuantityOnHand` on the new entity:
```csharp
        var stockItem = new StockItem
        {
            MaterialId = dto.MaterialId,
            LengthInches = (decimal)lengthInches,
            Name = dto.Name,
            QuantityOnHand = dto.QuantityOnHand,
            Notes = dto.Notes
        };
```
becomes:
```csharp
        var stockItem = new StockItem
        {
            MaterialId = dto.MaterialId,
            LengthInches = (decimal)lengthInches,
            Name = dto.Name,
            Notes = dto.Notes
        };
```

Update `MapToDto`:
```csharp
    private static StockItemDto MapToDto(StockItem s) => new()
    {
        Id = s.Id,
        MaterialId = s.MaterialId,
        MaterialName = s.Material?.DisplayName ?? string.Empty,
        LengthInches = s.LengthInches,
        LengthFormatted = ArchUnits.FormatFromInches((double)s.LengthInches),
        Name = s.Name,
        QuantityOnHand = s.QuantityOnHand,
        Notes = s.Notes,
        IsActive = s.IsActive
    };

    private static StockTransactionDto MapTransactionToDto(StockTransaction t) => new()
    {
        Id = t.Id,
        StockItemId = t.StockItemId,
        Quantity = t.Quantity,
        Type = t.Type.ToString(),
        JobId = t.JobId,
        JobNumber = t.Job?.JobNumber,
        Notes = t.Notes,
        CreatedAt = t.CreatedAt
    };
}
```
becomes:
```csharp
    private static StockItemDto MapToDto(StockItem s) => new()
    {
        Id = s.Id,
        MaterialId = s.MaterialId,
        MaterialName = s.Material?.DisplayName ?? string.Empty,
        LengthInches = s.LengthInches,
        LengthFormatted = ArchUnits.FormatFromInches((double)s.LengthInches),
        Name = s.Name,
        Notes = s.Notes,
        IsActive = s.IsActive
    };
}
```

- [ ] **Step 3: Fix `JobsController.GetAvailableStock`, which also maps `QuantityOnHand`**

In `CutList.Web/Controllers/JobsController.cs`:
```csharp
        var items = await _jobService.GetAvailableStockForMaterialAsync(materialId);
        return Ok(items.Select(s => new StockItemDto
        {
            Id = s.Id,
            MaterialId = s.MaterialId,
            MaterialName = s.Material?.DisplayName ?? string.Empty,
            LengthInches = s.LengthInches,
            LengthFormatted = ArchUnits.FormatFromInches((double)s.LengthInches),
            Name = s.Name,
            QuantityOnHand = s.QuantityOnHand,
            IsActive = s.IsActive
        }).ToList());
```
becomes:
```csharp
        var items = await _jobService.GetAvailableStockForMaterialAsync(materialId);
        return Ok(items.Select(s => new StockItemDto
        {
            Id = s.Id,
            MaterialId = s.MaterialId,
            MaterialName = s.Material?.DisplayName ?? string.Empty,
            LengthInches = s.LengthInches,
            LengthFormatted = ArchUnits.FormatFromInches((double)s.LengthInches),
            Name = s.Name,
            IsActive = s.IsActive
        }).ToList());
```

- [ ] **Step 4: Trim `StockItemDtos.cs`**

Replace the full contents of `CutList.Web/DTOs/StockItemDtos.cs` with:
```csharp
namespace CutList.Web.DTOs;

public class StockItemDto
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public class CreateStockItemDto
{
    public int MaterialId { get; set; }
    public string Length { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Notes { get; set; }
}

public class UpdateStockItemDto
{
    public string? Length { get; set; }
    public string? Name { get; set; }
    public string? Notes { get; set; }
}
```
This deletes `StockTransactionDto`, `AddStockDto`, `UseStockDto`, `AdjustStockDto`, `ScrapStockDto`.

- [ ] **Step 5: Build and check diagnostics**

Run:
```bash
mcp__RoslynBridge__get_diagnostics_summary  # for CutList.Web
dotnet build CutList.Web/CutList.Web.csproj
```
Expected: 0 errors. (`CutListPackingService` and `CatalogService` still reference `StockItem.QuantityOnHand` directly on the entity, which still exists — untouched until Tasks 3/4/6.)

- [ ] **Step 6: Manual check**

With the app running, confirm `/stock/new` still creates a stock item successfully, and Swagger (`/swagger` in Development) no longer lists the removed `stock-items` sub-routes.

- [ ] **Step 7: Commit**

```bash
git add CutList.Web/Services/StockItemService.cs CutList.Web/Controllers/StockItemsController.cs CutList.Web/Controllers/JobsController.cs CutList.Web/DTOs/StockItemDtos.cs
git commit -m "$(cat <<'EOF'
refactor: remove stock-transaction service methods, endpoints, and DTOs

No job workflow ever called these (receive/use/adjust/scrap/recalculate)
- inventory quantity tracking is being removed per
docs/superpowers/specs/2026-08-01-remove-inventory-quantity-tracking-design.md.
EOF
)"
```

---

### Task 3: Remove the packing engine's auto-discovery fallback

**Files:**
- Modify: `CutList.Web/Services/CutListPackingService.cs:64-94`

**Interfaces:**
- Consumes: `JobStock` (unchanged), `StockItem.QuantityOnHand` (still present on entity, read here for the last time before Task 6 removes it).
- Produces: `PackAsync` now requires job-specific `JobStock` rows to produce any stock bins for a material — later tasks/manual verification must account for this.

- [ ] **Step 1: Delete the fallback branch**

In `CutList.Web/Services/CutListPackingService.cs`, the `if/else` that builds `stockBins` currently reads:
```csharp
            // Check if job has specific stock configured for this material
            if (jobStockByMaterial.TryGetValue(materialId, out var materialJobStock) && materialJobStock.Count > 0)
            {
                // Use job-specific stock configuration
                foreach (var stock in materialJobStock.OrderBy(s => s.Priority))
                {
                    stockBins.Add(new StockBinSource
                    {
                        LengthInches = stock.LengthInches,
                        Quantity = stock.Quantity,
                        Priority = stock.Priority,
                        IsInStock = !stock.IsCustomLength && stock.StockItemId.HasValue
                    });
                }
            }
            else
            {
                // No job-specific stock - use all available stock items for this material
                var stockItems = await context.StockItems
                    .Where(s => s.MaterialId == materialId && s.IsActive)
                    .ToListAsync();

                foreach (var stock in stockItems)
                {
                    if (stock.QuantityOnHand > 0)
                    {
                        // In-stock with finite quantity
                        stockBins.Add(new StockBinSource
                        {
                            LengthInches = stock.LengthInches,
                            Quantity = stock.QuantityOnHand,
                            Priority = 1,
                            IsInStock = true
                        });
                    }

                    // Always add as purchasable (unlimited) - algorithm will use in-stock first due to priority
                    stockBins.Add(new StockBinSource
                    {
                        LengthInches = stock.LengthInches,
                        Quantity = -1, // unlimited
                        Priority = 2,
                        IsInStock = false
                    });
                }
            }
```
becomes:
```csharp
            // Use job-specific stock configuration. Jobs must have stock explicitly configured -
            // there is no fallback to "whatever's in inventory."
            if (jobStockByMaterial.TryGetValue(materialId, out var materialJobStock))
            {
                foreach (var stock in materialJobStock.OrderBy(s => s.Priority))
                {
                    stockBins.Add(new StockBinSource
                    {
                        LengthInches = stock.LengthInches,
                        Quantity = stock.Quantity,
                        Priority = stock.Priority,
                        IsInStock = !stock.IsCustomLength && stock.StockItemId.HasValue
                    });
                }
            }
```

Note this also drops the now-unreachable `.Count > 0` check (an empty `List<JobStock>` for a material key can't occur — `jobStockByMaterial` is built from a non-empty grouping — but keeping the plain `TryGetValue` matches the simplified intent and avoids a dead condition).

The `context` field on `PackAsync` (`await using var context = _factory.CreateDbContext();`) is still used elsewhere in the method (`await context.Materials.FirstOrDefaultAsync(...)`), so no other changes are needed in this file.

- [ ] **Step 2: Build and check diagnostics**

Run:
```bash
mcp__RoslynBridge__get_diagnostics_summary  # for CutList.Web
dotnet build CutList.Web/CutList.Web.csproj
```
Expected: 0 errors.

- [ ] **Step 3: Manual check**

With the app running: create (or reuse) a job with parts for a material, and **do not** add any Stock rows for that material, then run Optimize. Confirm all of that material's parts now show up under "Items Not Placed" instead of being silently packed from inventory. Then add explicit Stock (both a finite-quantity catalog item and an unlimited custom length) and re-optimize, confirming packing now works and respects the finite quantity as a hard ceiling.

- [ ] **Step 4: Commit**

```bash
git add CutList.Web/Services/CutListPackingService.cs
git commit -m "$(cat <<'EOF'
refactor: remove packing auto-discovery fallback

The fallback silently pulled from StockItem.QuantityOnHand and always
added an extra unlimited bin on top when a job had no stock configured
- i.e. it assumed unlimited purchasing. Per
docs/superpowers/specs/2026-08-01-remove-inventory-quantity-tracking-design.md,
a job's available stock must now be exactly what's explicitly entered.
EOF
)"
```

---

### Task 4: Remove QuantityOnHand from catalog import/export

**Files:**
- Modify: `CutList.Web/DTOs/CatalogDtos.cs:102-108`
- Modify: `CutList.Web/Services/CatalogService.cs:335-394`

**Interfaces:**
- Consumes: nothing from prior tasks.
- Produces: `CatalogStockItemDto` with no `QuantityOnHand` — existing seed JSON (`alro-catalog.json`, `oneals-catalog.json`) keeps the field in the file; `System.Text.Json` ignores unknown properties on deserialize by default, so no seed-file edits are required.

- [ ] **Step 1: Drop the field from `CatalogStockItemDto`**

In `CutList.Web/DTOs/CatalogDtos.cs`:
```csharp
public class CatalogStockItemDto
{
    public decimal LengthInches { get; set; }
    public string? Name { get; set; }
    public int QuantityOnHand { get; set; }
    public string? Notes { get; set; }
}
```
becomes:
```csharp
public class CatalogStockItemDto
{
    public decimal LengthInches { get; set; }
    public string? Name { get; set; }
    public string? Notes { get; set; }
}
```

- [ ] **Step 2: Stop reading/writing it in `CatalogService`**

In `CutList.Web/Services/CatalogService.cs`, in `ImportStockItemsAsync`:
```csharp
                    var stockItem = new StockItem
                    {
                        MaterialId = material.Id,
                        LengthInches = dto.LengthInches,
                        Name = dto.Name,
                        QuantityOnHand = dto.QuantityOnHand,
                        Notes = dto.Notes,
                        CreatedAt = DateTime.UtcNow
                    };
```
becomes:
```csharp
                    var stockItem = new StockItem
                    {
                        MaterialId = material.Id,
                        LengthInches = dto.LengthInches,
                        Name = dto.Name,
                        Notes = dto.Notes,
                        CreatedAt = DateTime.UtcNow
                    };
```

In `MapStockItems`:
```csharp
    private static List<CatalogStockItemDto> MapStockItems(Material m)
    {
        return m.StockItems.OrderBy(s => s.LengthInches).Select(s => new CatalogStockItemDto
        {
            LengthInches = s.LengthInches,
            Name = s.Name,
            QuantityOnHand = s.QuantityOnHand,
            Notes = s.Notes
        }).ToList();
    }
```
becomes:
```csharp
    private static List<CatalogStockItemDto> MapStockItems(Material m)
    {
        return m.StockItems.OrderBy(s => s.LengthInches).Select(s => new CatalogStockItemDto
        {
            LengthInches = s.LengthInches,
            Name = s.Name,
            Notes = s.Notes
        }).ToList();
    }
```

- [ ] **Step 3: Build and check diagnostics**

Run:
```bash
mcp__RoslynBridge__get_diagnostics_summary  # for CutList.Web
dotnet build CutList.Web/CutList.Web.csproj
```
Expected: 0 errors.

- [ ] **Step 4: Manual check**

If a Catalog import/export flow is easy to exercise locally (via `CatalogController`/Swagger or `scripts/ExportData`), run an export and confirm the resulting JSON's stock items no longer include `quantityOnHand`. If not convenient, the build passing plus code review of the diff is sufficient — this is a pure data-shape change with no algorithmic effect.

- [ ] **Step 5: Commit**

```bash
git add CutList.Web/DTOs/CatalogDtos.cs CutList.Web/Services/CatalogService.cs
git commit -m "$(cat <<'EOF'
refactor: drop QuantityOnHand from catalog import/export

Part of removing inventory quantity tracking; existing seed JSON keeps
the field on disk but it's now ignored on import.
EOF
)"
```

---

### Task 5: Update CutList.Mcp and republish

**Files:**
- Modify: `CutList.Mcp/ApiClient.cs:73-91,355-366`
- Modify: `CutList.Mcp/InventoryTools.cs:139-357,514-552`

**Interfaces:**
- Consumes: `StockItemDto`/`CreateStockItemDto` shape from Task 2 (no `QuantityOnHand`) — the REST API this project calls has already dropped the field, so this task brings the MCP client in sync.
- Produces: `add_stock_item` and `add_stock` MCP tools with no `quantityOnHand`/`QuantityOnHand` parameters or fields.

- [ ] **Step 1: Update `ApiClient.cs`**

`ApiStockItemDto` currently:
```csharp
public class ApiStockItemDto
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int QuantityOnHand { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}
```
becomes:
```csharp
public class ApiStockItemDto
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}
```

`CreateStockItemAsync` currently:
```csharp
    public async Task<ApiStockItemDto?> CreateStockItemAsync(int materialId, string length, string? name, int quantityOnHand, string? notes)
    {
        var body = new
        {
            MaterialId = materialId,
            Length = length,
            Name = name,
            QuantityOnHand = quantityOnHand,
            Notes = notes
        };
```
becomes:
```csharp
    public async Task<ApiStockItemDto?> CreateStockItemAsync(int materialId, string length, string? name, string? notes)
    {
        var body = new
        {
            MaterialId = materialId,
            Length = length,
            Name = name,
            Notes = notes
        };
```

- [ ] **Step 2: Update `InventoryTools.cs` — `StockItemDto` and `ListStockItems`**

The MCP-local `StockItemDto`:
```csharp
public class StockItemDto
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int QuantityOnHand { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}
```
becomes:
```csharp
public class StockItemDto
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}
```

In `ListStockItems`, the mapping:
```csharp
            StockItems = items.Select(s => new StockItemDto
            {
                Id = s.Id,
                MaterialId = s.MaterialId,
                MaterialName = s.MaterialName,
                LengthInches = s.LengthInches,
                LengthFormatted = s.LengthFormatted,
                Name = s.Name,
                QuantityOnHand = s.QuantityOnHand,
                Notes = s.Notes,
                IsActive = s.IsActive
            }).ToList()
```
becomes:
```csharp
            StockItems = items.Select(s => new StockItemDto
            {
                Id = s.Id,
                MaterialId = s.MaterialId,
                MaterialName = s.MaterialName,
                LengthInches = s.LengthInches,
                LengthFormatted = s.LengthFormatted,
                Name = s.Name,
                Notes = s.Notes,
                IsActive = s.IsActive
            }).ToList()
```

- [ ] **Step 3: Update `AddStockItem`**

```csharp
    [McpServerTool(Name = "add_stock_item"), Description("Adds a new stock item (a specific length of material that can be stocked).")]
    public async Task<StockItemResult> AddStockItem(
        [Description("Material ID (use list_materials to find IDs)")]
        int materialId,
        [Description("Stock length (e.g., '20'', '240', '20 ft')")]
        string length,
        [Description("Optional name/label for this stock item")]
        string? name = null,
        [Description("Initial quantity on hand (default 0)")]
        int quantityOnHand = 0,
        [Description("Notes")]
        string? notes = null)
    {
        try
        {
            var stockItem = await _api.CreateStockItemAsync(materialId, length, name, quantityOnHand, notes);

            if (stockItem == null)
                return new StockItemResult { Success = false, Error = "Failed to create stock item" };

            return new StockItemResult
            {
                Success = true,
                StockItem = new StockItemDto
                {
                    Id = stockItem.Id,
                    MaterialId = stockItem.MaterialId,
                    MaterialName = stockItem.MaterialName,
                    LengthInches = stockItem.LengthInches,
                    LengthFormatted = stockItem.LengthFormatted,
                    Name = stockItem.Name,
                    QuantityOnHand = stockItem.QuantityOnHand,
                    Notes = stockItem.Notes,
                    IsActive = stockItem.IsActive
                }
            };
        }
        catch (ApiConflictException ex)
        {
            return new StockItemResult { Success = false, Error = ex.Message };
        }
        catch (HttpRequestException ex)
        {
            return new StockItemResult { Success = false, Error = ex.Message };
        }
    }
```
becomes:
```csharp
    [McpServerTool(Name = "add_stock_item"), Description("Adds a new stock item (a specific length of material that can be stocked).")]
    public async Task<StockItemResult> AddStockItem(
        [Description("Material ID (use list_materials to find IDs)")]
        int materialId,
        [Description("Stock length (e.g., '20'', '240', '20 ft')")]
        string length,
        [Description("Optional name/label for this stock item")]
        string? name = null,
        [Description("Notes")]
        string? notes = null)
    {
        try
        {
            var stockItem = await _api.CreateStockItemAsync(materialId, length, name, notes);

            if (stockItem == null)
                return new StockItemResult { Success = false, Error = "Failed to create stock item" };

            return new StockItemResult
            {
                Success = true,
                StockItem = new StockItemDto
                {
                    Id = stockItem.Id,
                    MaterialId = stockItem.MaterialId,
                    MaterialName = stockItem.MaterialName,
                    LengthInches = stockItem.LengthInches,
                    LengthFormatted = stockItem.LengthFormatted,
                    Name = stockItem.Name,
                    Notes = stockItem.Notes,
                    IsActive = stockItem.IsActive
                }
            };
        }
        catch (ApiConflictException ex)
        {
            return new StockItemResult { Success = false, Error = ex.Message };
        }
        catch (HttpRequestException ex)
        {
            return new StockItemResult { Success = false, Error = ex.Message };
        }
    }
```

- [ ] **Step 4: Update the `AddStock` convenience tool**

```csharp
    [McpServerTool(Name = "add_stock"), Description("Convenience method: adds a material (if needed) and a stock item (if needed) with an initial quantity, all in one call.")]
    public async Task<AddStockResult> AddStock(
        [Description("Material shape (e.g., 'Angle', 'FlatBar')")]
        string shape,
        [Description("Material size (e.g., '2 x 2 x 1/4')")]
        string size,
        [Description("Stock length (e.g., '20'', '240')")]
        string length,
        [Description("Quantity on hand (default 0)")]
        int quantityOnHand = 0,
        [Description("Material type: Steel, Aluminum, Stainless, Brass, Copper (default: Steel)")]
        string type = "Steel",
        [Description("Grade or specification (e.g., 'A36', 'Hot Roll', '304', '6061-T6')")]
        string? grade = null)
```
becomes:
```csharp
    [McpServerTool(Name = "add_stock"), Description("Convenience method: adds a material (if needed) and a stock item (if needed), all in one call.")]
    public async Task<AddStockResult> AddStock(
        [Description("Material shape (e.g., 'Angle', 'FlatBar')")]
        string shape,
        [Description("Material size (e.g., '2 x 2 x 1/4')")]
        string size,
        [Description("Stock length (e.g., '20'', '240')")]
        string length,
        [Description("Material type: Steel, Aluminum, Stainless, Brass, Copper (default: Steel)")]
        string type = "Steel",
        [Description("Grade or specification (e.g., 'A36', 'Hot Roll', '304', '6061-T6')")]
        string? grade = null)
```

Further down in the same method, the two `CreateStockItemAsync` calls and the final result:
```csharp
                stockItem = await _api.CreateStockItemAsync(material.Id, length, null, quantityOnHand, null);
```
becomes:
```csharp
                stockItem = await _api.CreateStockItemAsync(material.Id, length, null, null);
```

```csharp
        return new AddStockResult
        {
            Success = true,
            MaterialId = material.Id,
            MaterialName = $"{material.Shape} - {material.Size}",
            MaterialCreated = materialCreated,
            StockItemId = stockItem.Id,
            StockItemCreated = stockItemCreated,
            LengthFormatted = ArchUnits.FormatFromInches(lengthInches),
            QuantityOnHand = stockItem.QuantityOnHand
        };
```
becomes:
```csharp
        return new AddStockResult
        {
            Success = true,
            MaterialId = material.Id,
            MaterialName = $"{material.Shape} - {material.Size}",
            MaterialCreated = materialCreated,
            StockItemId = stockItem.Id,
            StockItemCreated = stockItemCreated,
            LengthFormatted = ArchUnits.FormatFromInches(lengthInches)
        };
```

- [ ] **Step 5: Drop `QuantityOnHand` from the two DTO classes at the bottom of the file**

```csharp
public class StockItemDto
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int QuantityOnHand { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}
```
(this is a duplicate declaration in the "DTOs" region at the bottom of the file — same fix as Step 2, applied to that copy)
becomes:
```csharp
public class StockItemDto
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}
```

> **Note:** `InventoryTools.cs` declares `StockItemDto` twice today — once inline in the class (used by `ListStockItems`/`AddStockItem`) and once in the `#region DTOs` block at the bottom of the file. Both need the `QuantityOnHand` property removed; grep the file for `public class StockItemDto` to confirm you've caught both before moving on.

```csharp
public class AddStockResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public bool MaterialCreated { get; set; }
    public int StockItemId { get; set; }
    public bool StockItemCreated { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
}
```
becomes:
```csharp
public class AddStockResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public bool MaterialCreated { get; set; }
    public int StockItemId { get; set; }
    public bool StockItemCreated { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
}
```

- [ ] **Step 6: Build**

Run:
```bash
dotnet build CutList.Mcp/CutList.Mcp.csproj
```
Expected: 0 errors.

- [ ] **Step 7: Republish the MCP server**

Per the project's MCP publishing workflow (CutList.Web must be running on `http://localhost:5270` for a live smoke test, but publishing itself doesn't require it):
```bash
dotnet publish CutList.Mcp/CutList.Mcp.csproj -c Release -o "$USERPROFILE/.claude/mcp/CutList.Mcp"
```

- [ ] **Step 8: Manual check**

Restart the `CutListMcp` MCP connection (or restart this Claude Code session) and call `add_stock` with a shape/size/length that doesn't exist yet, confirming it still creates the material and stock item successfully without a `quantityOnHand` argument.

- [ ] **Step 9: Commit**

```bash
git add CutList.Mcp/ApiClient.cs CutList.Mcp/InventoryTools.cs
git commit -m "$(cat <<'EOF'
refactor: drop QuantityOnHand from CutList.Mcp inventory tools

Matches the REST API's StockItemDto/CreateStockItemDto shape after
inventory quantity tracking removal. Republished to
~/.claude/mcp/CutList.Mcp/.
EOF
)"
```

---

### Task 6: Drop QuantityOnHand column and StockTransaction table

**Files:**
- Modify: `CutList.Web/Data/Entities/StockItem.cs`
- Delete: `CutList.Web/Data/Entities/StockTransaction.cs`
- Modify: `CutList.Web/Data/ApplicationDbContext.cs:16,160-177`
- Create: `CutList.Web/Migrations/<timestamp>_RemoveInventoryQuantityTracking.cs` (generated)

**Interfaces:**
- Consumes: confirmation that nothing outside this set of files still references `StockItem.QuantityOnHand`, `StockTransaction`, or `StockTransactionType` (true after Tasks 1-5).
- Produces: final entity/schema shape. No later task depends on anything new here beyond "the column/table are gone."

- [ ] **Step 1: Confirm no remaining references**

Run:
```bash
mcp__RoslynBridge__search_code  # query: "QuantityOnHand" across the solution
```
or, if unavailable, Grep for `QuantityOnHand` and `StockTransaction` across `CutList.Web` and `CutList.Mcp`. Expected: matches only inside `CutList.Web/Data/Entities/StockItem.cs`, `CutList.Web/Data/Entities/StockTransaction.cs`, `CutList.Web/Data/ApplicationDbContext.cs`, and the historical `Migrations/*.cs`/`*.Designer.cs` files (which are a permanent historical record and must not be edited).

- [ ] **Step 2: Update `StockItem.cs`**

```csharp
namespace CutList.Web.Data.Entities;

public class StockItem
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public decimal LengthInches { get; set; }
    public string? Name { get; set; }
    public int QuantityOnHand { get; set; } = 0;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Material Material { get; set; } = null!;
    public ICollection<StockTransaction> Transactions { get; set; } = new List<StockTransaction>();
}
```
becomes:
```csharp
namespace CutList.Web.Data.Entities;

public class StockItem
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public decimal LengthInches { get; set; }
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Material Material { get; set; } = null!;
}
```

- [ ] **Step 3: Delete `StockTransaction.cs`**

Delete the file `CutList.Web/Data/Entities/StockTransaction.cs` entirely (it defined `StockTransaction` and `StockTransactionType`).

- [ ] **Step 4: Update `ApplicationDbContext.cs`**

Remove the `DbSet`:
```csharp
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<CuttingTool> CuttingTools => Set<CuttingTool>();
```
becomes:
```csharp
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<CuttingTool> CuttingTools => Set<CuttingTool>();
```

Remove the `StockTransaction` config block:
```csharp
        // StockItem
        modelBuilder.Entity<StockItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LengthInches).HasPrecision(10, 4);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Material)
                .WithMany(m => m.StockItems)
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.MaterialId, e.LengthInches }).IsUnique();
        });

        // StockTransaction
        modelBuilder.Entity<StockTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.StockItem)
                .WithMany(s => s.Transactions)
                .HasForeignKey(e => e.StockItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Job)
                .WithMany()
                .HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // CuttingTool
```
becomes:
```csharp
        // StockItem
        modelBuilder.Entity<StockItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LengthInches).HasPrecision(10, 4);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Material)
                .WithMany(m => m.StockItems)
                .HasForeignKey(e => e.MaterialId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.MaterialId, e.LengthInches }).IsUnique();
        });

        // CuttingTool
```

- [ ] **Step 5: Build and check diagnostics**

Run:
```bash
mcp__RoslynBridge__get_diagnostics_summary  # for CutList.Web
dotnet build CutList.Web/CutList.Web.csproj
dotnet build CutList.sln
```
Expected: 0 errors across the whole solution — this is the point where every consumer touched in Tasks 1-5 finally has to line up.

- [ ] **Step 6: Create and apply the EF Core migration**

Run:
```bash
dotnet ef migrations add RemoveInventoryQuantityTracking --project CutList.Web
dotnet ef database update --project CutList.Web
```
Expected: the generated migration's `Up` drops the `QuantityOnHand` column from `StockItems` and drops the `StockTransactions` table (EF will infer this automatically from the model diff — no manual SQL needed). Confirm both operations appear in the generated migration file before applying.

- [ ] **Step 7: Manual check**

Run the full app end-to-end: create a job, add parts, add both a catalog stock item (finite quantity) and a custom unlimited stock line, optimize, confirm results are correct and the "Items Not Placed" section behaves as expected when quantity is insufficient. Confirm `/stock` CRUD still works post-migration.

- [ ] **Step 8: Commit**

```bash
git add CutList.Web/Data/Entities/StockItem.cs CutList.Web/Data/ApplicationDbContext.cs CutList.Web/Migrations/
git rm CutList.Web/Data/Entities/StockTransaction.cs
git commit -m "$(cat <<'EOF'
feat: drop QuantityOnHand column and StockTransactions table

Final step of removing inventory quantity tracking - see
docs/superpowers/specs/2026-08-01-remove-inventory-quantity-tracking-design.md.
Destructive to any existing on-hand/transaction data; confirmed
acceptable since nothing read it automatically.
EOF
)"
```

---

### Task 7: Allow unlimited quantity for catalog-sourced job stock, and tweak the unplaced-items message

**Files:**
- Modify: `CutList.Web/Components/Pages/Jobs/Edit.razor`

**Interfaces:**
- Consumes: `JobStock.Quantity` (unchanged type — `int`, `-1` = unlimited, already supported end-to-end by `CutListPackingService`/`MultiBinEngine` for custom-length rows since before this refactor).
- Produces: nothing new for other tasks; this is the last functional change in the plan.

- [ ] **Step 1: Widen the "Qty to Use" input on the catalog-stock form**

```razor
                <div class="col-md-3">
                    <label class="form-label">Qty to Use</label>
                    <input type="number" class="form-control" @bind="newStock.Quantity" min="1" />
                </div>
```
becomes:
```razor
                <div class="col-md-3">
                    <label class="form-label">Qty to Use</label>
                    <input type="number" class="form-control" @bind="newStock.Quantity" min="-1" />
                    <small class="text-muted">-1 = unlimited</small>
                </div>
```

- [ ] **Step 2: Match the validation rule to the custom-stock form**

In `SaveStockFromInventoryAsync`:
```csharp
        if (newStock.Quantity < 1)
        {
            stockErrorMessage = "Quantity must be at least 1";
            return;
        }
```
becomes:
```csharp
        if (newStock.Quantity < -1 || newStock.Quantity == 0)
        {
            stockErrorMessage = "Quantity must be at least 1 (or -1 for unlimited)";
            return;
        }
```

- [ ] **Step 3: Tweak the "Items Not Placed" copy on the Results tab**

```razor
                            @if (materialResult.PackResult.ItemsNotUsed.Count > 0)
                            {
                                <div class="alert alert-danger">
                                    <strong>@materialResult.PackResult.ItemsNotUsed.Count items not placed</strong> —
                                    No stock lengths available or parts too long.
                                </div>
                            }
```
becomes:
```razor
                            @if (materialResult.PackResult.ItemsNotUsed.Count > 0)
                            {
                                <div class="alert alert-danger">
                                    <strong>@materialResult.PackResult.ItemsNotUsed.Count items not placed</strong> —
                                    not enough stock quantity entered for this job, no stock lengths configured, or parts too long.
                                </div>
                            }
```

- [ ] **Step 4: Build and check diagnostics**

Run:
```bash
mcp__RoslynBridge__get_diagnostics_summary  # for CutList.Web
dotnet build CutList.Web/CutList.Web.csproj
```
Expected: 0 errors.

- [ ] **Step 5: Manual check**

In the running app, add a catalog-sourced stock line to a job's Stock tab and set its quantity to `-1`; confirm it saves without the old "at least 1" error. Set a finite quantity below what's needed to pack all parts and confirm the Results tab's unplaced-items message reads correctly.

- [ ] **Step 6: Commit**

```bash
git add CutList.Web/Components/Pages/Jobs/Edit.razor
git commit -m "$(cat <<'EOF'
feat: allow unlimited quantity for catalog-sourced job stock

Custom-length job stock already supported -1 (unlimited); catalog-
sourced stock only allowed >= 1. Closes that inconsistency per
docs/superpowers/specs/2026-08-01-remove-inventory-quantity-tracking-design.md.
Also updates the Results tab's unplaced-items message now that
insufficient configured quantity is a normal cause, not an edge case.
EOF
)"
```

---

### Task 8: Full-solution verification pass

**Files:** none (verification only)

**Interfaces:**
- Consumes: everything from Tasks 1-7.
- Produces: confidence the refactor is complete and consistent.

- [ ] **Step 1: Clean full-solution build**

```bash
dotnet clean CutList.sln
dotnet build CutList.sln
```
Expected: 0 errors, 0 new warnings related to `StockItem`/`StockTransaction`/`QuantityOnHand`.

- [ ] **Step 2: Confirm no stray references remain**

```bash
mcp__RoslynBridge__search_code  # query: "QuantityOnHand" and separately "StockTransaction" across the whole solution
```
Expected: zero hits outside historical `Migrations/*.cs` / `*.Designer.cs` files (which must never be edited retroactively).

- [ ] **Step 3: End-to-end manual walkthrough**

With `dotnet run --project CutList.Web/CutList.Web.csproj` running:
1. `/stock` — create two stock items for the same material (different lengths), confirm no quantity fields anywhere on the page.
2. Create a new job, add parts for that material.
3. On the Stock tab: add one catalog-sourced stock row with a finite quantity (e.g. `2`), and one custom-length row set to unlimited (`-1`).
4. Optimize. Confirm `InStockBins` reflects the catalog row correctly, and parts beyond what the finite quantity can supply either come from the unlimited custom row or land in "Items Not Placed" if none is configured for that case.
5. Lock the job, confirm locking still works exactly as before (unaffected by this refactor).
6. Via the MCP tools (`list_stock_items`, `add_stock`), confirm both work end-to-end against the running app with no `quantityOnHand` involved.

- [ ] **Step 4: Update CLAUDE.md if needed**

Re-read the `StockItem`, `StockTransaction` (delete this subsection), `StockItemService`, and packing-related sections of `CutList.Web`'s `CLAUDE.md` entity/service tables and correct any that still describe `QuantityOnHand`/`StockTransaction`/auto-discovery. This project's CLAUDE.md explicitly requires staying current with entity/service changes.

- [ ] **Step 5: Final commit (if Step 4 produced changes)**

```bash
git add CLAUDE.md
git commit -m "$(cat <<'EOF'
docs: update CLAUDE.md for inventory quantity tracking removal
EOF
)"
```

---

## Self-Review Notes

- **Spec coverage:** every "Scope" bullet in `docs/superpowers/specs/2026-08-01-remove-inventory-quantity-tracking-design.md` maps to a task above — data model (Task 6), services/API (Tasks 2, 3), catalog (Task 4), MCP (Task 5), UI (Tasks 1, 7), migration (Task 6). "Out of scope" items (packing algorithm itself, Purchase List concept, seed-file cleanup) are correctly untouched by every task.
- **Placeholder scan:** no TBDs; every step shows exact before/after code or an exact shell command.
- **Type consistency:** `JobStock.Quantity` stays `int` throughout (no type change, just widened validation range) — verified consistent across Tasks 1, 3, 7. `StockItemDto`/`CreateStockItemDto` shapes in Task 2 match what Task 5's MCP client is updated to expect.
