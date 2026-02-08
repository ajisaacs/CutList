using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class JobService
{
    private readonly ApplicationDbContext _context;

    public JobService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Job>> GetAllAsync()
    {
        return await _context.Jobs
            .Include(p => p.CuttingTool)
            .Include(p => p.Parts)
                .ThenInclude(pt => pt.Material)
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Job?> GetByIdAsync(int id)
    {
        return await _context.Jobs
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
        job ??= new Job();
        job.JobNumber = await GenerateJobNumberAsync();
        job.CreatedAt = DateTime.UtcNow;
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();
        return job;
    }

    public async Task<string> GenerateJobNumberAsync()
    {
        var maxNumber = await _context.Jobs
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
        job.UpdatedAt = DateTime.UtcNow;
        _context.Jobs.Update(job);
        await _context.SaveChangesAsync();
    }

    public async Task LockAsync(int id)
    {
        var job = await _context.Jobs.FindAsync(id);
        if (job != null)
        {
            job.LockedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task UnlockAsync(int id)
    {
        var job = await _context.Jobs.FindAsync(id);
        if (job != null)
        {
            job.LockedAt = null;
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(int id)
    {
        var job = await _context.Jobs.FindAsync(id);
        if (job != null)
        {
            _context.Jobs.Remove(job);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Job> DuplicateAsync(int id)
    {
        var original = await GetByIdAsync(id);
        if (original == null)
        {
            throw new ArgumentException("Job not found", nameof(id));
        }

        var duplicate = new Job
        {
            JobNumber = await GenerateJobNumberAsync(),
            Name = string.IsNullOrWhiteSpace(original.Name) ? null : $"{original.Name} (Copy)",
            Customer = original.Customer,
            CuttingToolId = original.CuttingToolId,
            Notes = original.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _context.Jobs.Add(duplicate);
        await _context.SaveChangesAsync();

        // Copy parts
        foreach (var part in original.Parts)
        {
            _context.JobParts.Add(new JobPart
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
            _context.JobStocks.Add(new JobStock
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

        await _context.SaveChangesAsync();
        return duplicate;
    }

    // Parts management
    public async Task<JobPart> AddPartAsync(JobPart part)
    {
        var maxOrder = await _context.JobParts
            .Where(p => p.JobId == part.JobId)
            .MaxAsync(p => (int?)p.SortOrder) ?? -1;
        part.SortOrder = maxOrder + 1;

        _context.JobParts.Add(part);
        await _context.SaveChangesAsync();

        // Update job timestamp
        var job = await _context.Jobs.FindAsync(part.JobId);
        if (job != null)
        {
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return part;
    }

    public async Task UpdatePartAsync(JobPart part)
    {
        _context.JobParts.Update(part);
        await _context.SaveChangesAsync();

        var job = await _context.Jobs.FindAsync(part.JobId);
        if (job != null)
        {
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeletePartAsync(int id)
    {
        var part = await _context.JobParts.FindAsync(id);
        if (part != null)
        {
            var jobId = part.JobId;
            _context.JobParts.Remove(part);
            await _context.SaveChangesAsync();

            var job = await _context.Jobs.FindAsync(jobId);
            if (job != null)
            {
                job.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }

    // Stock management
    public async Task<JobStock> AddStockAsync(JobStock stock)
    {
        var maxOrder = await _context.JobStocks
            .Where(s => s.JobId == stock.JobId)
            .MaxAsync(s => (int?)s.SortOrder) ?? -1;
        stock.SortOrder = maxOrder + 1;

        _context.JobStocks.Add(stock);
        await _context.SaveChangesAsync();

        var job = await _context.Jobs.FindAsync(stock.JobId);
        if (job != null)
        {
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return stock;
    }

    public async Task UpdateStockAsync(JobStock stock)
    {
        _context.JobStocks.Update(stock);
        await _context.SaveChangesAsync();

        var job = await _context.Jobs.FindAsync(stock.JobId);
        if (job != null)
        {
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteStockAsync(int id)
    {
        var stock = await _context.JobStocks.FindAsync(id);
        if (stock != null)
        {
            var jobId = stock.JobId;
            _context.JobStocks.Remove(stock);
            await _context.SaveChangesAsync();

            var job = await _context.Jobs.FindAsync(jobId);
            if (job != null)
            {
                job.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }

    public async Task<List<StockItem>> GetAvailableStockForMaterialAsync(int materialId)
    {
        return await _context.StockItems
            .Include(s => s.Material)
            .Where(s => s.MaterialId == materialId && s.IsActive)
            .OrderBy(s => s.LengthInches)
            .ToListAsync();
    }

    // Cutting tools
    public async Task<List<CuttingTool>> GetCuttingToolsAsync(bool includeInactive = false)
    {
        var query = _context.CuttingTools.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }
        return await query.OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<CuttingTool?> GetCuttingToolByIdAsync(int id)
    {
        return await _context.CuttingTools.FindAsync(id);
    }

    public async Task<CuttingTool?> GetDefaultCuttingToolAsync()
    {
        return await _context.CuttingTools.FirstOrDefaultAsync(t => t.IsDefault && t.IsActive);
    }

    public async Task<CuttingTool> CreateCuttingToolAsync(CuttingTool tool)
    {
        if (tool.IsDefault)
        {
            // Clear other defaults
            var others = await _context.CuttingTools.Where(t => t.IsDefault).ToListAsync();
            foreach (var other in others)
            {
                other.IsDefault = false;
            }
        }

        _context.CuttingTools.Add(tool);
        await _context.SaveChangesAsync();
        return tool;
    }

    public async Task UpdateCuttingToolAsync(CuttingTool tool)
    {
        if (tool.IsDefault)
        {
            var others = await _context.CuttingTools.Where(t => t.IsDefault && t.Id != tool.Id).ToListAsync();
            foreach (var other in others)
            {
                other.IsDefault = false;
            }
        }

        _context.CuttingTools.Update(tool);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteCuttingToolAsync(int id)
    {
        var tool = await _context.CuttingTools.FindAsync(id);
        if (tool != null)
        {
            tool.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }
}
