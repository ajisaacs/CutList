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
