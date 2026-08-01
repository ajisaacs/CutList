using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class StockItemService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public StockItemService(IDbContextFactory<ApplicationDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<StockItem>> GetAllAsync(bool includeInactive = false)
    {
        await using var context = _factory.CreateDbContext();

        var query = context.StockItems
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
        await using var context = _factory.CreateDbContext();

        var query = context.StockItems
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
        await using var context = _factory.CreateDbContext();

        return await context.StockItems
            .Include(s => s.Material)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<StockItem> CreateAsync(StockItem stockItem)
    {
        await using var context = _factory.CreateDbContext();

        stockItem.CreatedAt = DateTime.UtcNow;
        context.StockItems.Add(stockItem);
        await context.SaveChangesAsync();
        return stockItem;
    }

    public async Task UpdateAsync(StockItem stockItem)
    {
        await using var context = _factory.CreateDbContext();

        stockItem.UpdatedAt = DateTime.UtcNow;
        context.StockItems.Update(stockItem);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var context = _factory.CreateDbContext();

        var stockItem = await context.StockItems.FindAsync(id);
        if (stockItem != null)
        {
            stockItem.IsActive = false;
            stockItem.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

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

    // Stock transaction methods
    public async Task<StockTransaction> AddStockAsync(int stockItemId, int quantity, string? notes = null)
    {
        await using var context = _factory.CreateDbContext();

        var stockItem = await context.StockItems.FindAsync(stockItemId)
            ?? throw new InvalidOperationException($"Stock item {stockItemId} not found");

        var transaction = new StockTransaction
        {
            StockItemId = stockItemId,
            Quantity = quantity,
            Type = StockTransactionType.Received,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        stockItem.QuantityOnHand += quantity;
        stockItem.UpdatedAt = DateTime.UtcNow;

        context.StockTransactions.Add(transaction);
        await context.SaveChangesAsync();

        return transaction;
    }

    public async Task<StockTransaction> UseStockAsync(int stockItemId, int quantity, int? jobId = null, string? notes = null)
    {
        await using var context = _factory.CreateDbContext();

        var stockItem = await context.StockItems.FindAsync(stockItemId)
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

        context.StockTransactions.Add(transaction);
        await context.SaveChangesAsync();

        return transaction;
    }

    public async Task<StockTransaction> AdjustStockAsync(int stockItemId, int newQuantity, string? notes = null)
    {
        await using var context = _factory.CreateDbContext();

        var stockItem = await context.StockItems.FindAsync(stockItemId)
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

        context.StockTransactions.Add(transaction);
        await context.SaveChangesAsync();

        return transaction;
    }

    public async Task<StockTransaction> ScrapStockAsync(int stockItemId, int quantity, string? notes = null)
    {
        await using var context = _factory.CreateDbContext();

        var stockItem = await context.StockItems.FindAsync(stockItemId)
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

        context.StockTransactions.Add(transaction);
        await context.SaveChangesAsync();

        return transaction;
    }

    public async Task<List<StockTransaction>> GetTransactionHistoryAsync(int stockItemId, int? limit = null)
    {
        await using var context = _factory.CreateDbContext();

        var query = context.StockTransactions
            .Include(t => t.Job)
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
        await using var context = _factory.CreateDbContext();

        var stockItem = await context.StockItems.FindAsync(stockItemId)
            ?? throw new InvalidOperationException($"Stock item {stockItemId} not found");

        var calculatedQuantity = await context.StockTransactions
            .Where(t => t.StockItemId == stockItemId)
            .SumAsync(t => t.Quantity);

        if (stockItem.QuantityOnHand != calculatedQuantity)
        {
            stockItem.QuantityOnHand = calculatedQuantity;
            stockItem.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        return calculatedQuantity;
    }
}
