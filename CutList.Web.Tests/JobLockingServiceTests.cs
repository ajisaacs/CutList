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
