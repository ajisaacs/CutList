using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class MaterialService
{
    private readonly ApplicationDbContext _context;

    public static readonly string[] CommonShapes =
    {
        "Round Tube",
        "Square Tube",
        "Rectangular Tube",
        "Angle",
        "Channel",
        "Flat Bar",
        "Round Bar",
        "Square Bar",
        "I-Beam",
        "Pipe"
    };

    public MaterialService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Material>> GetAllAsync(bool includeInactive = false)
    {
        var query = _context.Materials.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(m => m.IsActive);
        }
        return await query.OrderBy(m => m.Shape).ThenBy(m => m.Size).ToListAsync();
    }

    public async Task<Material?> GetByIdAsync(int id)
    {
        return await _context.Materials.FindAsync(id);
    }

    public async Task<Material> CreateAsync(Material material)
    {
        material.CreatedAt = DateTime.UtcNow;
        _context.Materials.Add(material);
        await _context.SaveChangesAsync();
        return material;
    }

    public async Task UpdateAsync(Material material)
    {
        material.UpdatedAt = DateTime.UtcNow;
        _context.Materials.Update(material);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var material = await _context.Materials.FindAsync(id);
        if (material != null)
        {
            material.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string shape, string size, int? excludeId = null)
    {
        var query = _context.Materials.Where(m => m.Shape == shape && m.Size == size && m.IsActive);
        if (excludeId.HasValue)
        {
            query = query.Where(m => m.Id != excludeId.Value);
        }
        return await query.AnyAsync();
    }
}
