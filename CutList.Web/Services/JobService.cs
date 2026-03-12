using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class JobService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public JobService(IDbContextFactory<ApplicationDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<Job>> GetAllAsync()
    {
        await using var context = _factory.CreateDbContext();
        return await context.Jobs
            .Include(p => p.CuttingTool)
            .Include(p => p.Parts)
                .ThenInclude(pt => pt.Material)
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Job?> GetByIdAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        return await context.Jobs
            .Include(p => p.CuttingTool)
            .Include(p => p.Parts.OrderBy(pt => pt.SortOrder))
                .ThenInclude(pt => pt.Material)
            .Include(p => p.Stock.OrderBy(s => s.SortOrder))
                .ThenInclude(s => s.Material)
            .Include(p => p.Stock)
                .ThenInclude(s => s.StockItem)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Job> CreateAsync(Job? job = null)
    {
        await using var context = _factory.CreateDbContext();
        job ??= new Job();
        job.JobNumber = await GenerateJobNumberAsync(context);
        job.CreatedAt = DateTime.UtcNow;
        context.Jobs.Add(job);
        await context.SaveChangesAsync();
        return job;
    }

    public async Task<string> GenerateJobNumberAsync()
    {
        await using var context = _factory.CreateDbContext();
        return await GenerateJobNumberAsync(context);
    }

    private static async Task<string> GenerateJobNumberAsync(ApplicationDbContext context)
    {
        var maxNumber = await context.Jobs
            .Where(j => j.JobNumber.StartsWith("JOB-"))
            .Select(j => j.JobNumber)
            .MaxAsync() as string;

        if (maxNumber == null)
            return "JOB-00001";

        var numPart = maxNumber.Substring(4);
        if (int.TryParse(numPart, out var num))
            return $"JOB-{num + 1:D5}";

        return $"JOB-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    public async Task<Job> QuickCreateAsync(string? customer = null)
    {
        var job = new Job { Customer = customer };
        return await CreateAsync(job);
    }

    public async Task UpdateAsync(Job job)
    {
        await using var context = _factory.CreateDbContext();
        job.UpdatedAt = DateTime.UtcNow;
        job.OptimizationResultJson = null;
        job.OptimizedAt = null;
        context.Jobs.Update(job);
        await context.SaveChangesAsync();
    }

    public async Task LockAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        var job = await context.Jobs.FindAsync(id);
        if (job != null)
        {
            job.LockedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task UnlockAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        var job = await context.Jobs.FindAsync(id);
        if (job != null)
        {
            job.LockedAt = null;
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        var job = await context.Jobs.FindAsync(id);
        if (job != null)
        {
            context.Jobs.Remove(job);
            await context.SaveChangesAsync();
        }
    }

    public async Task<Job> DuplicateAsync(int id)
    {
        await using var context = _factory.CreateDbContext();

        var original = await context.Jobs
            .Include(p => p.CuttingTool)
            .Include(p => p.Parts.OrderBy(pt => pt.SortOrder))
                .ThenInclude(pt => pt.Material)
            .Include(p => p.Stock.OrderBy(s => s.SortOrder))
                .ThenInclude(s => s.Material)
            .Include(p => p.Stock)
                .ThenInclude(s => s.StockItem)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (original == null)
        {
            throw new ArgumentException("Job not found", nameof(id));
        }

        var duplicate = new Job
        {
            JobNumber = await GenerateJobNumberAsync(context),
            Name = string.IsNullOrWhiteSpace(original.Name) ? null : $"{original.Name} (Copy)",
            Customer = original.Customer,
            CuttingToolId = original.CuttingToolId,
            Notes = original.Notes,
            CreatedAt = DateTime.UtcNow
        };

        context.Jobs.Add(duplicate);
        await context.SaveChangesAsync();

        // Copy parts
        foreach (var part in original.Parts)
        {
            context.JobParts.Add(new JobPart
            {
                JobId = duplicate.Id,
                MaterialId = part.MaterialId,
                Name = part.Name,
                LengthInches = part.LengthInches,
                Quantity = part.Quantity,
                SortOrder = part.SortOrder
            });
        }

        // Copy stock selections
        foreach (var stock in original.Stock)
        {
            context.JobStocks.Add(new JobStock
            {
                JobId = duplicate.Id,
                MaterialId = stock.MaterialId,
                StockItemId = stock.StockItemId,
                LengthInches = stock.LengthInches,
                Quantity = stock.Quantity,
                IsCustomLength = stock.IsCustomLength,
                Priority = stock.Priority,
                SortOrder = stock.SortOrder
            });
        }

        await context.SaveChangesAsync();
        return duplicate;
    }

    // Optimization result persistence
    public async Task SaveOptimizationResultAsync(int jobId, string resultJson, DateTime optimizedAt)
    {
        await using var context = _factory.CreateDbContext();
        var job = await context.Jobs.FindAsync(jobId);
        if (job != null)
        {
            job.OptimizationResultJson = resultJson;
            job.OptimizedAt = optimizedAt;
            await context.SaveChangesAsync();
        }
    }

    public async Task ClearOptimizationResultAsync(int jobId)
    {
        await using var context = _factory.CreateDbContext();
        var job = await context.Jobs.FindAsync(jobId);
        if (job != null && job.OptimizationResultJson != null)
        {
            job.OptimizationResultJson = null;
            job.OptimizedAt = null;
            await context.SaveChangesAsync();
        }
    }

    // Parts management
    public async Task<JobPart> AddPartAsync(JobPart part)
    {
        await using var context = _factory.CreateDbContext();
        var maxOrder = await context.JobParts
            .Where(p => p.JobId == part.JobId)
            .MaxAsync(p => (int?)p.SortOrder) ?? -1;
        part.SortOrder = maxOrder + 1;

        context.JobParts.Add(part);
        await context.SaveChangesAsync();

        // Update job timestamp and clear stale results
        var job = await context.Jobs.FindAsync(part.JobId);
        if (job != null)
        {
            job.UpdatedAt = DateTime.UtcNow;
            job.OptimizationResultJson = null;
            job.OptimizedAt = null;
            await context.SaveChangesAsync();
        }

        return part;
    }

    public async Task UpdatePartAsync(JobPart part)
    {
        await using var context = _factory.CreateDbContext();
        context.JobParts.Update(part);
        await context.SaveChangesAsync();

        var job = await context.Jobs.FindAsync(part.JobId);
        if (job != null)
        {
            job.UpdatedAt = DateTime.UtcNow;
            job.OptimizationResultJson = null;
            job.OptimizedAt = null;
            await context.SaveChangesAsync();
        }
    }

    public async Task DeletePartAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        var part = await context.JobParts.FindAsync(id);
        if (part != null)
        {
            var jobId = part.JobId;
            context.JobParts.Remove(part);
            await context.SaveChangesAsync();

            var job = await context.Jobs.FindAsync(jobId);
            if (job != null)
            {
                job.UpdatedAt = DateTime.UtcNow;
                job.OptimizationResultJson = null;
                job.OptimizedAt = null;
                await context.SaveChangesAsync();
            }
        }
    }

    // Stock management
    public async Task<JobStock> AddStockAsync(JobStock stock)
    {
        await using var context = _factory.CreateDbContext();
        var maxOrder = await context.JobStocks
            .Where(s => s.JobId == stock.JobId)
            .MaxAsync(s => (int?)s.SortOrder) ?? -1;
        stock.SortOrder = maxOrder + 1;

        context.JobStocks.Add(stock);
        await context.SaveChangesAsync();

        var job = await context.Jobs.FindAsync(stock.JobId);
        if (job != null)
        {
            job.UpdatedAt = DateTime.UtcNow;
            job.OptimizationResultJson = null;
            job.OptimizedAt = null;
            await context.SaveChangesAsync();
        }

        return stock;
    }

    public async Task UpdateStockAsync(JobStock stock)
    {
        await using var context = _factory.CreateDbContext();
        context.JobStocks.Update(stock);
        await context.SaveChangesAsync();

        var job = await context.Jobs.FindAsync(stock.JobId);
        if (job != null)
        {
            job.UpdatedAt = DateTime.UtcNow;
            job.OptimizationResultJson = null;
            job.OptimizedAt = null;
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteStockAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        var stock = await context.JobStocks.FindAsync(id);
        if (stock != null)
        {
            var jobId = stock.JobId;
            context.JobStocks.Remove(stock);
            await context.SaveChangesAsync();

            var job = await context.Jobs.FindAsync(jobId);
            if (job != null)
            {
                job.UpdatedAt = DateTime.UtcNow;
                job.OptimizationResultJson = null;
                job.OptimizedAt = null;
                await context.SaveChangesAsync();
            }
        }
    }

    public async Task<List<StockItem>> GetAvailableStockForMaterialAsync(int materialId)
    {
        await using var context = _factory.CreateDbContext();
        return await context.StockItems
            .Include(s => s.Material)
            .Where(s => s.MaterialId == materialId && s.IsActive)
            .OrderBy(s => s.LengthInches)
            .ToListAsync();
    }

    // Cutting tools
    public async Task<List<CuttingTool>> GetCuttingToolsAsync(bool includeInactive = false)
    {
        await using var context = _factory.CreateDbContext();
        var query = context.CuttingTools.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }
        return await query.OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<CuttingTool?> GetCuttingToolByIdAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        return await context.CuttingTools.FindAsync(id);
    }

    public async Task<CuttingTool?> GetDefaultCuttingToolAsync()
    {
        await using var context = _factory.CreateDbContext();
        return await context.CuttingTools.FirstOrDefaultAsync(t => t.IsDefault && t.IsActive);
    }

    public async Task<CuttingTool> CreateCuttingToolAsync(CuttingTool tool)
    {
        await using var context = _factory.CreateDbContext();
        if (tool.IsDefault)
        {
            // Clear other defaults
            var others = await context.CuttingTools.Where(t => t.IsDefault).ToListAsync();
            foreach (var other in others)
            {
                other.IsDefault = false;
            }
        }

        context.CuttingTools.Add(tool);
        await context.SaveChangesAsync();
        return tool;
    }

    public async Task UpdateCuttingToolAsync(CuttingTool tool)
    {
        await using var context = _factory.CreateDbContext();
        if (tool.IsDefault)
        {
            var others = await context.CuttingTools.Where(t => t.IsDefault && t.Id != tool.Id).ToListAsync();
            foreach (var other in others)
            {
                other.IsDefault = false;
            }
        }

        context.CuttingTools.Update(tool);
        await context.SaveChangesAsync();
    }

    public async Task DeleteCuttingToolAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        var tool = await context.CuttingTools.FindAsync(id);
        if (tool != null)
        {
            tool.IsActive = false;
            await context.SaveChangesAsync();
        }
    }
}
