using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class PurchaseItemService
{
    private readonly ApplicationDbContext _context;

    public PurchaseItemService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PurchaseItem>> GetAllAsync(PurchaseItemStatus? status = null)
    {
        var query = _context.PurchaseItems
            .Include(p => p.StockItem)
                .ThenInclude(s => s.Material)
            .Include(p => p.Supplier)
            .Include(p => p.Job)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        return await query
            .OrderBy(p => p.Status)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<PurchaseItem?> GetByIdAsync(int id)
    {
        return await _context.PurchaseItems
            .Include(p => p.StockItem)
                .ThenInclude(s => s.Material)
            .Include(p => p.Supplier)
            .Include(p => p.Job)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<PurchaseItem> CreateAsync(PurchaseItem item)
    {
        item.CreatedAt = DateTime.UtcNow;
        _context.PurchaseItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task CreateBulkAsync(List<PurchaseItem> items)
    {
        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.CreatedAt = now;
        }
        _context.PurchaseItems.AddRange(items);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(PurchaseItem item)
    {
        item.UpdatedAt = DateTime.UtcNow;
        _context.PurchaseItems.Update(item);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(int id, PurchaseItemStatus status)
    {
        var item = await _context.PurchaseItems.FindAsync(id);
        if (item != null)
        {
            item.Status = status;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateSupplierAsync(int id, int? supplierId)
    {
        var item = await _context.PurchaseItems.FindAsync(id);
        if (item != null)
        {
            item.SupplierId = supplierId;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(int id)
    {
        var item = await _context.PurchaseItems.FindAsync(id);
        if (item != null)
        {
            _context.PurchaseItems.Remove(item);
            await _context.SaveChangesAsync();
        }
    }
}
