using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class StockItemService
{
    private readonly ApplicationDbContext _context;

    public StockItemService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<StockItem>> GetAllAsync(bool includeInactive = false)
    {
        var query = _context.StockItems
            .Include(s => s.Material)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        return await query
            .OrderBy(s => s.Material.Shape)
            .ThenBy(s => s.Material.Size)
            .ThenBy(s => s.LengthInches)
            .ToListAsync();
    }

    public async Task<List<StockItem>> GetByMaterialAsync(int materialId, bool includeInactive = false)
    {
        var query = _context.StockItems
            .Include(s => s.Material)
            .Where(s => s.MaterialId == materialId);

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        return await query
            .OrderBy(s => s.LengthInches)
            .ToListAsync();
    }

    public async Task<StockItem?> GetByIdAsync(int id)
    {
        return await _context.StockItems
            .Include(s => s.Material)
            .Include(s => s.SupplierOfferings)
            .ThenInclude(o => o.Supplier)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<StockItem> CreateAsync(StockItem stockItem)
    {
        stockItem.CreatedAt = DateTime.UtcNow;
        _context.StockItems.Add(stockItem);
        await _context.SaveChangesAsync();
        return stockItem;
    }

    public async Task UpdateAsync(StockItem stockItem)
    {
        stockItem.UpdatedAt = DateTime.UtcNow;
        _context.StockItems.Update(stockItem);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var stockItem = await _context.StockItems.FindAsync(id);
        if (stockItem != null)
        {
            stockItem.IsActive = false;
            stockItem.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int materialId, decimal lengthInches, int? excludeId = null)
    {
        var query = _context.StockItems.Where(s =>
            s.MaterialId == materialId &&
            s.LengthInches == lengthInches &&
            s.IsActive);

        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }

    // Stock transaction methods
    public async Task<StockTransaction> AddStockAsync(int stockItemId, int quantity, int? supplierId = null, decimal? unitPrice = null, string? notes = null)
    {
        var stockItem = await _context.StockItems.FindAsync(stockItemId)
            ?? throw new InvalidOperationException($"Stock item {stockItemId} not found");

        var transaction = new StockTransaction
        {
            StockItemId = stockItemId,
            Quantity = quantity,
            Type = StockTransactionType.Received,
            SupplierId = supplierId,
            UnitPrice = unitPrice,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        stockItem.QuantityOnHand += quantity;
        stockItem.UpdatedAt = DateTime.UtcNow;

        _context.StockTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return transaction;
    }

    public async Task<decimal?> GetAverageCostAsync(int stockItemId)
    {
        var transactions = await _context.StockTransactions
            .Where(t => t.StockItemId == stockItemId && t.Type == StockTransactionType.Received && t.UnitPrice.HasValue)
            .ToListAsync();

        if (transactions.Count == 0)
            return null;

        var totalCost = transactions.Sum(t => t.Quantity * t.UnitPrice!.Value);
        var totalQty = transactions.Sum(t => t.Quantity);

        return totalQty > 0 ? totalCost / totalQty : null;
    }

    public async Task<decimal?> GetLastPurchasePriceAsync(int stockItemId)
    {
        return await _context.StockTransactions
            .Where(t => t.StockItemId == stockItemId && t.Type == StockTransactionType.Received && t.UnitPrice.HasValue)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => t.UnitPrice)
            .FirstOrDefaultAsync();
    }

    public async Task<StockTransaction> UseStockAsync(int stockItemId, int quantity, int? jobId = null, string? notes = null)
    {
        var stockItem = await _context.StockItems.FindAsync(stockItemId)
            ?? throw new InvalidOperationException($"Stock item {stockItemId} not found");

        var transaction = new StockTransaction
        {
            StockItemId = stockItemId,
            Quantity = -quantity,
            Type = StockTransactionType.Used,
            JobId = jobId,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        stockItem.QuantityOnHand -= quantity;
        stockItem.UpdatedAt = DateTime.UtcNow;

        _context.StockTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return transaction;
    }

    public async Task<StockTransaction> AdjustStockAsync(int stockItemId, int newQuantity, string? notes = null)
    {
        var stockItem = await _context.StockItems.FindAsync(stockItemId)
            ?? throw new InvalidOperationException($"Stock item {stockItemId} not found");

        var difference = newQuantity - stockItem.QuantityOnHand;

        var transaction = new StockTransaction
        {
            StockItemId = stockItemId,
            Quantity = difference,
            Type = StockTransactionType.Adjustment,
            Notes = notes ?? "Manual adjustment",
            CreatedAt = DateTime.UtcNow
        };

        stockItem.QuantityOnHand = newQuantity;
        stockItem.UpdatedAt = DateTime.UtcNow;

        _context.StockTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return transaction;
    }

    public async Task<StockTransaction> ScrapStockAsync(int stockItemId, int quantity, string? notes = null)
    {
        var stockItem = await _context.StockItems.FindAsync(stockItemId)
            ?? throw new InvalidOperationException($"Stock item {stockItemId} not found");

        var transaction = new StockTransaction
        {
            StockItemId = stockItemId,
            Quantity = -quantity,
            Type = StockTransactionType.Scrapped,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        stockItem.QuantityOnHand -= quantity;
        stockItem.UpdatedAt = DateTime.UtcNow;

        _context.StockTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return transaction;
    }

    public async Task<List<StockTransaction>> GetTransactionHistoryAsync(int stockItemId, int? limit = null)
    {
        var query = _context.StockTransactions
            .Include(t => t.Job)
            .Include(t => t.Supplier)
            .Where(t => t.StockItemId == stockItemId)
            .OrderByDescending(t => t.CreatedAt)
            .AsQueryable();

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<int> RecalculateQuantityAsync(int stockItemId)
    {
        var stockItem = await _context.StockItems.FindAsync(stockItemId)
            ?? throw new InvalidOperationException($"Stock item {stockItemId} not found");

        var calculatedQuantity = await _context.StockTransactions
            .Where(t => t.StockItemId == stockItemId)
            .SumAsync(t => t.Quantity);

        if (stockItem.QuantityOnHand != calculatedQuantity)
        {
            stockItem.QuantityOnHand = calculatedQuantity;
            stockItem.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return calculatedQuantity;
    }
}
