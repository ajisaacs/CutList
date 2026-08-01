# Unified Add/Edit Stock Modal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Job Edit page's Stock tab three inconsistent add/edit UI surfaces (bulk import modal, inline custom-length form, inline edit forms) with a single "Add Stock" button and one modal that handles both adding and editing, for both inventory-sourced and custom-length stock.

**Architecture:** Single-file Razor component change in `CutList.Web/Components/Pages/Jobs/Edit.razor`. Three boolean visibility flags (`showStockForm`, `showCustomStockForm`, `showImportModal`) collapse into one (`showStockModal`) plus a `StockModalTab` enum (`Inventory`/`Custom`) for tab selection; the existing `editingStock` field (already present) continues to discriminate Add vs Edit mode. All markup for the three old surfaces is merged into one top-level modal block, following the same top-level-markup-block pattern already used by the existing "Part Modal Dialog" and (soon-to-be-replaced) "Import Stock Modal" blocks in this file.

**Tech Stack:** .NET 8 Blazor Server, Bootstrap 5 modal/tab markup. No automated test project exists for `CutList.Web` (confirmed: no `*.Tests.csproj` in the solution). Verification is `dotnet build` for compile correctness plus manual browser verification via the `webapp-testing` skill (Playwright) for behavior — there is no unit-test step in this plan.

## Global Constraints

