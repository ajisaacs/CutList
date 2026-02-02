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
            .Include(s => s.Stocks)
            .ThenInclude(st => st.Material)
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

    // Stock management
    public async Task<List<SupplierStock>> GetStocksForSupplierAsync(int supplierId)
    {
        return await _context.SupplierStocks
            .Include(s => s.Material)
            .Where(s => s.SupplierId == supplierId && s.IsActive)
            .OrderBy(s => s.Material.Shape)
            .ThenBy(s => s.Material.Size)
            .ThenBy(s => s.LengthInches)
            .ToListAsync();
    }

    public async Task<List<SupplierStock>> GetStocksForMaterialAsync(int materialId)
    {
        return await _context.SupplierStocks
            .Include(s => s.Supplier)
            .Where(s => s.MaterialId == materialId && s.IsActive && s.Supplier.IsActive)
            .OrderBy(s => s.Supplier.Name)
            .ThenBy(s => s.LengthInches)
            .ToListAsync();
    }

    public async Task<SupplierStock?> GetStockByIdAsync(int id)
    {
        return await _context.SupplierStocks
            .Include(s => s.Material)
            .Include(s => s.Supplier)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<SupplierStock> AddStockAsync(SupplierStock stock)
    {
        _context.SupplierStocks.Add(stock);
        await _context.SaveChangesAsync();
        return stock;
    }

    public async Task UpdateStockAsync(SupplierStock stock)
    {
        _context.SupplierStocks.Update(stock);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteStockAsync(int id)
    {
        var stock = await _context.SupplierStocks.FindAsync(id);
        if (stock != null)
        {
            stock.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> StockExistsAsync(int supplierId, int materialId, decimal lengthInches, int? excludeId = null)
    {
        var query = _context.SupplierStocks.Where(s =>
            s.SupplierId == supplierId &&
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
