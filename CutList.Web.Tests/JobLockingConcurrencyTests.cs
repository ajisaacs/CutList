using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CutList.Web.Data.Entities;
using CutList.Web.DTOs;
using CutList.Web.Services;
using CutList.Web.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CutList.Web.Tests;

/// <summary>
/// Proves lock enforcement holds across interleavings: a lock committed after a mutation read the
/// unlocked job but before it saved wins, partial saves roll back, and nothing is replayed.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class JobLockingConcurrencyTests : IAsyncLifetime
{
    private static readonly DateTime CompetingLock =
        new DateTime(2026, 10, 3, 13, 0, 0, DateTimeKind.Utc).AddTicks(1357913);

    private readonly SqlServerFixture _db;
    private TestSeed _seed = null!;
    private AsyncServiceScope _scope;
    private JobService _jobs = null!;

    public JobLockingConcurrencyTests(SqlServerFixture db)
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
        _db.Factory.Sql.Reset();
        await _scope.DisposeAsync();
    }

    public enum RaceOp
    {
        UpdateHeader, Delete, AddPart, UpdatePart, DeletePart, AddStock, UpdateStock, DeleteStock, SaveResult, ClearResult
    }

    public static TheoryData<RaceOp> AllRaceOps => new(Enum.GetValues<RaceOp>());

    [Theory]
    [MemberData(nameof(AllRaceOps))]
    public async Task Lock_committed_between_read_and_save_wins_and_the_mutation_rolls_back(RaceOp op)
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        _db.Factory.SaveGate.BeforeNextSave(() => SetLockInSeparateContextAsync(_seed.UnlockedJobId, CompetingLock));
        _db.Factory.Sql.Start();

        var ex = await Assert.ThrowsAsync<JobLockedException>(() => RunAsync(op));

        Assert.Equal(1, _db.Factory.SaveGate.TriggeredCount);
        Assert.Equal(_seed.UnlockedJobId, ex.JobId);
        Assert.Equal(CompetingLock, ex.LockedAt);
        Assert.IsAssignableFrom<DbUpdateConcurrencyException>(ex.InnerException);
        JobSnapshot.AssertUnchanged(WithLock(before, _seed.UnlockedJobId, CompetingLock), await JobSnapshot.CaptureAsync(_db));

        // The rejected mutation was attempted exactly once: no automatic replay.
        Assert.Single(_db.Factory.Sql.Commands, IsJobWriteBatch);

        // Unlocking afterwards does not resurrect the rejected change.
        await _jobs.UnlockAsync(_seed.UnlockedJobId);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Mutations_send_LockedAt_in_the_job_UPDATE_and_DELETE_predicates()
    {
        _db.Factory.Sql.Start();
        await _jobs.UpdatePartAsync(new JobPart
        {
            Id = _seed.UnlockedPartId, JobId = _seed.UnlockedJobId, MaterialId = _seed.FlatBarMaterialId,
            Name = "Brace", LengthInches = 31m, Quantity = 8
        });
        var update = Assert.Single(_db.Factory.Sql.Commands, c => c.Contains("UPDATE [Jobs]"));
        Assert.Contains("UPDATE [JobParts]", update);
        Assert.Matches(@"UPDATE \[Jobs\][\s\S]*WHERE \[Id\] = @\w+ AND \[LockedAt\] IS NULL", update);

        await _jobs.UnlockAsync(_seed.LockedJobId);
        await _jobs.LockAsync(_seed.LockedJobId);
        await _jobs.UnlockAsync(_seed.LockedJobId);
        var unlock = _db.Factory.Sql.Commands.Last(c => c.Contains("UPDATE [Jobs]"));
        Assert.Matches(@"WHERE \[Id\] = @\w+ AND \[LockedAt\] = @\w+", unlock);

        _db.Factory.Sql.Start();
        await _jobs.DeleteAsync(_seed.UnlockedJobId);
        var delete = Assert.Single(_db.Factory.Sql.Commands, c => c.Contains("DELETE FROM [Jobs]"));
        Assert.Matches(@"WHERE \[Id\] = @\w+ AND \[LockedAt\] IS NULL", delete);
    }

    [Fact]
    public async Task Edit_committed_before_the_lock_is_kept_and_later_edits_are_blocked()
    {
        var part = new JobPart
        {
            JobId = _seed.UnlockedJobId, MaterialId = _seed.FlatBarMaterialId, Name = "Before lock",
            LengthInches = 12m, Quantity = 2
        };
        await _jobs.AddPartAsync(part);
        await _jobs.LockAsync(_seed.UnlockedJobId);
        var locked = await JobSnapshot.CaptureAsync(_db);

        Assert.Contains(locked.Parts, p => p.Id == part.Id && p.Name == "Before lock");
        Assert.NotNull(locked.Job(_seed.UnlockedJobId).LockedAt);

        await Assert.ThrowsAsync<JobLockedException>(() => _jobs.AddPartAsync(new JobPart
        {
            JobId = _seed.UnlockedJobId, MaterialId = _seed.FlatBarMaterialId, Name = "After lock",
            LengthInches = 12m, Quantity = 2
        }));
        await Assert.ThrowsAsync<JobLockedException>(() => _jobs.DeletePartAsync(part.Id));
        JobSnapshot.AssertUnchanged(locked, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Failure_after_child_and_parent_writes_rolls_both_back()
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        _db.Factory.Sql.FailAfterNextBatch(
            sql => sql.Contains("INSERT INTO [JobParts]") && sql.Contains("UPDATE [Jobs]"),
            evidenceSql:
                "N'inserted=', (SELECT COUNT(*) FROM [JobParts] WHERE [Name] = N'Never committed'), " +
                $"N'; cleared=', (SELECT COUNT(*) FROM [Jobs] WHERE [Id] = {_seed.UnlockedJobId} AND [OptimizationResultJson] IS NULL)");

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => _jobs.AddPartAsync(new JobPart
        {
            JobId = _seed.UnlockedJobId, MaterialId = _seed.FlatBarMaterialId, Name = "Never committed",
            LengthInches = 12m, Quantity = 2
        }));

        // The injected failure ran after both writes in the same batch, inside a transaction:
        // SQL Server saw the inserted part and the cleared result before the THROW.
        var faulted = _db.Factory.Sql.FaultedCommand;
        Assert.NotNull(faulted);
        Assert.True(faulted.IndexOf("INSERT INTO [JobParts]", StringComparison.Ordinal) < faulted.IndexOf("THROW 50001", StringComparison.Ordinal));
        Assert.True(faulted.IndexOf("UPDATE [Jobs]", StringComparison.Ordinal) < faulted.IndexOf("THROW 50001", StringComparison.Ordinal));
        Assert.Contains("Injected failure after job writes; inserted=1; cleared=1; trancount=1", ex.GetBaseException().Message);
        Assert.IsNotType<DbUpdateConcurrencyException>(ex);

        // ...and none of it survived.
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Unrelated_database_failure_is_not_reported_as_a_lock_conflict()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        // Nonexistent material: foreign-key violation, not a lock conflict.
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => _jobs.AddPartAsync(new JobPart
        {
            JobId = _seed.UnlockedJobId, MaterialId = int.MaxValue, Name = "Bad material", LengthInches = 1m, Quantity = 1
        }));

        Assert.IsNotType<DbUpdateConcurrencyException>(ex);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Concurrent_lock_requests_keep_the_first_lock_time()
    {
        _db.Factory.SaveGate.BeforeNextSave(() => SetLockInSeparateContextAsync(_seed.UnlockedJobId, CompetingLock));
        var before = await JobSnapshot.CaptureAsync(_db);

        await _jobs.LockAsync(_seed.UnlockedJobId);

        Assert.Equal(1, _db.Factory.SaveGate.TriggeredCount);
        JobSnapshot.AssertUnchanged(WithLock(before, _seed.UnlockedJobId, CompetingLock), await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Concurrent_unlock_requests_both_succeed()
    {
        _db.Factory.SaveGate.BeforeNextSave(() => SetLockInSeparateContextAsync(_seed.LockedJobId, null));
        var before = await JobSnapshot.CaptureAsync(_db);

        await _jobs.UnlockAsync(_seed.LockedJobId);

        Assert.Equal(1, _db.Factory.SaveGate.TriggeredCount);
        JobSnapshot.AssertUnchanged(WithLock(before, _seed.LockedJobId, null), await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Unlock_does_not_overwrite_a_newer_lock_from_another_session()
    {
        // Another session unlocks and re-locks (a new order) after this unlock read the old lock.
        _db.Factory.SaveGate.BeforeNextSave(async () =>
        {
            await SetLockInSeparateContextAsync(_seed.LockedJobId, null);
            await SetLockInSeparateContextAsync(_seed.LockedJobId, CompetingLock);
        });
        var before = await JobSnapshot.CaptureAsync(_db);

        var ex = await Assert.ThrowsAsync<JobMutationConflictException>(() => _jobs.UnlockAsync(_seed.LockedJobId));

        Assert.Equal(_seed.LockedJobId, ex.JobId);
        JobSnapshot.AssertUnchanged(WithLock(before, _seed.LockedJobId, CompetingLock), await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Rest_reports_a_lock_that_wins_the_race_as_job_locked()
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        _db.Factory.SaveGate.BeforeNextSave(() => SetLockInSeparateContextAsync(_seed.UnlockedJobId, CompetingLock));
        using var client = _db.Factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/jobs/{_seed.UnlockedJobId}/parts/{_seed.UnlockedPartId}", new UpdateJobPartDto { Quantity = 99 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("job_locked", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(new DateTimeOffset(CompetingLock), body.RootElement.GetProperty("lockedAt").GetDateTimeOffset());
        JobSnapshot.AssertUnchanged(WithLock(before, _seed.UnlockedJobId, CompetingLock), await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Rest_reports_a_job_removed_during_the_save_as_job_changed()
    {
        _db.Factory.SaveGate.BeforeNextSave(async () =>
        {
            await using var context = await _db.CreateContextAsync();
            await context.Jobs.Where(j => j.Id == _seed.UnlockedJobId).ExecuteDeleteAsync();
        });
        using var client = _db.Factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/jobs/{_seed.UnlockedJobId}/parts/{_seed.UnlockedPartId}", new UpdateJobPartDto { Quantity = 99 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal("Job changed", root.GetProperty("title").GetString());
        Assert.Equal("job_changed", root.GetProperty("code").GetString());
        Assert.Equal(_seed.UnlockedJobId, root.GetProperty("jobId").GetInt32());
        Assert.Contains("Reload the job", root.GetProperty("detail").GetString());
        Assert.False(root.TryGetProperty("lockedAt", out _));
    }

    private Task RunAsync(RaceOp op)
    {
        var jobId = _seed.UnlockedJobId;
        return op switch
        {
            RaceOp.UpdateHeader => _jobs.UpdateAsync(new Job { Id = jobId, Name = "Raced rename", Customer = "Raced", CuttingToolId = 4 }),
            RaceOp.Delete => _jobs.DeleteAsync(jobId),
            RaceOp.AddPart => ChildMutations.RunAsync(_jobs, _seed, ChildOp.AddPart, jobId, _seed.UnlockedPartId, _seed.UnlockedStockId),
            RaceOp.UpdatePart => ChildMutations.RunAsync(_jobs, _seed, ChildOp.UpdatePart, jobId, _seed.UnlockedPartId, _seed.UnlockedStockId),
            RaceOp.DeletePart => ChildMutations.RunAsync(_jobs, _seed, ChildOp.DeletePart, jobId, _seed.UnlockedPartId, _seed.UnlockedStockId),
            RaceOp.AddStock => ChildMutations.RunAsync(_jobs, _seed, ChildOp.AddStock, jobId, _seed.UnlockedPartId, _seed.UnlockedStockId),
            RaceOp.UpdateStock => ChildMutations.RunAsync(_jobs, _seed, ChildOp.UpdateStock, jobId, _seed.UnlockedPartId, _seed.UnlockedStockId),
            RaceOp.DeleteStock => ChildMutations.RunAsync(_jobs, _seed, ChildOp.DeleteStock, jobId, _seed.UnlockedPartId, _seed.UnlockedStockId),
            RaceOp.SaveResult => _jobs.SaveOptimizationResultAsync(jobId, "{\"raced\":true}", DateTime.UtcNow),
            RaceOp.ClearResult => _jobs.ClearOptimizationResultAsync(jobId),
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
        };
    }

    private static bool IsJobWriteBatch(string sql) =>
        sql.Contains("UPDATE [Jobs]") || sql.Contains("DELETE FROM [Jobs]");

    private async Task SetLockInSeparateContextAsync(int jobId, DateTime? lockedAt)
    {
        await using var context = await _db.CreateContextAsync();
        var rows = await context.Jobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.LockedAt, lockedAt));
        Assert.Equal(1, rows);
    }

    private static JobSnapshot WithLock(JobSnapshot snapshot, int jobId, DateTime? lockedAt) => snapshot with
    {
        Jobs = snapshot.Jobs
            .Select(j => j.Id == jobId
                ? j with { LockedAt = lockedAt is { } value ? DateTime.SpecifyKind(value, DateTimeKind.Unspecified) : null }
                : j)
            .ToList()
    };
}
