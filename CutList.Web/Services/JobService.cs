using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.Data.SqlClient;
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
            .Include(p => p.PartSetInstances)
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

    /// <summary>
    /// Updates an unlocked job's header fields (name, customer, cutting tool, notes) and clears its
    /// saved optimization result. Lock state, identifiers, timestamps, saved results, and child or
    /// navigation collections on <paramref name="job"/> are ignored; the persisted job is authoritative.
    /// </summary>
    /// <exception cref="JobLockedException">The persisted job is locked.</exception>
    /// <exception cref="KeyNotFoundException">The job does not exist.</exception>
    public async Task UpdateAsync(Job job)
    {
        await using var context = _factory.CreateDbContext();
        var stored = await LoadUnlockedJobAsync(context, job.Id);

        stored.Name = job.Name;
        stored.Customer = job.Customer;
        stored.CuttingToolId = job.CuttingToolId;
        stored.Notes = job.Notes;
        TouchJob(context, stored);
        InvalidateOptimization(stored);

        await SaveJobMutationAsync(context, stored.Id);
    }

    /// <summary>
    /// Locks the job (materials ordered). Idempotent: an already-locked job keeps its original lock
    /// time. Locking never changes the saved optimization result. Missing jobs are ignored.
    /// </summary>
    /// <exception cref="JobMutationConflictException">The job was deleted while being locked.</exception>
    public async Task LockAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        var job = await context.Jobs.FirstOrDefaultAsync(j => j.Id == id);
        if (job == null || job.LockedAt != null)
            return;

        job.LockedAt = DateTime.UtcNow;
        await SaveLockTransitionAsync(context, id, locked: true);
    }

    /// <summary>
    /// Explicitly unlocks the job so it can be edited again. The saved optimization result is kept;
    /// subsequent edits invalidate it as usual. Missing or already-unlocked jobs are ignored.
    /// </summary>
    /// <exception cref="JobMutationConflictException">
    /// The job was unlocked and re-locked (or deleted) by another session while being unlocked.
    /// </exception>
    public async Task UnlockAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        var job = await context.Jobs.FirstOrDefaultAsync(j => j.Id == id);
        if (job == null || job.LockedAt == null)
            return;

        job.LockedAt = null;
        await SaveLockTransitionAsync(context, id, locked: false);
    }

    /// <summary>
    /// Deletes an unlocked job and (by cascade) its parts and stock. Deleting a missing job is a no-op.
    /// </summary>
    /// <exception cref="JobLockedException">The job is locked.</exception>
    public async Task DeleteAsync(int id)
    {
        await using var context = _factory.CreateDbContext();
        var job = await context.Jobs.FirstOrDefaultAsync(j => j.Id == id);
        if (job == null)
            return;

        RequireUnlocked(job);
        context.Jobs.Remove(job);
        await SaveJobMutationAsync(context, job.Id);
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
            .Include(p => p.PartSetInstances)
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
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SaveChangesAsync();

        duplicate.JobNumber = $"JOB-{duplicate.Id}";
        await context.SaveChangesAsync();

        var instances = original.PartSetInstances.ToDictionary(i => i.Id, i => new JobPartSetInstance
        {
            JobId = duplicate.Id, PartSetId = i.PartSetId, DrawingNumber = i.DrawingNumber,
            Revision = i.Revision, DisplayName = i.DisplayName, Quantity = i.Quantity
        });
        context.JobPartSetInstances.AddRange(instances.Values);

        // Copy parts with new instance identities, never links to the source job.
        foreach (var part in original.Parts)
        {
            context.JobParts.Add(new JobPart
            {
                JobId = duplicate.Id,
                MaterialId = part.MaterialId,
                PartSetInstance = part.PartSetInstanceId is int instanceId ? instances[instanceId] : null,
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
        await transaction.CommitAsync();
        return duplicate;
    }

    // Optimization result persistence

    /// <summary>
    /// Saves the optimization result for an unlocked job. Missing jobs are ignored.
    /// </summary>
    /// <exception cref="JobLockedException">The job is locked (its ordered cut plan is preserved).</exception>
    public async Task SaveOptimizationResultAsync(int jobId, string resultJson, DateTime optimizedAt)
    {
        await using var context = _factory.CreateDbContext();
        var job = await context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
        if (job == null)
            return;

        RequireUnlocked(job);
        job.OptimizationResultJson = resultJson;
        job.OptimizedAt = optimizedAt;
        ForceJobUpdate(context, job);

        await SaveJobMutationAsync(context, job.Id);
    }

    /// <summary>
    /// Clears the saved optimization result for an unlocked job. Missing jobs and already-empty
    /// results are ignored.
    /// </summary>
    /// <exception cref="JobLockedException">The job is locked (even if its result is already empty).</exception>
    public async Task ClearOptimizationResultAsync(int jobId)
    {
        await using var context = _factory.CreateDbContext();
        var job = await context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
        if (job == null)
            return;

        RequireUnlocked(job);
        if (job.OptimizationResultJson == null && job.OptimizedAt == null)
            return;

        InvalidateOptimization(job);
        ForceJobUpdate(context, job);

        await SaveJobMutationAsync(context, job.Id);
    }

    /// <summary>Atomically captures a reusable set and its expanded cut rows in an unlocked job.</summary>
    public async Task<JobPartSetInstance> AddPartSetAsync(int jobId, int partSetId, int quantity)
    {
        await using var context = _factory.CreateDbContext();
        var job = await LoadUnlockedJobAsync(context, jobId);
        if (quantity < 1) throw new ArgumentException("Assembly quantity must be positive.", nameof(quantity));
        var set = await context.PartSets.AsNoTracking()
            .Include(s => s.Members.OrderBy(m => m.SortOrder).ThenBy(m => m.Id))
            .FirstOrDefaultAsync(s => s.Id == partSetId)
            ?? throw new KeyNotFoundException($"Part set {partSetId} was not found.");
        if (set.IsArchived || set.Members.Count == 0)
            throw new ArgumentException("An archived or empty part set cannot be added to a job.", nameof(partSetId));
        foreach (var member in set.Members) PartSetService.ValidateRow(member);
        await PartSetService.ValidateMaterialsAsync(context, set.Members);
        var maxOrder = await context.JobParts.Where(p => p.JobId == jobId)
            .MaxAsync(p => (int?)p.SortOrder) ?? -1;
        var instance = new JobPartSetInstance
        {
            JobId = jobId, PartSetId = set.Id, DrawingNumber = set.DrawingNumber,
            Revision = set.Revision, DisplayName = set.DisplayName, Quantity = quantity
        };
        try
        {
            foreach (var member in set.Members)
                instance.Parts.Add(new JobPart
                {
                    JobId = jobId, MaterialId = member.MaterialId, Name = member.Name,
                    LengthInches = member.LengthInches, Quantity = checked(member.Quantity * quantity),
                    SortOrder = maxOrder = checked(maxOrder + 1)
                });
        }
        catch (OverflowException ex)
        {
            throw new ArgumentException("Expanded quantities or sort orders exceed the supported integer range.", nameof(quantity), ex);
        }
        context.JobPartSetInstances.Add(instance);
        TouchJob(context, job);
        InvalidateOptimization(job);
        await SaveJobMutationAsync(context, jobId);
        return instance;
    }

    /// <summary>Removes only the specified job's whole instance and its snapshot rows in one save.</summary>
    public async Task RemovePartSetInstanceAsync(int jobId, int instanceId)
    {
        await using var context = _factory.CreateDbContext();
        var job = await LoadUnlockedJobAsync(context, jobId);
        var instance = await context.JobPartSetInstances.Include(i => i.Parts)
            .FirstOrDefaultAsync(i => i.Id == instanceId && i.JobId == jobId)
            ?? throw new KeyNotFoundException($"Part set instance {instanceId} was not found in job {jobId}.");
        context.JobParts.RemoveRange(instance.Parts);
        context.JobPartSetInstances.Remove(instance);
        TouchJob(context, job);
        InvalidateOptimization(job);
        await SaveJobMutationAsync(context, jobId);
    }

    private static void RequireManualPart(JobPart part)
    {
        if (part.PartSetInstanceId != null)
            throw new ArgumentException("Set-created parts cannot be edited or deleted individually; remove the whole part set instance instead.");
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
        RequireManualPart(stored);

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
        RequireManualPart(stored);
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
        ForceJobUpdate(context, job);
    }

    /// <summary>
    /// Marks the job modified without changing UpdatedAt, so EF always issues the guarded
    /// UPDATE ... WHERE LockedAt = @original even when every assigned value is unchanged.
    /// </summary>
    private static void ForceJobUpdate(ApplicationDbContext context, Job job)
    {
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
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            // SQL may insert children before the parent UPDATE. A concurrently deleted job
            // can therefore fail its FK before EF gets to the LockedAt concurrency check.
            // Translate only a proven missing owner; unrelated FK/check failures stay errors.
            await using var fresh = _factory.CreateDbContext();
            if (!await fresh.Jobs.AsNoTracking().AnyAsync(j => j.Id == jobId))
                throw new JobMutationConflictException(jobId, ex);
            throw;
        }
    }

    /// <summary>
    /// Saves an explicit lock/unlock. If another session changed the lock first, a job already in the
    /// requested state is treated as success; an opposite transition is reported, never overwritten.
    /// </summary>
    private async Task SaveLockTransitionAsync(ApplicationDbContext context, int jobId, bool locked)
    {
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await using var fresh = _factory.CreateDbContext();
            var current = await fresh.Jobs.AsNoTracking()
                .Where(j => j.Id == jobId)
                .Select(j => new { j.LockedAt })
                .FirstOrDefaultAsync();

            if (current != null && current.LockedAt.HasValue == locked)
                return;

            throw new JobMutationConflictException(jobId, ex);
        }
    }
}
