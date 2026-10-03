using CutList.Web.Data;
using CutList.Web.Data.Entities;
using CutList.Web.Services;
using CutList.Web.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class JobLockingServiceTests : IAsyncLifetime
{
    private readonly SqlServerFixture _db;
    private TestSeed _seed = null!;
    private AsyncServiceScope _scope;
    private JobService _jobs = null!;

    public JobLockingServiceTests(SqlServerFixture db)
    {
        _db = db;
    }

    public async Task InitializeAsync()
    {
        _seed = await _db.ResetAsync();
        _scope = _db.Services.CreateAsyncScope();
        _jobs = _scope.ServiceProvider.GetRequiredService<JobService>();
    }

    public async Task DisposeAsync()
    {
        _db.Factory.SaveGate.Reset();
        await _scope.DisposeAsync();
    }

    [Fact]
    public async Task AddPart_rejects_locked_job_without_changing_anything()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        var ex = await Assert.ThrowsAsync<JobLockedException>(() => _jobs.AddPartAsync(new JobPart
        {
            JobId = _seed.LockedJobId,
            MaterialId = _seed.FlatBarMaterialId,
            Name = "Injected",
            LengthInches = 12m,
            Quantity = 2
        }));

        Assert.Equal(_seed.LockedJobId, ex.JobId);
        Assert.Equal(_seed.LockedAt, ex.LockedAt);
        Assert.Equal(DateTimeKind.Utc, ex.LockedAt.Kind);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task AddPart_on_unlocked_job_saves_scalars_only_and_returns_generated_identity()
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        var input = new JobPart
        {
            JobId = _seed.UnlockedJobId,
            MaterialId = _seed.RoundTubeMaterialId,
            Name = "Leg",
            LengthInches = 18.75m,
            Quantity = 4,
            SortOrder = 99,
            // A caller-supplied navigation graph must not be attached or written.
            Job = new Job { Id = _seed.LockedJobId, JobNumber = "FORGED", LockedAt = null },
            Material = new Material { Id = _seed.RoundTubeMaterialId, Size = "FORGED SIZE" }
        };

        var returned = await _jobs.AddPartAsync(input);

        Assert.Same(input, returned);
        Assert.NotEqual(0, input.Id);
        Assert.Equal(1, input.SortOrder);

        var after = await JobSnapshot.CaptureAsync(_db);
        Assert.Equal(
            new PartRow(input.Id, _seed.UnlockedJobId, _seed.RoundTubeMaterialId, "Leg", 18.75m, 4, 1),
            Assert.Single(after.Parts, p => p.Id == input.Id));
        Assert.Equal(before.Materials, after.Materials);
        Assert.Equal(before.Job(_seed.LockedJobId), after.Job(_seed.LockedJobId));

        var job = after.Job(_seed.UnlockedJobId);
        Assert.Null(job.OptimizationResultJson);
        Assert.Null(job.OptimizedAt);
        Assert.Null(job.LockedAt);
        Assert.Equal("JOB-SEED-OPEN", job.JobNumber);
    }

    [Fact]
    public async Task AddPart_rolls_back_when_job_is_locked_after_read_but_before_save()
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        var competingLock = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc).AddTicks(4242424);
        _db.Factory.SaveGate.BeforeNextSave(() => LockInSeparateContextAsync(_seed.UnlockedJobId, competingLock));

        var ex = await Assert.ThrowsAsync<JobLockedException>(() => _jobs.AddPartAsync(new JobPart
        {
            JobId = _seed.UnlockedJobId,
            MaterialId = _seed.FlatBarMaterialId,
            Name = "Raced part",
            LengthInches = 10m,
            Quantity = 1
        }));

        Assert.Equal(1, _db.Factory.SaveGate.TriggeredCount);
        Assert.Equal(_seed.UnlockedJobId, ex.JobId);
        Assert.Equal(competingLock, ex.LockedAt);
        Assert.IsAssignableFrom<DbUpdateConcurrencyException>(ex.InnerException);

        // Only the competing lock survives: no inserted part and the saved result is intact.
        // (SQL Server datetime2 values read back with DateTimeKind.Unspecified.)
        var persistedLock = DateTime.SpecifyKind(competingLock, DateTimeKind.Unspecified);
        var expected = before with
        {
            Jobs = before.Jobs
                .Select(j => j.Id == _seed.UnlockedJobId ? j with { LockedAt = persistedLock } : j)
                .ToList()
        };
        JobSnapshot.AssertUnchanged(expected, await JobSnapshot.CaptureAsync(_db));
    }

    // --- Child mutations (parts and stock) ---

    [Theory]
    [MemberData(nameof(ChildMutations.All), MemberType = typeof(ChildMutations))]
    public async Task Child_mutation_rejects_locked_job_without_changing_anything(ChildOp op)
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        var ex = await Assert.ThrowsAsync<JobLockedException>(() => ChildMutations.RunAsync(
            _jobs, _seed, op, _seed.LockedJobId, _seed.LockedPartId, _seed.LockedStockId));

        Assert.Equal(_seed.LockedJobId, ex.JobId);
        Assert.Equal(_seed.LockedAt, ex.LockedAt);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Theory]
    [MemberData(nameof(ChildMutations.All), MemberType = typeof(ChildMutations))]
    public async Task Child_mutation_on_unlocked_job_changes_only_that_child_and_invalidates_result(ChildOp op)
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        var newId = await ChildMutations.RunAsync(
            _jobs, _seed, op, _seed.UnlockedJobId, _seed.UnlockedPartId, _seed.UnlockedStockId);

        var after = await JobSnapshot.CaptureAsync(_db);
        var updatedAt = after.Job(_seed.UnlockedJobId).UpdatedAt;
        Assert.True(updatedAt > before.Job(_seed.UnlockedJobId).UpdatedAt);
        JobSnapshot.AssertUnchanged(
            ChildMutations.ExpectedAfterServiceRun(before, _seed, op, _seed.UnlockedJobId, _seed.UnlockedPartId,
                _seed.UnlockedStockId, newId, updatedAt),
            after);
    }

    [Theory]
    [MemberData(nameof(ChildMutations.All), MemberType = typeof(ChildMutations))]
    public async Task Child_mutation_succeeds_after_explicit_unlock_and_invalidates_result(ChildOp op)
    {
        await _jobs.UnlockAsync(_seed.LockedJobId);
        var before = await JobSnapshot.CaptureAsync(_db);
        Assert.Null(before.Job(_seed.LockedJobId).LockedAt);
        Assert.NotNull(before.Job(_seed.LockedJobId).OptimizationResultJson);

        var newId = await ChildMutations.RunAsync(
            _jobs, _seed, op, _seed.LockedJobId, _seed.LockedPartId, _seed.LockedStockId);

        var after = await JobSnapshot.CaptureAsync(_db);
        var updatedAt = after.Job(_seed.LockedJobId).UpdatedAt;
        Assert.True(updatedAt > before.Job(_seed.LockedJobId).UpdatedAt);
        JobSnapshot.AssertUnchanged(
            ChildMutations.ExpectedAfterServiceRun(before, _seed, op, _seed.LockedJobId, _seed.LockedPartId,
                _seed.LockedStockId, newId, updatedAt),
            after);
    }

    [Fact]
    public async Task UpdatePart_uses_stored_owner_not_a_forged_unlocked_job()
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        var forgedJob = new Job { Id = _seed.UnlockedJobId, JobNumber = "FORGED", LockedAt = null };

        var ex = await Assert.ThrowsAsync<JobLockedException>(() => _jobs.UpdatePartAsync(new JobPart
        {
            Id = _seed.LockedPartId,
            JobId = _seed.UnlockedJobId,
            Job = forgedJob,
            MaterialId = _seed.RoundTubeMaterialId,
            Material = new Material { Id = _seed.RoundTubeMaterialId, Size = "FORGED SIZE" },
            Name = "Moved to an open job",
            LengthInches = 1m,
            Quantity = 1
        }));

        Assert.Equal(_seed.LockedJobId, ex.JobId);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task UpdateStock_uses_stored_owner_not_a_forged_unlocked_job()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        var ex = await Assert.ThrowsAsync<JobLockedException>(() => _jobs.UpdateStockAsync(new JobStock
        {
            Id = _seed.LockedStockId,
            JobId = _seed.UnlockedJobId,
            Job = new Job { Id = _seed.UnlockedJobId, JobNumber = "FORGED" },
            MaterialId = _seed.FlatBarMaterialId,
            StockItemId = _seed.FlatBarStockItemId,
            StockItem = new StockItem { Id = _seed.FlatBarStockItemId, MaterialId = _seed.FlatBarMaterialId, LengthInches = 1m },
            LengthInches = 1m,
            Quantity = 1,
            Priority = 1
        }));

        Assert.Equal(_seed.LockedJobId, ex.JobId);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Child_updates_cannot_reparent_an_unlocked_child()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        await Assert.ThrowsAsync<ArgumentException>(() => _jobs.UpdatePartAsync(new JobPart
        {
            Id = _seed.UnlockedPartId, JobId = _seed.LockedJobId, MaterialId = _seed.FlatBarMaterialId,
            Name = "Reparented", LengthInches = 30m, Quantity = 8
        }));
        await Assert.ThrowsAsync<ArgumentException>(() => _jobs.UpdateStockAsync(new JobStock
        {
            Id = _seed.UnlockedStockId, JobId = _seed.LockedJobId, MaterialId = _seed.FlatBarMaterialId,
            StockItemId = _seed.FlatBarStockItemId, LengthInches = 240m, Quantity = 2, Priority = 10
        }));

        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task AddStock_ignores_forged_navigation_graph_and_checks_persisted_parent()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        // A forged unlocked Job navigation does not bypass the persisted lock.
        await Assert.ThrowsAsync<JobLockedException>(() => _jobs.AddStockAsync(new JobStock
        {
            JobId = _seed.LockedJobId,
            Job = new Job { Id = _seed.LockedJobId, JobNumber = "FORGED", LockedAt = null },
            MaterialId = _seed.FlatBarMaterialId,
            LengthInches = 100m,
            Quantity = 1,
            IsCustomLength = true
        }));
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));

        // On an unlocked job, forged catalog navigations are not written.
        var stock = new JobStock
        {
            JobId = _seed.UnlockedJobId,
            MaterialId = _seed.FlatBarMaterialId,
            Material = new Material { Id = _seed.FlatBarMaterialId, Size = "FORGED SIZE" },
            StockItemId = _seed.FlatBarStockItemId,
            StockItem = new StockItem { Id = _seed.FlatBarStockItemId, MaterialId = _seed.FlatBarMaterialId, LengthInches = 1m, Name = "FORGED" },
            LengthInches = 240m,
            Quantity = 6,
            Priority = 5
        };
        await _jobs.AddStockAsync(stock);

        var after = await JobSnapshot.CaptureAsync(_db);
        Assert.NotEqual(0, stock.Id);
        Assert.Equal(before.Materials, after.Materials);
        Assert.Equal(before.StockItems, after.StockItems);
        Assert.Equal(before.Job(_seed.LockedJobId), after.Job(_seed.LockedJobId));
    }

    [Fact]
    public async Task Deleting_a_missing_child_is_a_no_op_and_updating_one_is_not_found()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        await _jobs.DeletePartAsync(int.MaxValue);
        await _jobs.DeleteStockAsync(int.MaxValue);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _jobs.UpdatePartAsync(new JobPart
        {
            Id = int.MaxValue, JobId = _seed.UnlockedJobId, MaterialId = _seed.FlatBarMaterialId, Name = "x",
            LengthInches = 1m, Quantity = 1
        }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _jobs.UpdateStockAsync(new JobStock
        {
            Id = int.MaxValue, JobId = _seed.UnlockedJobId, MaterialId = _seed.FlatBarMaterialId,
            LengthInches = 1m, Quantity = 1
        }));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _jobs.AddStockAsync(new JobStock
        {
            JobId = int.MaxValue, MaterialId = _seed.FlatBarMaterialId, LengthInches = 1m, Quantity = 1
        }));

        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    // --- Header updates and deletion ---

    [Fact]
    public async Task UpdateAsync_rejects_locked_job_even_when_caller_clears_LockedAt()
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        var job = (await _jobs.GetByIdAsync(_seed.LockedJobId))!;
        job.Name = "Renamed after order";
        job.LockedAt = null; // forged/stale lock state from the caller

        var ex = await Assert.ThrowsAsync<JobLockedException>(() => _jobs.UpdateAsync(job));

        Assert.Equal(_seed.LockedJobId, ex.JobId);
        Assert.Equal(_seed.LockedAt, ex.LockedAt);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task UpdateAsync_with_a_view_read_before_another_session_locked_is_rejected()
    {
        var stale = (await _jobs.GetByIdAsync(_seed.UnlockedJobId))!;
        Assert.Null(stale.LockedAt);

        await using (var otherScope = _db.Services.CreateAsyncScope())
        {
            await otherScope.ServiceProvider.GetRequiredService<JobService>().LockAsync(_seed.UnlockedJobId);
        }

        var before = await JobSnapshot.CaptureAsync(_db);
        var lockedAt = before.Job(_seed.UnlockedJobId).LockedAt;
        Assert.NotNull(lockedAt);

        stale.Notes = "Edited in a stale editor";
        stale.LockedAt = null;
        var ex = await Assert.ThrowsAsync<JobLockedException>(() => _jobs.UpdateAsync(stale));

        Assert.Equal(lockedAt, ex.LockedAt);
        var after = await JobSnapshot.CaptureAsync(_db);
        JobSnapshot.AssertUnchanged(before, after);
        Assert.Equal("{\"seed\":\"unlocked cut plan\"}", after.Job(_seed.UnlockedJobId).OptimizationResultJson);
    }

    [Fact]
    public async Task UpdateAsync_on_unlocked_job_copies_only_the_header_fields()
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        var job = (await _jobs.GetByIdAsync(_seed.UnlockedJobId))!;

        // Editable header fields.
        job.Name = "Renamed rack";
        job.Customer = "New Customer";
        job.CuttingToolId = 3;
        job.Notes = "Updated notes";

        // Everything else on the incoming graph must be ignored.
        job.JobNumber = "JOB-HACKED";
        job.CreatedAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        job.UpdatedAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        job.LockedAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        job.OptimizationResultJson = "{\"forged\":true}";
        job.OptimizedAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        job.CuttingTool!.Name = "Renamed tool via navigation";
        job.CuttingTool.KerfInches = 9m;
        var part = job.Parts.Single();
        part.Name = "Edited via header graph";
        part.Quantity = 1000;
        part.Material.Size = "Edited material via graph";
        job.Parts.Add(new JobPart { MaterialId = _seed.FlatBarMaterialId, Name = "Added via header", LengthInches = 1m, Quantity = 1 });
        job.Stock.Single().Quantity = 1000;

        await _jobs.UpdateAsync(job);

        var after = await JobSnapshot.CaptureAsync(_db);
        var updated = after.Job(_seed.UnlockedJobId);
        Assert.True(updated.UpdatedAt > before.Job(_seed.UnlockedJobId).UpdatedAt);
        var expected = before with
        {
            Jobs = before.Jobs.Select(j => j.Id == _seed.UnlockedJobId
                ? j with
                {
                    Name = "Renamed rack", Customer = "New Customer", CuttingToolId = 3, Notes = "Updated notes",
                    UpdatedAt = updated.UpdatedAt, OptimizationResultJson = null, OptimizedAt = null
                }
                : j).ToList()
        };
        JobSnapshot.AssertUnchanged(expected, after);
    }

    [Fact]
    public async Task UpdateAsync_for_missing_job_is_not_found()
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _jobs.UpdateAsync(new Job { Id = int.MaxValue, Name = "Ghost" }));
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task DeleteAsync_rejects_locked_job_and_keeps_its_children()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        var ex = await Assert.ThrowsAsync<JobLockedException>(() => _jobs.DeleteAsync(_seed.LockedJobId));

        Assert.Equal(_seed.LockedJobId, ex.JobId);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task DeleteAsync_removes_unlocked_job_with_its_children_only()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        await _jobs.DeleteAsync(_seed.UnlockedJobId);
        await _jobs.DeleteAsync(int.MaxValue); // missing job remains a no-op

        var expected = before with
        {
            Jobs = before.Jobs.Where(j => j.Id != _seed.UnlockedJobId).ToList(),
            Parts = before.Parts.Where(p => p.JobId != _seed.UnlockedJobId).ToList(),
            Stock = before.Stock.Where(s => s.JobId != _seed.UnlockedJobId).ToList()
        };
        JobSnapshot.AssertUnchanged(expected, await JobSnapshot.CaptureAsync(_db));
    }

    // --- Optimization result persistence ---

    [Fact]
    public async Task SaveOptimizationResult_rejects_locked_job_including_identical_values()
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        var stored = before.Job(_seed.LockedJobId);

        await Assert.ThrowsAsync<JobLockedException>(() =>
            _jobs.SaveOptimizationResultAsync(_seed.LockedJobId, "{\"new\":\"provisional plan\"}", DateTime.UtcNow));
        await Assert.ThrowsAsync<JobLockedException>(() =>
            _jobs.SaveOptimizationResultAsync(_seed.LockedJobId, stored.OptimizationResultJson!, stored.OptimizedAt!.Value));

        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task ClearOptimizationResult_rejects_locked_job_including_an_already_empty_result()
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        await Assert.ThrowsAsync<JobLockedException>(() => _jobs.ClearOptimizationResultAsync(_seed.LockedJobId));
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));

        await using (var context = await _db.CreateContextAsync())
        {
            await context.Jobs.Where(j => j.Id == _seed.LockedJobId).ExecuteUpdateAsync(s => s
                .SetProperty(j => j.OptimizationResultJson, (string?)null)
                .SetProperty(j => j.OptimizedAt, (DateTime?)null));
        }

        var emptyBefore = await JobSnapshot.CaptureAsync(_db);
        await Assert.ThrowsAsync<JobLockedException>(() => _jobs.ClearOptimizationResultAsync(_seed.LockedJobId));
        JobSnapshot.AssertUnchanged(emptyBefore, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Optimization_result_save_and_clear_work_on_unlocked_job_without_touching_UpdatedAt()
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        var optimizedAt = new DateTime(2026, 10, 3, 9, 15, 0, DateTimeKind.Utc).AddTicks(9876543);

        await _jobs.SaveOptimizationResultAsync(_seed.UnlockedJobId, "{\"plan\":2}", optimizedAt);

        var saved = await JobSnapshot.CaptureAsync(_db);
        JobSnapshot.AssertUnchanged(before with
        {
            Jobs = before.Jobs.Select(j => j.Id == _seed.UnlockedJobId
                ? j with
                {
                    OptimizationResultJson = "{\"plan\":2}",
                    OptimizedAt = DateTime.SpecifyKind(optimizedAt, DateTimeKind.Unspecified)
                }
                : j).ToList()
        }, saved);

        await _jobs.ClearOptimizationResultAsync(_seed.UnlockedJobId);
        await _jobs.ClearOptimizationResultAsync(_seed.UnlockedJobId); // already empty: no-op

        JobSnapshot.AssertUnchanged(before with
        {
            Jobs = before.Jobs.Select(j => j.Id == _seed.UnlockedJobId
                ? j with { OptimizationResultJson = null, OptimizedAt = null }
                : j).ToList()
        }, await JobSnapshot.CaptureAsync(_db));
    }

    // --- Lock transitions ---

    [Fact]
    public async Task LockAsync_is_idempotent_and_preserves_the_original_lock_time_and_result()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        await _jobs.LockAsync(_seed.LockedJobId);
        await _jobs.LockAsync(_seed.LockedJobId);

        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task LockAsync_sets_lock_without_invalidating_result_and_UnlockAsync_preserves_result()
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        var startedAt = DateTime.UtcNow.AddSeconds(-1);

        await _jobs.LockAsync(_seed.UnlockedJobId);

        var locked = await JobSnapshot.CaptureAsync(_db);
        var lockedAt = locked.Job(_seed.UnlockedJobId).LockedAt;
        Assert.NotNull(lockedAt);
        Assert.True(lockedAt >= startedAt);
        JobSnapshot.AssertUnchanged(before with
        {
            Jobs = before.Jobs.Select(j => j.Id == _seed.UnlockedJobId ? j with { LockedAt = lockedAt } : j).ToList()
        }, locked);

        await _jobs.UnlockAsync(_seed.UnlockedJobId);
        await _jobs.UnlockAsync(_seed.LockedJobId);
        await _jobs.UnlockAsync(_seed.LockedJobId); // already unlocked: no-op

        JobSnapshot.AssertUnchanged(before with
        {
            Jobs = before.Jobs.Select(j => j with { LockedAt = null }).ToList()
        }, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Lock_and_unlock_of_missing_job_are_no_ops()
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        await _jobs.LockAsync(int.MaxValue);
        await _jobs.UnlockAsync(int.MaxValue);
        await _jobs.SaveOptimizationResultAsync(int.MaxValue, "{}", DateTime.UtcNow);
        await _jobs.ClearOptimizationResultAsync(int.MaxValue);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    // --- Allowed while locked ---

    [Fact]
    public async Task DuplicateAsync_copies_a_locked_job_into_a_new_unlocked_job_without_results()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        var duplicate = await _jobs.DuplicateAsync(_seed.LockedJobId);

        var after = await JobSnapshot.CaptureAsync(_db);
        Assert.NotEqual(_seed.LockedJobId, duplicate.Id);
        var copy = after.Job(duplicate.Id);
        var source = before.Job(_seed.LockedJobId);
        Assert.Equal($"JOB-{duplicate.Id}", copy.JobNumber);
        Assert.Equal($"{source.Name} (Copy)", copy.Name);
        Assert.Equal(source.Customer, copy.Customer);
        Assert.Equal(source.CuttingToolId, copy.CuttingToolId);
        Assert.Equal(source.Notes, copy.Notes);
        Assert.Null(copy.LockedAt);
        Assert.Null(copy.OptimizationResultJson);
        Assert.Null(copy.OptimizedAt);

        Assert.Equal(
            before.PartsOf(_seed.LockedJobId).Select(p => p with { Id = 0, JobId = 0 }),
            after.PartsOf(duplicate.Id).Select(p => p with { Id = 0, JobId = 0 }));
        Assert.Equal(
            before.StockOf(_seed.LockedJobId).Select(s => s with { Id = 0, JobId = 0 }),
            after.StockOf(duplicate.Id).Select(s => s with { Id = 0, JobId = 0 }));
        Assert.DoesNotContain(after.PartsOf(duplicate.Id), p => before.Parts.Any(bp => bp.Id == p.Id));

        // The locked source and everything else are untouched.
        JobSnapshot.AssertUnchanged(before, after with
        {
            Jobs = after.Jobs.Where(j => j.Id != duplicate.Id).ToList(),
            Parts = after.Parts.Where(p => p.JobId != duplicate.Id).ToList(),
            Stock = after.Stock.Where(s => s.JobId != duplicate.Id).ToList()
        });
    }

    [Fact]
    public async Task LockedAt_is_a_concurrency_token_on_the_job_model()
    {
        await using var context = await _db.CreateContextAsync();
        var property = context.Model.FindEntityType(typeof(Job))!.FindProperty(nameof(Job.LockedAt))!;
        Assert.True(property.IsConcurrencyToken);
    }

    private async Task LockInSeparateContextAsync(int jobId, DateTime lockedAt)
    {
        await using var context = await _db.CreateContextAsync();
        var rows = await context.Jobs
            .Where(j => j.Id == jobId && j.LockedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.LockedAt, lockedAt));
        Assert.Equal(1, rows);
    }
}
