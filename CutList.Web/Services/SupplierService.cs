using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class SupplierService
{
    private readonly ApplicationDbContext _context;

    public SupplierService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Supplier>> GetAllAsync(bool includeInactive = false)
    {
        var query = _context.Suppliers.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }
        return await query.OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<Supplier?> GetByIdAsync(int id)
    {
        return await _context.Suppliers
            .Include(s => s.Offerings)
            .ThenInclude(o => o.StockItem)
            .ThenInclude(si => si.Material)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Supplier> CreateAsync(Supplier supplier)
    {
        supplier.CreatedAt = DateTime.UtcNow;
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();
        return supplier;
    }

    public async Task UpdateAsync(Supplier supplier)
    {
        _context.Suppliers.Update(supplier);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier != null)
        {
            supplier.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }

    // Offering management
    public async Task<List<SupplierOffering>> GetOfferingsForSupplierAsync(int supplierId)
    {
        return await _context.SupplierOfferings
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
        return await _context.SupplierOfferings
            .Include(o => o.Supplier)
            .Where(o => o.StockItemId == stockItemId && o.IsActive && o.Supplier.IsActive)
            .OrderBy(o => o.Supplier.Name)
            .ToListAsync();
    }

    public async Task<SupplierOffering?> GetOfferingByIdAsync(int id)
    {
        return await _context.SupplierOfferings
            .Include(o => o.StockItem)
            .ThenInclude(si => si.Material)
            .Include(o => o.Supplier)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<SupplierOffering> AddOfferingAsync(SupplierOffering offering)
    {
        _context.SupplierOfferings.Add(offering);
        await _context.SaveChangesAsync();
        return offering;
    }

    public async Task UpdateOfferingAsync(SupplierOffering offering)
    {
        _context.SupplierOfferings.Update(offering);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteOfferingAsync(int id)
    {
        var offering = await _context.SupplierOfferings.FindAsync(id);
        if (offering != null)
        {
            offering.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> OfferingExistsAsync(int supplierId, int stockItemId, int? excludeId = null)
    {
        var query = _context.SupplierOfferings.Where(o =>
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
