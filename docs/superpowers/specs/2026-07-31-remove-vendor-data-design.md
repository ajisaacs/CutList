# Remove vendor/supplier data from CutList

## Background

CutList's stated purpose is a 1D bin-packing optimizer: given a parts list and available stock, compute an efficient cutting plan. Supplier/vendor management (contacts, part numbers, pricing, purchase orders) was built alongside it but never touched by the packing engine itself (`AdvancedFitEngine`, `MultiBinEngine`, `CutListPackingService` never reference `Supplier`). It only entered through inventory-management pages.

This surfaced concretely in the Alro Steel catalog scraper: every scraped stock item was wrapped in a `supplierOfferings` array with empty `partNumber`/`supplierDescription` placeholders (Alro's SmartGrid never exposes part numbers), making it needlessly verbose to add or change a stock length. Investigating further surfaced the actual reported bug — a user (Carrol) couldn't figure out how to change an existing stock item's length, because `Stock/Edit.razor` hard-locks the Length field to `readonly` outside of creation.

Decision: fix the length-editing bug, and separately, pull vendor/purchasing concepts out of CutList entirely. Procurement (who to buy from, at what price, PO status) is a different domain than nesting optimization. If a "what should we order" feature is needed later, it can live in a layer above CutList that queries the existing Materials/StockItems REST API (`MaterialsController`, `StockItemsController`) to compare `JobStock` requirements against `StockItem.QuantityOnHand` — nothing in this change forecloses that.

## Scope

### Kept in CutList (nesting/inventory-relevant)

- **`StockItem.QuantityOnHand`** — becomes directly editable via a stripped-down `StockTransaction` log:
  - Keeps: `Quantity`, `Type` (`Received`/`Used`/`Adjustment`/`Scrapped`; `Returned` is dead code today — never constructed anywhere, only referenced in a UI badge-color switch — and stays dead, out of scope to touch), `JobId`, `Notes`, `CreatedAt`.
  - Drops: `SupplierId`, `UnitPrice`, and the `Supplier` navigation.
  - `StockItemService` keeps `AddStockAsync/UseStockAsync/AdjustStockAsync/ScrapStockAsync/GetTransactionHistoryAsync/RecalculateQuantityAsync`, with `supplierId`/`unitPrice` parameters removed from `AddStockAsync`.
  - Drops `GetAverageCostAsync`/`GetLastPurchasePriceAsync` and `StockPricingDto` entirely — there's no non-vendor version of "cost."
- **`Job.LockedAt`/`IsLocked`** — kept. Becomes a direct manual Lock/Unlock action instead of a side effect of "Add to Order List." Still production-relevant: don't let someone edit a parts list after material's been committed/cut against it.
- **Bug fix**: `Stock/Edit.razor`'s Length field becomes editable in both create and edit modes. The backend already supports this — `StockItemService.ExistsAsync` already excludes the current row's ID for the edit case in its `(MaterialId, LengthInches)` uniqueness check — so this is a front-end-only fix.

### Removed entirely (procurement layer, not CutList's job)

- **Entities**: `Supplier`, `SupplierOffering`, `PurchaseItem`.
- **Services/Controllers/DTOs**: `SupplierService`, `PurchaseItemService`, `SuppliersController`, `SupplierDtos.cs`. `StockItemsController` drops its `/offerings` and `/pricing` endpoints.
- **Pages**: `Suppliers/Index.razor`, `Suppliers/Edit.razor`, `Orders/Index.razor`, `Orders/Add.razor`. `NavMenu.razor` loses the Suppliers and Orders links. `Home.razor` loses the Suppliers dashboard card and supplier/order mentions in the getting-started copy.
- **`Jobs/Edit.razor`**: "Add to Order List" button (which created `PurchaseItem`s and locked the job) is replaced with a plain "Lock Job" button that calls `JobService.LockAsync` directly. Drops the `PurchaseItemService` dependency and bulk-create logic.
- **Catalog import/export**: `CatalogDtos.cs` drops `CatalogSupplierDto`, `CatalogSupplierOfferingDto`, `CatalogData.Suppliers`, and `CatalogStockItemDto.SupplierOfferings`. `CatalogService` drops `ImportSuppliersAsync` and all offering-import logic. Stock items in the catalog format become `{ lengthInches, quantityOnHand }`.
- **Seed data**: `alro-catalog.json` already regenerated without vendor wrappers. `oneals-catalog.json` gets the same treatment via a one-off transform script (not checked in as a permanent tool) — it currently has *real* O'Neal part numbers/prices, which will be discarded along with the empty Alro placeholders, for consistency.
- **`CutList.Mcp`**: removes `list_suppliers`, `add_supplier`, `list_supplier_offerings`, `add_supplier_offering`, `add_stock_with_offering` (tools, DTOs, and the corresponding `ApiClient` methods). Adds a plain `add_stock` tool: find-or-create material → find-or-create stock item → set quantity — same convenience flow as `add_stock_with_offering` minus the offering step. Republished to `~/.claude/mcp/CutList.Mcp/` per the standard MCP publishing workflow.

### Migration

One new EF Core migration:
- Drops `Suppliers`, `SupplierOfferings`, `PurchaseItems` tables.
- Drops `SupplierId`/`UnitPrice` columns and the associated FK from `StockTransactions`.

This is destructive to whatever currently lives in those tables (e.g., any real Supplier/Offering/PurchaseItem rows in the dev DB). Applied immediately after creation per the usual EF workflow, no separate confirmation gate beyond this design doc.

## Out of scope

- Building the "layer above" that would query stock levels for purchasing decisions — not needed now, and the existing Materials/StockItems REST API already exposes what such a layer would need later.
- Any change to the packing algorithm itself (`CutList.Core`) — untouched by this work, confirming it never depended on vendor data in the first place.
- Removing the dead `StockTransactionType.Returned` enum value — unused, harmless, unrelated to vendor data.