- Full design spec: `docs/superpowers/specs/2026-08-01-unified-add-stock-modal-design.md` — every requirement in it must be reflected below.
- No changes to the Parts tab, its modal, `JobService`, or any API surface (spec's "Out of Scope" section).
- No change to validation rules or business logic — UI consolidation only. All four existing handler methods (`SaveStockFromInventoryAsync`, `SaveCustomStockAsync`, `ImportSelectedStockAsync`, and the renamed candidate-loading method) keep their existing validation logic verbatim.
- Match existing code style in this file: top-level markup blocks (not extra `RenderFragment` C# methods) for modals, mirroring the existing "Part Modal Dialog" block's inline `@if`/`else` branching.

---

### Task 1: Consolidate Stock tab add/edit UI into one modal

**Files:**
- Modify: `CutList.Web/Components/Pages/Jobs/Edit.razor`

**Interfaces:**
- Produces: `private enum StockModalTab { Inventory, Custom }`, `private bool showStockModal`, `private StockModalTab stockModalTab`, `private async Task ShowAddStockModal()`, `private void CloseStockModal()`, `private async Task LoadImportCandidatesAsync()` — these are the only new/renamed symbols; every other field/method referenced below (`newStock`, `editingStock`, `stockErrorMessage`, `stockSelectedShape`, `stockSelectedMaterialId`, `availableStockItems`, `loadingImport`, `importCandidates`, `importErrorMessage`, `ImportStockCandidate`, `job`, `DistinctShapes`, `materials`, `ArchUnits`, `OnStockShapeChanged`, `OnStockMaterialChanged`, `SaveStockFromInventoryAsync`, `SaveCustomStockAsync`, `ImportSelectedStockAsync`, `ToggleAllImportCandidates`, `EditStock`, `DeleteStock`, `RenderStockTable`) already exists in the file and keeps its current signature.

This is one cohesive task — the markup and the code-behind are too interdependent in a single `.razor` file to compile in a valid intermediate state, so it's implemented as one connected set of edits followed by a single build/verification gate.

- [ ] **Step 1: Replace the Stock-form and Import-modal state fields with the unified modal state**

In the `@code` block, find this region (currently around line 353-367):

```csharp
    // Stock form
    private bool showStockForm;
    private bool showCustomStockForm;
    private JobStock newStock = new();
    private JobStock? editingStock;
    private string? stockErrorMessage;
    private MaterialShape? stockSelectedShape;
    private int stockSelectedMaterialId;
    private List<StockItem> availableStockItems = new();

    // Import modal
    private bool showImportModal;
    private bool loadingImport;
    private List<ImportStockCandidate> importCandidates = new();
    private string? importErrorMessage;
```

Replace it with:

```csharp
    // Stock modal (unified add/edit, both inventory-sourced and custom-length)
    private enum StockModalTab { Inventory, Custom }
    private bool showStockModal;
    private StockModalTab stockModalTab = StockModalTab.Inventory;
    private JobStock newStock = new();
    private JobStock? editingStock;
    private string? stockErrorMessage;
    private MaterialShape? stockSelectedShape;
    private int stockSelectedMaterialId;
    private List<StockItem> availableStockItems = new();
    private bool loadingImport;
    private List<ImportStockCandidate> importCandidates = new();
    private string? importErrorMessage;
```

- [ ] **Step 2: Replace `RenderStockTab` to use a single "Add Stock" button and drop the inline form rendering**

Find (currently around line 737-777):

```csharp
    // Stock tab
    private RenderFragment RenderStockTab() => __builder =>
    {
        <div class="card">
            <div class="card-header d-flex justify-content-between align-items-center">
                <h5 class="mb-0">Stock for This Job</h5>
                @if (!job.IsLocked)
                {
                    <div class="d-flex gap-2">
                        <button class="btn btn-success" @onclick="ShowImportModal" disabled="@(job.Parts.Count == 0)"
                                title="@(job.Parts.Count == 0 ? "Add parts first to match against inventory" : "Find and import stock matching your parts")">
                            Import from Inventory
                        </button>
                        <button class="btn btn-primary" @onclick="ShowAddCustomStock">Add Custom Length</button>
                    </div>
                }
            </div>
            <div class="card-body">
                @if (showStockForm)
                {
                    @RenderStockFromInventoryForm()
                }
                else if (showCustomStockForm)
                {
                    @RenderCustomStockForm()
                }

                @if (job.Stock.Count == 0)
                {
                    <div class="text-center py-4 text-muted">
                        <p class="mb-2">No stock configured for this job.</p>
                        <p class="small">Add stock from your inventory or define custom lengths.</p>
                        <p class="small">If no stock is selected, the optimizer will use all available stock for the materials in your parts list.</p>
                    </div>
                }
                else
                {
                    @RenderStockTable()
                }
            </div>
        </div>
    };
```

Replace it with:

```csharp
    // Stock tab
    private RenderFragment RenderStockTab() => __builder =>
    {
        <div class="card">
            <div class="card-header d-flex justify-content-between align-items-center">
                <h5 class="mb-0">Stock for This Job</h5>
                @if (!job.IsLocked)
                {
                    <button class="btn btn-primary" @onclick="ShowAddStockModal">Add Stock</button>
                }
            </div>
            <div class="card-body">
                @if (job.Stock.Count == 0)
                {
                    <div class="text-center py-4 text-muted">
                        <p class="mb-2">No stock configured for this job.</p>
                        <p class="small">Add stock from your inventory or define custom lengths.</p>
                        <p class="small">If no stock is selected, the optimizer will use all available stock for the materials in your parts list.</p>
                    </div>
                }
                else
                {
                    @RenderStockTable()
                }
            </div>
        </div>
    };
```

- [ ] **Step 3: Delete `RenderStockFromInventoryForm` and `RenderCustomStockForm`**

Delete these two methods entirely (currently around lines 779-894, immediately after the new `RenderStockTab` and before `RenderStockTable`):

```csharp
    private RenderFragment RenderStockFromInventoryForm() => __builder =>
    {
        <div class="border rounded p-3 mb-3 bg-light">
            <h6>@(editingStock == null ? "Add Stock from Inventory" : "Edit Stock Selection")</h6>
            ...
        </div>
    };

    private RenderFragment RenderCustomStockForm() => __builder =>
    {
        <div class="border rounded p-3 mb-3 bg-light">
            <h6>@(editingStock == null ? "Add Custom Stock Length" : "Edit Custom Stock")</h6>
            ...
        </div>
    };
```

(Their field content is reused, without the wrapping `<div class="border...">`/`<h6>`/footer-button markup, inside the new modal in Step 6.)

- [ ] **Step 4: Replace `ShowAddCustomStock`/`CancelStockForm` with `ShowAddStockModal`/`CloseStockModal`, and rename `ShowImportModal`/`CloseImportModal`**

Find (currently around line 1230-1246):

```csharp
    private void ShowAddCustomStock()
    {
        editingStock = null;
        newStock = new JobStock { JobId = Id!.Value, Quantity = -1, Priority = 10, IsCustomLength = true };
        stockSelectedShape = null;
        showStockForm = false;
        showCustomStockForm = true;
        stockErrorMessage = null;
    }

    private void CancelStockForm()
    {
        showStockForm = false;
        showCustomStockForm = false;
        editingStock = null;
    }
```

Replace it with:

```csharp
    private async Task ShowAddStockModal()
    {
        editingStock = null;
        newStock = new JobStock { JobId = Id!.Value, Quantity = -1, Priority = 10 };
        stockSelectedShape = null;
        stockSelectedMaterialId = 0;
        availableStockItems.Clear();
        stockErrorMessage = null;
        importErrorMessage = null;
        importCandidates.Clear();
        stockModalTab = job.Parts.Count > 0 ? StockModalTab.Inventory : StockModalTab.Custom;
        showStockModal = true;

        if (job.Parts.Count > 0)
        {
            await LoadImportCandidatesAsync();
        }
    }

    private void CloseStockModal()
    {
        showStockModal = false;
        editingStock = null;
        importCandidates.Clear();
        stockErrorMessage = null;
        importErrorMessage = null;
    }
```

- [ ] **Step 5: Update `EditStock` to open the unified modal**

Find (currently around line 1269-1299):

```csharp
    private void EditStock(JobStock stock)
    {
        editingStock = stock;
        newStock = new JobStock
        {
            Id = stock.Id,
            JobId = stock.JobId,
            MaterialId = stock.MaterialId,
            StockItemId = stock.StockItemId,
            LengthInches = stock.LengthInches,
            Quantity = stock.Quantity,
            IsCustomLength = stock.IsCustomLength,
            Priority = stock.Priority,
            SortOrder = stock.SortOrder
        };
        stockSelectedShape = stock.Material?.Shape;
        stockSelectedMaterialId = stock.MaterialId;
        stockErrorMessage = null;

        if (stock.IsCustomLength)
        {
            showStockForm = false;
            showCustomStockForm = true;
        }
        else
        {
            showStockForm = true;
            showCustomStockForm = false;
            _ = OnStockMaterialChanged();
        }
    }
```

Replace it with:

```csharp
    private void EditStock(JobStock stock)
    {
        editingStock = stock;
        newStock = new JobStock
        {
            Id = stock.Id,
            JobId = stock.JobId,
            MaterialId = stock.MaterialId,
            StockItemId = stock.StockItemId,
            LengthInches = stock.LengthInches,
            Quantity = stock.Quantity,
            IsCustomLength = stock.IsCustomLength,
            Priority = stock.Priority,
            SortOrder = stock.SortOrder
        };
        stockSelectedShape = stock.Material?.Shape;
        stockSelectedMaterialId = stock.MaterialId;
        stockErrorMessage = null;
        stockModalTab = stock.IsCustomLength ? StockModalTab.Custom : StockModalTab.Inventory;
        showStockModal = true;

        if (!stock.IsCustomLength)
        {
            _ = OnStockMaterialChanged();
        }
    }
```

- [ ] **Step 6: Update the three save/import completion handlers to close the unified modal**

In `SaveStockFromInventoryAsync` (currently around line 1349-1353), find:

```csharp
        job = (await JobService.GetByIdAsync(Id!.Value))!;
        showStockForm = false;
        editingStock = null;
        packResult = null;
        summary = null;
    }
```

(this is the end of `SaveStockFromInventoryAsync`) and replace with:

```csharp
        job = (await JobService.GetByIdAsync(Id!.Value))!;
        showStockModal = false;
        editingStock = null;
        packResult = null;
        summary = null;
    }
```

In `SaveCustomStockAsync` (currently around line 1396-1401), find:

```csharp
        job = (await JobService.GetByIdAsync(Id!.Value))!;
        showCustomStockForm = false;
        editingStock = null;
        packResult = null;
        summary = null;
    }
```

Replace with:

```csharp
        job = (await JobService.GetByIdAsync(Id!.Value))!;
        showStockModal = false;
        editingStock = null;
        packResult = null;
        summary = null;
    }
```

In `ImportSelectedStockAsync` (currently around line 1492-1497), find:

```csharp
            job = (await JobService.GetByIdAsync(Id!.Value))!;
            showImportModal = false;
            importCandidates.Clear();
            packResult = null;
            summary = null;
        }
```

Replace with:

```csharp
            job = (await JobService.GetByIdAsync(Id!.Value))!;
            showStockModal = false;
            importCandidates.Clear();
            packResult = null;
            summary = null;
        }
```

- [ ] **Step 7: Rename `ShowImportModal` to `LoadImportCandidatesAsync` and delete `CloseImportModal`**

Find (currently around line 1411-1457):

```csharp
    // Import modal methods
    private async Task ShowImportModal()
    {
        importErrorMessage = null;
        importCandidates.Clear();
        loadingImport = true;
        showImportModal = true;

        try
        {
            var materialIds = job.Parts.Select(p => p.MaterialId).Distinct().ToList();
            var existingStockItemIds = job.Stock
                .Where(s => s.StockItemId.HasValue)
                .Select(s => s.StockItemId!.Value)
                .ToHashSet();

            foreach (var materialId in materialIds)
            {
                var stockItems = await JobService.GetAvailableStockForMaterialAsync(materialId);
                foreach (var item in stockItems.Where(s => !existingStockItemIds.Contains(s.Id)))
                {
                    importCandidates.Add(new ImportStockCandidate
                    {
                        StockItem = item,
                        Selected = true,
                        Quantity = -1,
                        Priority = 10
                    });
                }
            }
        }
        catch (Exception ex)
        {
            importErrorMessage = $"Error loading stock: {ex.Message}";
        }
        finally
        {
            loadingImport = false;
        }
    }

    private void CloseImportModal()
    {
        showImportModal = false;
        importCandidates.Clear();
        importErrorMessage = null;
    }
```

Replace it with:

```csharp
    // Loads inventory stock candidates for the modal's "From Inventory" tab
    private async Task LoadImportCandidatesAsync()
    {
        importErrorMessage = null;
        importCandidates.Clear();
        loadingImport = true;

        try
        {
            var materialIds = job.Parts.Select(p => p.MaterialId).Distinct().ToList();
            var existingStockItemIds = job.Stock
                .Where(s => s.StockItemId.HasValue)
                .Select(s => s.StockItemId!.Value)
                .ToHashSet();

            foreach (var materialId in materialIds)
            {
                var stockItems = await JobService.GetAvailableStockForMaterialAsync(materialId);
                foreach (var item in stockItems.Where(s => !existingStockItemIds.Contains(s.Id)))
                {
                    importCandidates.Add(new ImportStockCandidate
                    {
                        StockItem = item,
                        Selected = true,
                        Quantity = -1,
                        Priority = 10
                    });
                }
            }
        }
        catch (Exception ex)
        {
            importErrorMessage = $"Error loading stock: {ex.Message}";
        }
        finally
        {
            loadingImport = false;
        }
    }
```

- [ ] **Step 8: Replace the "Import Stock Modal" markup block with the unified "Add/Edit Stock Modal" block**

Find the entire top-level block currently at lines 232-324:

```razor
@* Import Stock Modal *@
@if (showImportModal)
{
    <div class="modal fade show d-block" tabindex="-1" style="background-color: rgba(0,0,0,0.5);">
        <div class="modal-dialog modal-lg">
            <div class="modal-content">
                ...
            </div>
        </div>
    </div>
}
```

(the full existing block — everything between `@* Import Stock Modal *@` and its closing `}`)

Replace it with:

```razor
@* Add/Edit Stock Modal *@
@if (showStockModal)
{
    <div class="modal fade show d-block" tabindex="-1" style="background-color: rgba(0,0,0,0.5);">
        <div class="modal-dialog modal-lg">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title">@(editingStock == null ? "Add Stock" : "Edit Stock")</h5>
                    <button type="button" class="btn-close" @onclick="CloseStockModal"></button>
                </div>
                <div class="modal-body">
                    @if (editingStock == null)
                    {
                        <ul class="nav nav-tabs mb-3" role="tablist">
                            <li class="nav-item" role="presentation">
                                <button class="nav-link @(stockModalTab == StockModalTab.Inventory ? "active" : "")"
                                        type="button" disabled="@(job.Parts.Count == 0)"
                                        title="@(job.Parts.Count == 0 ? "Add parts first to match against inventory" : "")"
                                        @onclick="() => stockModalTab = StockModalTab.Inventory">
                                    From Inventory
                                </button>
                            </li>
                            <li class="nav-item" role="presentation">
                                <button class="nav-link @(stockModalTab == StockModalTab.Custom ? "active" : "")"
                                        type="button"
                                        @onclick="() => stockModalTab = StockModalTab.Custom">
                                    Custom Length
                                </button>
                            </li>
                        </ul>
                    }

                    @if (stockModalTab == StockModalTab.Inventory && editingStock == null)
                    {
                        @if (loadingImport)
                        {
                            <div class="text-center py-4">
                                <span class="spinner-border"></span>
                                <p class="mt-2 text-muted">Finding matching stock...</p>
                            </div>
                        }
                        else if (importCandidates.Count == 0)
                        {
                            <div class="text-center py-4 text-muted">
                                <p class="mb-2">No matching inventory stock found.</p>
                                <p class="small">Either no stock items exist for the materials in your parts, or they have already been added to this job.</p>
                            </div>
                        }
                        else
                        {
                            <div class="d-flex justify-content-between align-items-center mb-3">
                                <div class="btn-group btn-group-sm">
                                    <button class="btn btn-outline-secondary" @onclick="() => ToggleAllImportCandidates(true)">Select All</button>
                                    <button class="btn btn-outline-secondary" @onclick="() => ToggleAllImportCandidates(false)">Select None</button>
                                </div>
                                <small class="text-muted">@importCandidates.Count(c => c.Selected) of @importCandidates.Count selected</small>
                            </div>

                            @foreach (var group in importCandidates
                                .GroupBy(c => c.StockItem.MaterialId)
                                .OrderBy(g => g.First().StockItem.Material.Shape)
                                .ThenBy(g => g.First().StockItem.Material.Size))
                            {
                                var material = group.First().StockItem.Material;
                                <h6 class="mt-3 mb-2 text-primary">@material.DisplayName</h6>
                                <table class="table table-sm table-hover mb-0">
                                    <thead>
                                        <tr>
                                            <th style="width: 40px;"></th>
                                            <th>Length</th>
                                            <th style="width: 120px;">Qty to Use</th>
                                            <th style="width: 100px;">Priority</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        @foreach (var candidate in group.OrderByDescending(c => c.StockItem.LengthInches))
                                        {
                                            <tr class="@(candidate.Selected ? "" : "text-muted")">
                                                <td>
                                                    <input type="checkbox" class="form-check-input" @bind="candidate.Selected" />
                                                </td>
                                                <td>@ArchUnits.FormatFromInches((double)candidate.StockItem.LengthInches)</td>
                                                <td>
                                                    <input type="number" class="form-control form-control-sm" @bind="candidate.Quantity"
                                                           min="-1" disabled="@(!candidate.Selected)" />
                                                    <small class="text-muted">-1 = unlimited</small>
                                                </td>
                                                <td>
                                                    <input type="number" class="form-control form-control-sm" @bind="candidate.Priority"
                                                           min="1" disabled="@(!candidate.Selected)" />
                                                </td>
                                            </tr>
                                        }
                                    </tbody>
                                </table>
                            }
                        }
                        @if (!string.IsNullOrEmpty(importErrorMessage))
                        {
                            <div class="alert alert-danger mt-3 mb-0">@importErrorMessage</div>
                        }
                    }
                    else if (stockModalTab == StockModalTab.Inventory && editingStock != null)
                    {
                        <div class="row g-3">
                            <div class="col-md-3">
                                <label class="form-label">Shape</label>
                                <select class="form-select" @bind="stockSelectedShape" @bind:after="OnStockShapeChanged">
                                    <option value="">-- Select --</option>
                                    @foreach (var shape in DistinctShapes)
                                    {
                                        <option value="@shape">@shape.GetDisplayName()</option>
                                    }
                                </select>
                            </div>
                            <div class="col-md-3">
                                <label class="form-label">Size</label>
                                <select class="form-select" @bind="stockSelectedMaterialId" @bind:after="OnStockMaterialChanged"
                                        disabled="@(!stockSelectedShape.HasValue)">
                                    <option value="0">-- Select --</option>
                                    @foreach (var material in materials.Where(m => stockSelectedShape.HasValue && m.Shape == stockSelectedShape.Value).OrderBy(m => m.SortOrder).ThenBy(m => m.Size))
                                    {
                                        <option value="@material.Id">@material.Size</option>
                                    }
                                </select>
                            </div>
                            <div class="col-md-3">
                                <label class="form-label">Stock Length</label>
                                <select class="form-select" @bind="newStock.StockItemId" disabled="@(stockSelectedMaterialId == 0)">
                                    <option value="">-- Select --</option>
                                    @foreach (var stock in availableStockItems)
                                    {
                                        <option value="@stock.Id">@ArchUnits.FormatFromInches((double)stock.LengthInches)</option>
                                    }
                                </select>
                            </div>
                            <div class="col-md-3">
                                <label class="form-label">Qty to Use</label>
                                <input type="number" class="form-control" @bind="newStock.Quantity" min="-1" />
                                <small class="text-muted">-1 = unlimited</small>
                            </div>
                        </div>
                        <div class="row g-3 mt-1">
                            <div class="col-md-3">
                                <label class="form-label">Priority</label>
                                <input type="number" class="form-control" @bind="newStock.Priority" min="1" />
                                <small class="text-muted">Lower = used first</small>
                            </div>
                        </div>
                        @if (!string.IsNullOrEmpty(stockErrorMessage))
                        {
                            <div class="alert alert-danger mt-3 mb-0">@stockErrorMessage</div>
                        }
                    }
                    else
                    {
                        <div class="row g-3">
                            <div class="col-md-3">
                                <label class="form-label">Shape</label>
                                <select class="form-select" @bind="stockSelectedShape" @bind:after="OnStockShapeChanged">
                                    <option value="">-- Select --</option>
                                    @foreach (var shape in DistinctShapes)
                                    {
                                        <option value="@shape">@shape.GetDisplayName()</option>
                                    }
                                </select>
                            </div>
                            <div class="col-md-3">
                                <label class="form-label">Size</label>
                                <select class="form-select" @bind="newStock.MaterialId" disabled="@(!stockSelectedShape.HasValue)">
                                    <option value="0">-- Select --</option>
                                    @foreach (var material in materials.Where(m => stockSelectedShape.HasValue && m.Shape == stockSelectedShape.Value).OrderBy(m => m.SortOrder).ThenBy(m => m.Size))
                                    {
                                        <option value="@material.Id">@material.Size</option>
                                    }
                                </select>
                            </div>
                            <div class="col-md-3">
                                <label class="form-label">Length</label>
                                <LengthInput @bind-Value="newStock.LengthInches" />
                            </div>
                            <div class="col-md-3">
                                <label class="form-label">Quantity</label>
                                <input type="number" class="form-control" @bind="newStock.Quantity" min="-1" />
                                <small class="text-muted">Use -1 for unlimited</small>
                            </div>
                        </div>
                        <div class="row g-3 mt-1">
                            <div class="col-md-3">
                                <label class="form-label">Priority</label>
                                <input type="number" class="form-control" @bind="newStock.Priority" min="1" />
                                <small class="text-muted">Lower = used first</small>
                            </div>
                        </div>
                        @if (!string.IsNullOrEmpty(stockErrorMessage))
                        {
                            <div class="alert alert-danger mt-3 mb-0">@stockErrorMessage</div>
                        }
                    }
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-outline-secondary" @onclick="CloseStockModal">Cancel</button>
                    @if (stockModalTab == StockModalTab.Inventory && editingStock == null)
                    {
                        @if (importCandidates.Count > 0)
                        {
                            <button type="button" class="btn btn-primary" @onclick="ImportSelectedStockAsync"
                                    disabled="@(!importCandidates.Any(c => c.Selected))">
                                Import @importCandidates.Count(c => c.Selected) Item@(importCandidates.Count(c => c.Selected) != 1 ? "s" : "")
                            </button>
                        }
                    }
                    else if (stockModalTab == StockModalTab.Inventory && editingStock != null)
                    {
                        <button type="button" class="btn btn-primary" @onclick="SaveStockFromInventoryAsync">Save Changes</button>
                    }
                    else
                    {
                        <button type="button" class="btn btn-primary" @onclick="SaveCustomStockAsync">
                            @(editingStock == null ? "Add Stock" : "Save Changes")
                        </button>
                    }
                </div>
            </div>
        </div>
    </div>
}
```

Note this new block goes in the exact same position (between the "Part Modal Dialog" block and the `@code` block) that the old "Import Stock Modal" block occupied.

- [ ] **Step 9: Verify no leftover references to removed identifiers**

Run:
```bash
grep -nE "showStockForm|showCustomStockForm|showImportModal|ShowAddCustomStock|CancelStockForm|CloseImportModal\b|RenderStockFromInventoryForm|RenderCustomStockForm" CutList.Web/Components/Pages/Jobs/Edit.razor
```
Expected: no output (all removed/renamed identifiers are gone). If anything matches, fix it before proceeding — it's a leftover reference that Step 10's build may or may not catch (e.g. a stray comment).

- [ ] **Step 10: Build and verify no compile errors**

Run:
```bash
dotnet build CutList.Web/CutList.Web.csproj
```
Expected: `Build succeeded.` with 0 errors. Fix any errors (most likely causes: a missed rename, or a Razor `@`-prefix mismatch in the nested `@if` blocks from Step 8 — compare carefully against the nesting depth shown above, every nested C# control-flow keyword inside markup context needs its own `@` prefix).

- [ ] **Step 11: Commit**

```bash
git add CutList.Web/Components/Pages/Jobs/Edit.razor
git commit -m "$(cat <<'EOF'
feat: unify add/edit stock UI into a single modal on Job Edit page

Replaces the three separate surfaces (bulk import modal, inline custom-length
form, inline edit forms) with one "Add Stock" button and modal that handles
both add and edit, for both inventory-sourced and custom-length stock.
EOF
)"
```

---

### Task 2: Manual verification of every Stock tab flow

**Files:**
- None modified — this task is verification only, using the `webapp-testing` skill (Playwright) against the running app. Fix-forward edits to `CutList.Web/Components/Pages/Jobs/Edit.razor` are in scope if a scenario below reveals a bug.

**Interfaces:**
- Consumes: the running `CutList.Web` app (`dotnet run --project CutList.Web/CutList.Web.csproj`, default `http://localhost:5270`) and an existing or newly-created Job on the `/jobs/{id}` Edit page's Stock tab.

- [ ] **Step 1: Start the app**

Run (background):
```bash
dotnet run --project CutList.Web/CutList.Web.csproj
```
Expected: app listening on `http://localhost:5270`.

- [ ] **Step 2: Verify "Add Stock" opens the modal defaulting to the Inventory tab when parts exist**

Using Playwright (webapp-testing skill): navigate to a job that already has at least one part and no stock yet (create one via the Parts tab if none exists), switch to the Stock tab.

Expected: one "Add Stock" button (no separate "Import from Inventory" / "Add Custom Length" buttons). Clicking it opens a modal titled "Add Stock" with two tabs, "From Inventory" active by default, showing the bulk-select candidate table (or a "No matching inventory stock found" message if the material has no inventory stock items — either is acceptable, both are pre-existing behaviors).

- [ ] **Step 3: Verify bulk import from the Inventory tab**

If candidates are present in the table from Step 2 (add a Stock Item via the `/stock/new` page first for the job's material if the list is empty, then reopen the modal), select at least one row's checkbox and click "Import N Item(s)".

Expected: modal closes, the new stock row appears in the Stock table with the correct material, length, quantity, and priority.

- [ ] **Step 4: Verify Custom Length tab add flow**

Reopen "Add Stock", click the "Custom Length" tab, fill in Shape, Size, Length, Quantity, Priority, click "Add Stock".

Expected: modal closes, the new custom-length row appears in the Stock table tagged with the "Custom" badge.

- [ ] **Step 5: Verify the Inventory tab is disabled when the job has no parts**

Create a new job with no parts yet, go to its Stock tab, click "Add Stock".

Expected: modal opens defaulting to the "Custom Length" tab; the "From Inventory" tab is visibly disabled and shows the "Add parts first to match against inventory" tooltip on hover; clicking it does nothing.

- [ ] **Step 6: Verify editing an inventory-sourced stock row**

On a job with an inventory-sourced stock row (from Step 3), click its Edit (pencil) button.

Expected: modal opens titled "Edit Stock" with no tab nav, showing the single inventory form (Shape/Size/Stock Length dropdown/Qty/Priority) prefilled with the row's current values. Change the quantity and click "Save Changes".

Expected: modal closes, the table row reflects the updated quantity.

- [ ] **Step 7: Verify editing a custom-length stock row**

On the custom-length row (from Step 4), click its Edit button.

Expected: modal opens titled "Edit Stock" with no tab nav, showing the custom form (Shape/Size/Length/Qty/Priority) prefilled. Change the priority and click "Save Changes".

Expected: modal closes, the table row reflects the updated priority.

- [ ] **Step 8: Verify Cancel discards changes in both Add and Edit mode**

Open "Add Stock", change a few fields on the Custom Length tab, click "Cancel". Reopen and confirm the fields are reset (not retaining the discarded values). Then click Edit on any row, change a field, click "Cancel", and confirm the table row is unchanged.

- [ ] **Step 9: Verify locked jobs still hide the button**

Lock a job (via the Results tab's "Lock Job" action, after running Optimize), return to the Stock tab.

Expected: "Add Stock" button is not rendered (matches pre-existing `@if (!job.IsLocked)` gating, unchanged by this plan) and Edit/Delete icons on stock rows are hidden.

- [ ] **Step 10: Take a screenshot of the modal in both tab states for the record, then stop the app**

Capture a screenshot of the modal on the "From Inventory" tab and one on "Custom Length" tab (Playwright `browser_take_screenshot`). Stop the `dotnet run` background process.
