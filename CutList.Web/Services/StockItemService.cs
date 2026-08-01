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
}
