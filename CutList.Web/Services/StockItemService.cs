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
}
