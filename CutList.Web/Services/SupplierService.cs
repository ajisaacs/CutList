using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class SupplierService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public SupplierService(IDbContextFactory<ApplicationDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<Supplier>> GetAllAsync(bool includeInactive = false)
    {
        await using var context = _factory.CreateDbContext();
        var query = context.Suppliers.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }
        return await query.OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<Supplier?> GetByIdAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        return await context.Suppliers
            .Include(s => s.Offerings)
            .ThenInclude(o => o.StockItem)
            .ThenInclude(si => si.Material)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Supplier> CreateAsync(Supplier supplier)
    {
        await using var context = _factory.CreateDbContext();
        supplier.CreatedAt = DateTime.UtcNow;
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();
        return supplier;
    }

    public async Task UpdateAsync(Supplier supplier)
    {
        await using var context = _factory.CreateDbContext();
        context.Suppliers.Update(supplier);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        var supplier = await context.Suppliers.FindAsync(id);
        if (supplier != null)
        {
            supplier.IsActive = false;
            await context.SaveChangesAsync();
        }
    }

    // Offering management
    public async Task<List<SupplierOffering>> GetOfferingsForSupplierAsync(int supplierId)
    {
        await using var context = _factory.CreateDbContext();
        return await context.SupplierOfferings
            .Include(o => o.StockItem)
            .ThenInclude(si => si.Material)
            .Where(o => o.SupplierId == supplierId && o.IsActive)
            .OrderBy(o => o.StockItem.Material.Shape)
            .ThenBy(o => o.StockItem.Material.Size)
            .ThenBy(o => o.StockItem.LengthInches)
            .ToListAsync();
    }

    public async Task<List<SupplierOffering>> GetOfferingsForStockItemAsync(int stockItemId)
    {
        await using var context = _factory.CreateDbContext();
        return await context.SupplierOfferings
            .Include(o => o.Supplier)
            .Where(o => o.StockItemId == stockItemId && o.IsActive && o.Supplier.IsActive)
            .OrderBy(o => o.Supplier.Name)
            .ToListAsync();
    }

    public async Task<SupplierOffering?> GetOfferingByIdAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        return await context.SupplierOfferings
            .Include(o => o.StockItem)
            .ThenInclude(si => si.Material)
            .Include(o => o.Supplier)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<SupplierOffering> AddOfferingAsync(SupplierOffering offering)
    {
        await using var context = _factory.CreateDbContext();
        context.SupplierOfferings.Add(offering);
        await context.SaveChangesAsync();
        return offering;
    }

    public async Task UpdateOfferingAsync(SupplierOffering offering)
    {
        await using var context = _factory.CreateDbContext();
        context.SupplierOfferings.Update(offering);
        await context.SaveChangesAsync();
    }

    public async Task DeleteOfferingAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        var offering = await context.SupplierOfferings.FindAsync(id);
        if (offering != null)
        {
            offering.IsActive = false;
            await context.SaveChangesAsync();
        }
    }

    public async Task<bool> OfferingExistsAsync(int supplierId, int stockItemId, int? excludeId = null)
    {
        await using var context = _factory.CreateDbContext();
        var query = context.SupplierOfferings.Where(o =>
            o.SupplierId == supplierId &&
            o.StockItemId == stockItemId &&
            o.IsActive);

        if (excludeId.HasValue)
        {
            query = query.Where(o => o.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
}
