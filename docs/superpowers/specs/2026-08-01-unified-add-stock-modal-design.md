# Unified Add/Edit Stock Modal — Job Edit Page, Stock Tab

## Problem

The Stock tab on the Job Edit page (`CutList.Web/Components/Pages/Jobs/Edit.razor`) currently exposes three separate, inconsistent UI surfaces for adding/editing job stock:

1. **Import from Inventory** button → opens a full modal (`showImportModal`) with a bulk multi-select table (grouped by material, checkboxes, Select All/None, per-row qty/priority). Disabled entirely when the job has no parts.
2. **Add Custom Length** button → toggles an inline (non-modal) single-row form (`showCustomStockForm`) in the card body.
3. **Edit** (pencil icon on an existing stock row) → reuses one of two inline single-row forms depending on the item type: the bulk-import modal's sibling single form (`showStockForm`, inventory-sourced) or the custom form (`showCustomStockForm`), neither as a modal for the inventory case's add path (only reachable via Edit today).

This is confusing: two different buttons for adding, plus edit behavior that doesn't match either add flow.

## Goal

Consolidate all of this into a single **"Add Stock"** button and one modal that handles both adding and editing, for both inventory-sourced and custom-length stock.

## State Model

Replace the three separate visibility flags with:

- `showStockModal` (bool) — single modal visibility flag, replacing `showStockForm`, `showCustomStockForm`, and `showImportModal`.
- `stockModalTab` (enum: `Inventory`, `Custom`) — which tab is active. Only switchable in Add mode.
- `editingStock` (existing field) — `null` means Add mode; non-null means Edit mode, and fixes which tab is shown.

`newStock`, `importCandidates`, `stockErrorMessage`, `importErrorMessage`, `availableStockItems`, `stockSelectedShape`, `stockSelectedMaterialId` are all reused as-is from the current implementation.

## Header

The "Import from Inventory" and "Add Custom Length" buttons are replaced with a single **"Add Stock"** button. It is always enabled (not gated on `job.Parts.Count`). Clicking it:

- Sets `editingStock = null`.
- Opens the modal (`showStockModal = true`).
- Defaults `stockModalTab` to `Inventory` if `job.Parts.Count > 0`, otherwise `Custom`.

## Modal — Add Mode (`editingStock == null`)

Nav tabs shown: **From Inventory** | **Custom Length**.

- The **From Inventory** tab is disabled (with the existing tooltip copy, "Add parts first to match against inventory") when `job.Parts.Count == 0`. Its content is today's bulk-select table verbatim, reusing `importCandidates` and the existing `ShowImportModal` loading logic (grouped by material, Select All/None, per-row qty/priority inputs).
- The **Custom Length** tab content is today's single-row custom form verbatim (shape → size → length → qty → priority).
- Footer's primary button adapts to the active tab:
  - Inventory tab: "Import N Item(s)", disabled when no candidates are selected — wired to `ImportSelectedStockAsync`.
  - Custom tab: "Add Stock" — wired to `SaveCustomStockAsync`.
- "Cancel" button always present, closes the modal and resets `editingStock`, `newStock`, `importCandidates`, and error messages.

## Modal — Edit Mode (`editingStock != null`)

No tab nav is shown — a single fixed form matching the item's existing type:

- If `editingStock.IsCustomLength`: renders the same custom-form fields (shape → size → length → qty → priority), prefilled from the stock row — wired to `SaveCustomStockAsync`.
- Else: renders the same single-item inventory form fields (shape → size → stock-length dropdown → qty → priority), prefilled — wired to `SaveStockFromInventoryAsync`.
- Footer primary button: "Save Changes".
- "Cancel" button behaves as above.

`EditStock(stock)` sets `editingStock`, prefills `newStock` (as it does today), sets `stockModalTab` to match the item's type, and opens `showStockModal = true`.

## Method Reuse

The four existing handler methods keep their current logic unchanged — only the surrounding modal chrome (open/close state, which form renders) changes:

- `SaveStockFromInventoryAsync` — single inventory-sourced add/edit.
- `SaveCustomStockAsync` — single custom-length add/edit.
- `ImportSelectedStockAsync` — bulk inventory import (add only).
- `ShowImportModal` (the loading logic, not the modal-open flag itself) — populates `importCandidates` when the Inventory tab is active/opened in Add mode.

## Error Handling

Unchanged. Each tab/mode keeps its own existing inline `alert-danger` validation message (`stockErrorMessage` for the two single-item forms, `importErrorMessage` for the bulk table).

## Out of Scope

- No changes to the Parts tab or its modal.
- No changes to the underlying `JobService` methods or API surface.
- No change to validation rules or business logic — this is a UI consolidation only.
