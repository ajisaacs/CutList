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
        job.JobNumber = CreateTemporaryJobNumber();
        job.CreatedAt = DateTime.UtcNow;
        context.Jobs.Add(job);
        await context.SaveChangesAsync();

        job.JobNumber = $"JOB-{job.Id}";
        await context.SaveChangesAsync();
        return job;
    }

    private static string CreateTemporaryJobNumber() => $"TMP-{Guid.NewGuid():N}"[..20];

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
            JobNumber = CreateTemporaryJobNumber(),
            Name = string.IsNullOrWhiteSpace(original.Name) ? null : $"{original.Name} (Copy)",
            Customer = original.Customer,
            CuttingToolId = original.CuttingToolId,
            Notes = original.Notes,
            CreatedAt = DateTime.UtcNow
        };

        context.Jobs.Add(duplicate);
        await context.SaveChangesAsync();

        duplicate.JobNumber = $"JOB-{duplicate.Id}";
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

    /// <summary>
    /// Adds a part to an unlocked job. Only the part's scalar fields are used; the generated
    /// <c>Id</c> and <c>SortOrder</c> are copied back to <paramref name="part"/> after saving.
    /// </summary>
    /// <exception cref="JobLockedException">The job is locked.</exception>
    /// <exception cref="KeyNotFoundException">The job does not exist.</exception>
    public async Task<JobPart> AddPartAsync(JobPart part)
    {
        await using var context = _factory.CreateDbContext();
        var job = await LoadUnlockedJobAsync(context, part.JobId);

        var maxOrder = await context.JobParts
            .Where(p => p.JobId == job.Id)
            .MaxAsync(p => (int?)p.SortOrder) ?? -1;

        var entity = new JobPart
        {
            JobId = job.Id,
            MaterialId = part.MaterialId,
            Name = part.Name,
            LengthInches = part.LengthInches,
            Quantity = part.Quantity,
            SortOrder = maxOrder + 1
        };
        context.JobParts.Add(entity);
        TouchJob(context, job);
        InvalidateOptimization(job);

        await SaveJobMutationAsync(context, job.Id);

        part.Id = entity.Id;
        part.SortOrder = entity.SortOrder;
        return part;
    }

    /// <summary>
    /// Updates a part's editable fields (material, name, length, quantity). The stored part's owner
    /// is authoritative: the owner must be unlocked and the incoming <c>JobId</c> must match it.
    /// </summary>
    /// <exception cref="JobLockedException">The owning job is locked.</exception>
    /// <exception cref="KeyNotFoundException">The part does not exist.</exception>
    /// <exception cref="ArgumentException">The part would be moved to a different job.</exception>
    public async Task UpdatePartAsync(JobPart part)
    {
        await using var context = _factory.CreateDbContext();
        var stored = await context.JobParts.FirstOrDefaultAsync(p => p.Id == part.Id)
            ?? throw new KeyNotFoundException($"Job part {part.Id} was not found.");
        var job = await LoadUnlockedJobAsync(context, stored.JobId);
        RequireSameOwner(stored.JobId, part.JobId, nameof(part));

        stored.MaterialId = part.MaterialId;
        stored.Name = part.Name;
        stored.LengthInches = part.LengthInches;
        stored.Quantity = part.Quantity;
        TouchJob(context, job);
        InvalidateOptimization(job);

        await SaveJobMutationAsync(context, job.Id);
    }

    /// <summary>Deletes a part from an unlocked job. Deleting a missing part is a no-op.</summary>
    /// <exception cref="JobLockedException">The owning job is locked.</exception>
    public async Task DeletePartAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        var stored = await context.JobParts.FirstOrDefaultAsync(p => p.Id == id);
        if (stored == null)
            return;

        var job = await LoadUnlockedJobAsync(context, stored.JobId);
        context.JobParts.Remove(stored);
        TouchJob(context, job);
        InvalidateOptimization(job);

        await SaveJobMutationAsync(context, job.Id);
    }

    // Stock management

    /// <summary>
    /// Adds stock to an unlocked job. Only the stock's scalar fields are used; the generated
    /// <c>Id</c> and <c>SortOrder</c> are copied back to <paramref name="stock"/> after saving.
    /// </summary>
    /// <exception cref="JobLockedException">The job is locked.</exception>
    /// <exception cref="KeyNotFoundException">The job does not exist.</exception>
    public async Task<JobStock> AddStockAsync(JobStock stock)
    {
        await using var context = _factory.CreateDbContext();
        var job = await LoadUnlockedJobAsync(context, stock.JobId);

        var maxOrder = await context.JobStocks
            .Where(s => s.JobId == job.Id)
            .MaxAsync(s => (int?)s.SortOrder) ?? -1;

        var entity = new JobStock
        {
            JobId = job.Id,
            MaterialId = stock.MaterialId,
            StockItemId = stock.StockItemId,
            LengthInches = stock.LengthInches,
            Quantity = stock.Quantity,
            IsCustomLength = stock.IsCustomLength,
            Priority = stock.Priority,
            SortOrder = maxOrder + 1
        };
        context.JobStocks.Add(entity);
        TouchJob(context, job);
        InvalidateOptimization(job);

        await SaveJobMutationAsync(context, job.Id);

        stock.Id = entity.Id;
        stock.SortOrder = entity.SortOrder;
        return stock;
    }

    /// <summary>
    /// Updates job stock's editable fields (material, stock item, length, quantity, custom flag,
    /// priority). The stored row's owner must be unlocked and match the incoming <c>JobId</c>.
    /// </summary>
    /// <exception cref="JobLockedException">The owning job is locked.</exception>
    /// <exception cref="KeyNotFoundException">The stock row does not exist.</exception>
    /// <exception cref="ArgumentException">The stock would be moved to a different job.</exception>
    public async Task UpdateStockAsync(JobStock stock)
    {
        await using var context = _factory.CreateDbContext();
        var stored = await context.JobStocks.FirstOrDefaultAsync(s => s.Id == stock.Id)
            ?? throw new KeyNotFoundException($"Job stock {stock.Id} was not found.");
        var job = await LoadUnlockedJobAsync(context, stored.JobId);
        RequireSameOwner(stored.JobId, stock.JobId, nameof(stock));

        stored.MaterialId = stock.MaterialId;
        stored.StockItemId = stock.StockItemId;
        stored.LengthInches = stock.LengthInches;
        stored.Quantity = stock.Quantity;
        stored.IsCustomLength = stock.IsCustomLength;
        stored.Priority = stock.Priority;
        TouchJob(context, job);
        InvalidateOptimization(job);

        await SaveJobMutationAsync(context, job.Id);
    }

    /// <summary>Deletes stock from an unlocked job. Deleting a missing row is a no-op.</summary>
    /// <exception cref="JobLockedException">The owning job is locked.</exception>
    public async Task DeleteStockAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        var stored = await context.JobStocks.FirstOrDefaultAsync(s => s.Id == id);
        if (stored == null)
            return;

        var job = await LoadUnlockedJobAsync(context, stored.JobId);
        context.JobStocks.Remove(stored);
        TouchJob(context, job);
        InvalidateOptimization(job);

        await SaveJobMutationAsync(context, job.Id);
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

    // Lock enforcement
    //
    // A locked job (materials ordered) is immutable until explicitly unlocked. Every job mutation
    // reads the persisted parent in its own context, rejects it if locked, and saves the parent
    // together with any child change in a single SaveChanges. Job.LockedAt is a concurrency token,
    // so a lock committed between the read and the save makes the parent UPDATE/DELETE affect zero
    // rows and the whole save rolls back.

    private static async Task<Job> LoadUnlockedJobAsync(ApplicationDbContext context, int jobId)
    {
        var job = await context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId)
            ?? throw new KeyNotFoundException($"Job {jobId} was not found.");
        RequireUnlocked(job);
        return job;
    }

    private static void RequireUnlocked(Job job)
    {
        if (job.LockedAt is DateTime lockedAt)
            throw new JobLockedException(job.Id, lockedAt);
    }

    private static void RequireSameOwner(int storedJobId, int incomingJobId, string paramName)
    {
        if (incomingJobId != storedJobId)
            throw new ArgumentException(
                $"Job children cannot be moved between jobs (stored job {storedJobId}, requested job {incomingJobId}).",
                paramName);
    }

    /// <summary>
    /// Stamps the job and forces its UPDATE so the LockedAt concurrency check runs in the same save.
    /// </summary>
    private static void TouchJob(ApplicationDbContext context, Job job)
    {
        job.UpdatedAt = DateTime.UtcNow;
        context.Entry(job).Property(j => j.UpdatedAt).IsModified = true;
    }

    private static void InvalidateOptimization(Job job)
    {
        job.OptimizationResultJson = null;
        job.OptimizedAt = null;
    }

    /// <summary>
    /// Saves a job mutation. If the job's lock state changed after it was read, the save has already
    /// rolled back; report the current state without retrying.
    /// </summary>
    private async Task SaveJobMutationAsync(ApplicationDbContext context, int jobId)
    {
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await using var fresh = _factory.CreateDbContext();
            var lockedAt = await fresh.Jobs.AsNoTracking()
                .Where(j => j.Id == jobId)
                .Select(j => j.LockedAt)
                .FirstOrDefaultAsync();

            if (lockedAt is DateTime currentLock)
                throw new JobLockedException(jobId, currentLock, ex);

            throw new JobMutationConflictException(jobId, ex);
        }
    }
}
