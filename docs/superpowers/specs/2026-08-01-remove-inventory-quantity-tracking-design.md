# Remove inventory quantity tracking from CutList

## Background

`StockItem.QuantityOnHand` and its `StockTransaction` ledger (Received/Used/Adjustment/Scrapped/Returned) exist today, but nothing in the job workflow reads or writes them automatically:

- Adding a stock item to a job (`JobStock`) never checks or reserves `QuantityOnHand` — a user can put `Quantity = 50` on a `JobStock` row pointing at a `StockItem` with 3 on hand and nothing complains.
- Locking a job (`JobService.LockAsync`) only stamps `LockedAt` — no transaction is created, `QuantityOnHand` doesn't move.
- The only place `QuantityOnHand` has any real effect is `CutListPackingService.PackAsync`'s auto-discovery fallback: when a job has *no* `JobStock` rows configured for a material, the packer pulls all active `StockItem`s for that material, treats the first `QuantityOnHand` bars as free ("in stock"), and always adds an *additional unlimited* bin on top for "to be purchased" — i.e. it silently assumes more can always be bought.
- The only way `QuantityOnHand` actually changes is a fully separate, fully manual flow (`StockItemsController` receive/use/adjust/scrap, driven from the `/stock/{id}` page) that nothing in the Jobs UI ever triggers.

Net effect: inventory quantity is a second, disconnected bookkeeping system that the job workflow neither respects nor maintains, and the auto-discovery fallback actively assumes unlimited purchasing beyond whatever quantity happens to be tracked. Decision: remove quantity tracking entirely. A job's stock should be exactly what the user explicitly enters for that job — nothing assumed, nothing silently topped up — and the user should never need to think about a separate "inventory" system to enter parts, stock, and get results.

## Scope

### Data model

- `StockItem` drops `QuantityOnHand`. Keeps `MaterialId`, `LengthInches`, `Name`, `Notes`, `IsActive`, `CreatedAt`/`UpdatedAt` — it becomes a pure catalog of known lengths per material (still used to populate the "pick a standard length" dropdown when adding stock to a job).
- `StockTransaction` entity and `StockTransactionType` enum: **deleted entirely**, along with the `ApplicationDbContext.StockTransactions` `DbSet` and its EF configuration.
- `JobStock` is unchanged in shape. Its `Quantity` becomes the sole source of truth for how many bars are available to a job everywhere it's used: a finite number is a hard ceiling (parts beyond it land in `ItemsNotPlaced`, nothing is silently treated as purchasable), and `-1` (unlimited) only applies when the user explicitly picks it. This "unlimited" option is extended to catalog-sourced `JobStock` rows, which today only allow it for custom-length rows.

### Services / API

- `StockItemService` drops `AddStockAsync`, `UseStockAsync`, `AdjustStockAsync`, `ScrapStockAsync`, `GetTransactionHistoryAsync`, `RecalculateQuantityAsync`. Keeps `GetAllAsync`, `GetByMaterialAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `ExistsAsync`.
- `StockItemsController` drops `POST /{id}/receive`, `POST /{id}/use`, `POST /{id}/adjust`, `POST /{id}/scrap`, `POST /{id}/recalculate`, `GET /{id}/transactions`. Keeps list/get/create/update/delete/by-material.
- `StockItemDto` drops `QuantityOnHand`; `CreateStockItemDto` drops `QuantityOnHand`. `StockTransactionDto`, `AddStockDto`, `UseStockDto`, `AdjustStockDto`, `ScrapStockDto` are deleted.
- `CutListPackingService.PackAsync` drops the auto-discovery fallback branch entirely (the "no job-specific stock configured" path that reads `QuantityOnHand` and adds an unlimited purchasable bin). A material with no `JobStock` rows configured packs zero stock bins, so all of its parts land in `ItemsNotPlaced` — same code path as today's "no stock available" case. The `IsInStock` bin classification (`!IsCustomLength && StockItemId.HasValue`) is untouched, since it already doesn't depend on quantity.
- `CatalogService`: `CatalogStockItemDto` drops `QuantityOnHand`; `ImportStockItemsAsync`/`MapStockItems` stop reading/writing it. Existing seed JSON (`alro-catalog.json`, `oneals-catalog.json`) keeps the field in the file for now — it's simply ignored on import.
- `CutList.Mcp/InventoryTools.cs`: its own `StockItemDto` drops `QuantityOnHand`. `add_stock_item` drops the `quantityOnHand` parameter. `add_stock` convenience tool drops `quantityOnHand` and the `QuantityOnHand` field on `AddStockResult` — it becomes purely "ensure this material and stock length exist." `ApiClient` methods that pass `quantityOnHand` are updated to match. Republished to `~/.claude/mcp/CutList.Mcp/` per the standard MCP publishing workflow.

### UI

- `/stock` (Index): drop the "On Hand" column/badge. Intro copy changes from "tracks how many pieces you have on hand" to describing stock items as the lengths of material available to cut from.
- `/stock/{id}` (Edit): drop the entire right-hand "Inventory" card (quantity badge, Add/Adjust Stock transaction form, transaction history table). Left-hand details form (Material, Length, Name, Notes) is unchanged.
- Job Edit → Stock tab: `SaveStockFromInventoryAsync`'s quantity validation changes from `Quantity < 1` to `Quantity < -1 || Quantity == 0` (matching custom-stock validation), and the form gains an "Unlimited" option for catalog-sourced rows.
- Job Edit → Results tab: the existing "Items Not Placed" warning stays as-is structurally; its copy changes from "No stock lengths available or parts too long" to also cover insufficient configured quantity, since a finite `JobStock.Quantity` is now the only thing that can produce unplaced items for a material that does have stock configured.

### Migration

One new EF Core migration:
- Drops the `QuantityOnHand` column from `StockItems`.
- Drops the `StockTransactions` table.

This is destructive to any `QuantityOnHand`/`StockTransaction` data currently on forge (confirmed acceptable — nothing reads it automatically today, and it isn't otherwise relied upon). Applied immediately after `dotnet ef migrations add`, per the standard EF workflow, no separate confirmation gate beyond this design doc.

## Out of scope

- Any change to the packing algorithm itself (`CutList.Core`) — untouched, since bin selection already only consumed `StockBinSource.Quantity`/`IsInStock`, not `QuantityOnHand` directly.
- Reworking the "Purchase List" / `ToBePurchasedBins` reporting concept — it stays as a classification of catalog-sourced vs. custom-length bins for the print report, which doesn't depend on quantity tracking.
- Cleaning up `QuantityOnHand` values already present in `alro-catalog.json`/`oneals-catalog.json` seed files — harmless once the import path ignores the field.
