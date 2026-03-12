using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class PurchaseItemService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public PurchaseItemService(IDbContextFactory<ApplicationDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<PurchaseItem>> GetAllAsync(PurchaseItemStatus? status = null)
    {
        await using var context = _factory.CreateDbContext();

        var query = context.PurchaseItems
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
        await using var context = _factory.CreateDbContext();

        return await context.PurchaseItems
            .Include(p => p.StockItem)
                .ThenInclude(s => s.Material)
            .Include(p => p.Supplier)
            .Include(p => p.Job)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<PurchaseItem> CreateAsync(PurchaseItem item)
    {
        await using var context = _factory.CreateDbContext();

        item.CreatedAt = DateTime.UtcNow;
        context.PurchaseItems.Add(item);
        await context.SaveChangesAsync();
        return item;
    }

    public async Task CreateBulkAsync(List<PurchaseItem> items)
    {
        await using var context = _factory.CreateDbContext();

        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.CreatedAt = now;
        }
        context.PurchaseItems.AddRange(items);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(PurchaseItem item)
    {
        await using var context = _factory.CreateDbContext();

        item.UpdatedAt = DateTime.UtcNow;
        context.PurchaseItems.Update(item);
        await context.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(int id, PurchaseItemStatus status)
    {
        await using var context = _factory.CreateDbContext();

        var item = await context.PurchaseItems.FindAsync(id);
        if (item != null)
        {
            item.Status = status;
            item.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateSupplierAsync(int id, int? supplierId)
    {
        await using var context = _factory.CreateDbContext();

        var item = await context.PurchaseItems.FindAsync(id);
        if (item != null)
        {
            item.SupplierId = supplierId;
            item.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(int id)
    {
        await using var context = _factory.CreateDbContext();

        var item = await context.PurchaseItems.FindAsync(id);
        if (item != null)
        {
            context.PurchaseItems.Remove(item);
            await context.SaveChangesAsync();
        }
    }
}
