    using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class MaterialService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public MaterialService(IDbContextFactory<ApplicationDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<Material>> GetAllAsync(bool includeInactive = false)
    {
        await using var context = _factory.CreateDbContext();
        var query = context.Materials
            .Include(m => m.Dimensions)
            .AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(m => m.IsActive);
        }
        return await query.OrderBy(m => m.Shape).ThenBy(m => m.SortOrder).ThenBy(m => m.Size).ToListAsync();
    }

    public async Task<Material?> GetByIdAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        return await context.Materials
            .Include(m => m.Dimensions)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<Material> CreateAsync(Material material)
    {
        await using var context = _factory.CreateDbContext();
        material.CreatedAt = DateTime.UtcNow;
        context.Materials.Add(material);
        await context.SaveChangesAsync();
        return material;
    }

    /// <summary>
    /// Creates a material with dimensions and auto-generates the Size string from dimensions.
    /// </summary>
    public async Task<Material> CreateWithDimensionsAsync(Material material, MaterialDimensions dimensions)
    {
        await using var context = _factory.CreateDbContext();
        material.CreatedAt = DateTime.UtcNow;

        // Auto-generate Size string from dimensions if not provided
        if (string.IsNullOrWhiteSpace(material.Size))
        {
            material.Size = dimensions.GenerateSizeString();
        }

        // Set sort order from primary dimension
        material.SortOrder = dimensions.GetSortOrder();

        context.Materials.Add(material);
        await context.SaveChangesAsync();

        // Link dimensions to the created material
        dimensions.MaterialId = material.Id;
        context.MaterialDimensions.Add(dimensions);
        await context.SaveChangesAsync();

        material.Dimensions = dimensions;
        return material;
    }

    public async Task UpdateAsync(Material material)
    {
        await using var context = _factory.CreateDbContext();
        material.UpdatedAt = DateTime.UtcNow;
        context.Materials.Update(material);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Updates a material and its dimensions. Updates the Size string from dimensions if regenerateSize is true.
    /// </summary>
    public async Task UpdateWithDimensionsAsync(Material material, MaterialDimensions dimensions, bool regenerateSize = false)
    {
        await using var context = _factory.CreateDbContext();
        material.UpdatedAt = DateTime.UtcNow;

        if (regenerateSize)
        {
            material.Size = dimensions.GenerateSizeString();
        }

        // Update sort order from primary dimension
        material.SortOrder = dimensions.GetSortOrder();

        // Ensure the dimensions have the correct MaterialId
        dimensions.MaterialId = material.Id;

        // If the dimensions entity already has an Id (was loaded from DB), just mark it as modified
        // Otherwise, check if dimensions exist and handle appropriately
        if (dimensions.Id > 0)
        {
            // Already tracked, just save
            context.Entry(material).State = EntityState.Modified;
        }
        else
        {
            context.Materials.Update(material);

            // Check if dimensions already exist for this material
            var existingDimensions = await context.MaterialDimensions
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.MaterialId == material.Id);

            if (existingDimensions != null)
            {
                // Copy the existing Id to update in place
                dimensions.Id = existingDimensions.Id;
                context.MaterialDimensions.Update(dimensions);
            }
            else
            {
                context.MaterialDimensions.Add(dimensions);
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        var material = await context.Materials.FindAsync(id);
        if (material != null)
        {
            material.IsActive = false;
            await context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(MaterialShape shape, string size, int? excludeId = null)
    {
        await using var context = _factory.CreateDbContext();
        var query = context.Materials.Where(m => m.Shape == shape && m.Size == size && m.IsActive);
        if (excludeId.HasValue)
        {
            query = query.Where(m => m.Id != excludeId.Value);
        }
        return await query.AnyAsync();
    }

    /// <summary>
    /// Search for Round Bar materials by diameter with tolerance.
    /// </summary>
    public async Task<List<Material>> SearchRoundBarByDiameterAsync(decimal targetDiameter, decimal tolerance)
    {
        await using var context = _factory.CreateDbContext();
        var minValue = targetDiameter - tolerance;
        var maxValue = targetDiameter + tolerance;

        return await context.Set<RoundBarDimensions>()
            .Include(d => d.Material)
            .Where(d => d.Material.IsActive)
            .Where(d => d.Diameter >= minValue && d.Diameter <= maxValue)
            .Select(d => d.Material)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Size)
            .ToListAsync();
    }

    /// <summary>
    /// Search for Round Tube materials by outer diameter with tolerance.
    /// </summary>
    public async Task<List<Material>> SearchRoundTubeByODAsync(decimal targetOD, decimal tolerance)
    {
        await using var context = _factory.CreateDbContext();
        var minValue = targetOD - tolerance;
        var maxValue = targetOD + tolerance;

        return await context.Set<RoundTubeDimensions>()
            .Include(d => d.Material)
            .Where(d => d.Material.IsActive)
            .Where(d => d.OuterDiameter >= minValue && d.OuterDiameter <= maxValue)
            .Select(d => d.Material)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Size)
            .ToListAsync();
    }

    /// <summary>
    /// Search for Flat Bar materials by width with tolerance.
    /// </summary>
    public async Task<List<Material>> SearchFlatBarByWidthAsync(decimal targetWidth, decimal tolerance)
    {
        await using var context = _factory.CreateDbContext();
        var minValue = targetWidth - tolerance;
        var maxValue = targetWidth + tolerance;

        return await context.Set<FlatBarDimensions>()
            .Include(d => d.Material)
            .Where(d => d.Material.IsActive)
            .Where(d => d.Width >= minValue && d.Width <= maxValue)
            .Select(d => d.Material)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Size)
            .ToListAsync();
    }

    /// <summary>
    /// Search for Square Bar materials by size with tolerance.
    /// </summary>
    public async Task<List<Material>> SearchSquareBarBySizeAsync(decimal targetSize, decimal tolerance)
    {
        await using var context = _factory.CreateDbContext();
        var minValue = targetSize - tolerance;
        var maxValue = targetSize + tolerance;

        return await context.Set<SquareBarDimensions>()
            .Include(d => d.Material)
            .Where(d => d.Material.IsActive)
            .Where(d => d.Size >= minValue && d.Size <= maxValue)
            .Select(d => d.Material)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Size)
            .ToListAsync();
    }

    /// <summary>
    /// Search for Square Tube materials by size with tolerance.
    /// </summary>
    public async Task<List<Material>> SearchSquareTubeBySizeAsync(decimal targetSize, decimal tolerance)
    {
        await using var context = _factory.CreateDbContext();
        var minValue = targetSize - tolerance;
        var maxValue = targetSize + tolerance;

        return await context.Set<SquareTubeDimensions>()
            .Include(d => d.Material)
            .Where(d => d.Material.IsActive)
            .Where(d => d.Size >= minValue && d.Size <= maxValue)
            .Select(d => d.Material)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Size)
            .ToListAsync();
    }

    /// <summary>
    /// Search for Rectangular Tube materials by width with tolerance.
    /// </summary>
    public async Task<List<Material>> SearchRectangularTubeByWidthAsync(decimal targetWidth, decimal tolerance)
    {
        await using var context = _factory.CreateDbContext();
        var minValue = targetWidth - tolerance;
        var maxValue = targetWidth + tolerance;

        return await context.Set<RectangularTubeDimensions>()
            .Include(d => d.Material)
            .Where(d => d.Material.IsActive)
            .Where(d => d.Width >= minValue && d.Width <= maxValue)
            .Select(d => d.Material)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Size)
            .ToListAsync();
    }

    /// <summary>
    /// Search for Angle materials by leg size with tolerance.
    /// </summary>
    public async Task<List<Material>> SearchAngleByLegAsync(decimal targetLeg, decimal tolerance)
    {
        await using var context = _factory.CreateDbContext();
        var minValue = targetLeg - tolerance;
        var maxValue = targetLeg + tolerance;

        return await context.Set<AngleDimensions>()
            .Include(d => d.Material)
            .Where(d => d.Material.IsActive)
            .Where(d => d.Leg1 >= minValue && d.Leg1 <= maxValue)
            .Select(d => d.Material)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Size)
            .ToListAsync();
    }

    /// <summary>
    /// Search for Channel materials by height with tolerance.
    /// </summary>
    public async Task<List<Material>> SearchChannelByHeightAsync(decimal targetHeight, decimal tolerance)
    {
        await using var context = _factory.CreateDbContext();
        var minValue = targetHeight - tolerance;
        var maxValue = targetHeight + tolerance;

        return await context.Set<ChannelDimensions>()
            .Include(d => d.Material)
            .Where(d => d.Material.IsActive)
            .Where(d => d.Height >= minValue && d.Height <= maxValue)
            .Select(d => d.Material)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Size)
            .ToListAsync();
    }

    /// <summary>
    /// Search for I-Beam materials by height with tolerance.
    /// </summary>
    public async Task<List<Material>> SearchIBeamByHeightAsync(decimal targetHeight, decimal tolerance)
    {
        await using var context = _factory.CreateDbContext();
        var minValue = targetHeight - tolerance;
        var maxValue = targetHeight + tolerance;

        return await context.Set<IBeamDimensions>()
            .Include(d => d.Material)
            .Where(d => d.Material.IsActive)
            .Where(d => d.Height >= minValue && d.Height <= maxValue)
            .Select(d => d.Material)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Size)
            .ToListAsync();
    }

    /// <summary>
    /// Search for Pipe materials by nominal size with tolerance.
    /// </summary>
    public async Task<List<Material>> SearchPipeByNominalSizeAsync(decimal targetNPS, decimal tolerance)
    {
        await using var context = _factory.CreateDbContext();
        var minValue = targetNPS - tolerance;
        var maxValue = targetNPS + tolerance;

        return await context.Set<PipeDimensions>()
            .Include(d => d.Material)
            .Where(d => d.Material.IsActive)
            .Where(d => d.NominalSize >= minValue && d.NominalSize <= maxValue)
            .Select(d => d.Material)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Size)
            .ToListAsync();
    }

    /// <summary>
    /// Gets materials filtered by shape.
    /// </summary>
    public async Task<List<Material>> GetByShapeAsync(MaterialShape shape, bool includeInactive = false)
    {
        await using var context = _factory.CreateDbContext();
        var query = context.Materials
            .Include(m => m.Dimensions)
            .Where(m => m.Shape == shape);

        if (!includeInactive)
        {
            query = query.Where(m => m.IsActive);
        }

        return await query.OrderBy(m => m.SortOrder).ThenBy(m => m.Size).ToListAsync();
    }

    /// <summary>
    /// Creates the appropriate dimension object for a given shape.
    /// </summary>
    public static MaterialDimensions CreateDimensionsForShape(MaterialShape shape) => shape switch
    {
        MaterialShape.RoundBar => new RoundBarDimensions(),
        MaterialShape.RoundTube => new RoundTubeDimensions(),
        MaterialShape.FlatBar => new FlatBarDimensions(),
        MaterialShape.SquareBar => new SquareBarDimensions(),
        MaterialShape.SquareTube => new SquareTubeDimensions(),
        MaterialShape.RectangularTube => new RectangularTubeDimensions(),
        MaterialShape.Angle => new AngleDimensions(),
        MaterialShape.Channel => new ChannelDimensions(),
        MaterialShape.IBeam => new IBeamDimensions(),
        MaterialShape.Pipe => new PipeDimensions(),
        _ => throw new ArgumentException($"Unknown shape: {shape}")
    };
}
