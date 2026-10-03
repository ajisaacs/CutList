using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CutList.Mcp;
using CutList.Web.DTOs;
using CutList.Web.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Tests;

/// <summary>
/// Exercises the real CutList.Mcp ApiClient and job tools against the in-process CutList.Web host.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class JobLockingMcpTests : IAsyncLifetime
{
    private const string LockedMessageStart = "is locked because materials have been ordered. Unlock it explicitly";

    private readonly SqlServerFixture _db;
    private TestSeed _seed = null!;
    private HttpClient _http = null!;
    private JobTools _tools = null!;

    public JobLockingMcpTests(SqlServerFixture db)
    {
        _db = db;
    }

    public async Task InitializeAsync()
    {
        _seed = await _db.ResetAsync();
        _http = _db.Factory.CreateClient();
        _tools = new JobTools(new ApiClient(_http));
    }

    public Task DisposeAsync()
    {
        _db.Factory.SaveGate.Reset();
        _http.Dispose();
        return Task.CompletedTask;
    }

    // --- Lock state is exposed read-only ---

    [Fact]
    public async Task Rest_list_and_detail_expose_lock_state()
    {
        var jobs = await _http.GetFromJsonAsync<List<JobDto>>("/api/jobs");
        var locked = Assert.Single(jobs!, j => j.Id == _seed.LockedJobId);
        var unlocked = Assert.Single(jobs!, j => j.Id == _seed.UnlockedJobId);
        Assert.True(locked.IsLocked);
        Assert.Equal(_seed.LockedAt, locked.LockedAt);
        Assert.False(unlocked.IsLocked);
        Assert.Null(unlocked.LockedAt);

        using var detail = JsonDocument.Parse(await _http.GetStringAsync($"/api/jobs/{_seed.LockedJobId}"));
        Assert.True(detail.RootElement.GetProperty("isLocked").GetBoolean());
        Assert.Equal(new DateTimeOffset(_seed.LockedAt), detail.RootElement.GetProperty("lockedAt").GetDateTimeOffset());

        using var openDetail = JsonDocument.Parse(await _http.GetStringAsync($"/api/jobs/{_seed.UnlockedJobId}"));
        Assert.False(openDetail.RootElement.GetProperty("isLocked").GetBoolean());
        Assert.Equal(JsonValueKind.Null, openDetail.RootElement.GetProperty("lockedAt").ValueKind);
    }

    [Fact]
    public async Task Mcp_list_and_get_job_project_lock_state()
    {
        var list = await _tools.ListJobs();
        Assert.True(list.Success);
        var locked = Assert.Single(list.Jobs, j => j.Id == _seed.LockedJobId);
        var unlocked = Assert.Single(list.Jobs, j => j.Id == _seed.UnlockedJobId);
        Assert.True(locked.IsLocked);
        Assert.Equal(_seed.LockedAt, locked.LockedAt);
        Assert.False(unlocked.IsLocked);
        Assert.Null(unlocked.LockedAt);

        var detail = await _tools.GetJob(_seed.LockedJobId);
        Assert.True(detail.Success);
        Assert.True(detail.Job!.IsLocked);
        Assert.Equal(_seed.LockedAt, detail.Job.LockedAt);

        var open = await _tools.GetJob(_seed.UnlockedJobId);
        Assert.False(open.Job!.IsLocked);
        Assert.Null(open.Job.LockedAt);
    }

    [Fact]
    public async Task Request_dtos_cannot_set_lock_state()
    {
        foreach (var type in new[] { typeof(CreateJobDto), typeof(UpdateJobDto), typeof(QuickCreateJobDto) })
        {
            Assert.Null(type.GetProperty("IsLocked"));
            Assert.Null(type.GetProperty("LockedAt"));
        }

        // A client-supplied lockedAt is ignored rather than locking or unlocking the job.
        var before = await JobSnapshot.CaptureAsync(_db);
        var put = await _http.PutAsync($"/api/jobs/{_seed.UnlockedJobId}", new StringContent(
            "{\"notes\":\"via api\",\"lockedAt\":\"2026-01-01T00:00:00Z\",\"isLocked\":true}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var after = await JobSnapshot.CaptureAsync(_db);
        Assert.Null(after.Job(_seed.UnlockedJobId).LockedAt);
        Assert.Equal("via api", after.Job(_seed.UnlockedJobId).Notes);

        var unlockAttempt = await _http.PutAsync($"/api/jobs/{_seed.LockedJobId}", new StringContent(
            "{\"notes\":\"unlock me\",\"lockedAt\":null,\"isLocked\":false}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Conflict, unlockAttempt.StatusCode);
        Assert.Equal(before.Job(_seed.LockedJobId), (await JobSnapshot.CaptureAsync(_db)).Job(_seed.LockedJobId));
    }

    // --- Mutation tools fail with the domain explanation ---

    public static TheoryData<string> MutationTools => new()
    {
        "update_job", "delete_job", "add_job_part", "add_job_parts", "delete_job_part", "add_job_stock", "delete_job_stock"
    };

    [Theory]
    [MemberData(nameof(MutationTools))]
    public async Task Mutation_tool_on_locked_job_fails_with_lock_explanation_and_changes_nothing(string tool)
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        var (success, error) = await InvokeMutationToolAsync(tool, _seed.LockedJobId, _seed.LockedPartId, _seed.LockedStockId);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Contains($"Job {_seed.LockedJobId} {LockedMessageStart}", error);
        Assert.DoesNotContain("does not indicate success", error);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Theory]
    [MemberData(nameof(MutationTools))]
    public async Task Mutation_tool_on_unlocked_job_still_succeeds(string tool)
    {
        var (success, error) = await InvokeMutationToolAsync(tool, _seed.UnlockedJobId, _seed.UnlockedPartId, _seed.UnlockedStockId);

        Assert.True(success, error);
        Assert.Null(error);
    }

    [Fact]
    public async Task Add_job_parts_on_locked_job_adds_nothing_and_reports_it()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        var result = await _tools.AddJobParts(_seed.LockedJobId, ThreeParts());

        Assert.False(result.Success);
        Assert.StartsWith("Added 0/3 parts.", result.Error);
        Assert.Contains("Stopped at 'First'", result.Error);
        Assert.Contains(LockedMessageStart, result.Error);
        Assert.True(result.Job!.IsLocked);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Add_job_parts_stops_when_the_job_is_locked_mid_batch_and_reports_actual_progress()
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        // Lock the job just before the second part's save commits.
        _db.Factory.SaveGate.BeforeNextSave(
            async () =>
            {
                await using var context = await _db.CreateContextAsync();
                await context.Jobs.Where(j => j.Id == _seed.UnlockedJobId)
                    .ExecuteUpdateAsync(s => s.SetProperty(j => j.LockedAt, DateTime.UtcNow));
            },
            when: ctx => ctx.ChangeTracker.Entries<CutList.Web.Data.Entities.JobPart>()
                .Any(e => e.State == EntityState.Added && e.Entity.Name == "Second"));

        var result = await _tools.AddJobParts(_seed.UnlockedJobId, ThreeParts());

        Assert.Equal(1, _db.Factory.SaveGate.TriggeredCount);
        Assert.False(result.Success);
        Assert.StartsWith("Added 1/3 parts.", result.Error);
        Assert.Contains("Stopped at 'Second'", result.Error);
        Assert.Contains(LockedMessageStart, result.Error);
        Assert.True(result.Job!.IsLocked);

        var after = await JobSnapshot.CaptureAsync(_db);
        var added = after.PartsOf(_seed.UnlockedJobId).Except(before.PartsOf(_seed.UnlockedJobId)).ToList();
        Assert.Equal(["First"], added.Select(p => p.Name));
        Assert.Equal(result.Job.Parts.Select(p => p.Id).Order(), after.PartsOf(_seed.UnlockedJobId).Select(p => p.Id).Order());
    }

    [Fact]
    public void Optimize_job_is_documented_as_a_non_persisting_preview()
    {
        var method = typeof(JobTools).GetMethod(nameof(JobTools.OptimizeJob))!;
        var description = method.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        Assert.Contains("does not save", description);
        Assert.Contains("locked", description);
    }

    [Fact]
    public async Task Optimize_job_previews_a_locked_job_without_changing_it()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        var result = await _tools.OptimizeJob(_seed.LockedJobId);

        Assert.True(result.Success, result.Error);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    // --- Other failures are unchanged ---

    [Fact]
    public async Task Missing_job_still_reports_not_found()
    {
        var result = await _tools.DeleteJob(999999);
        Assert.False(result.Success);
        Assert.Contains("404", result.Error);
    }

    [Fact]
    public async Task Duplicate_resource_conflicts_still_raise_ApiConflictException()
    {
        var api = new ApiClient(_http);
        var ex = await Assert.ThrowsAsync<ApiConflictException>(() =>
            api.CreateStockItemAsync(_seed.FlatBarMaterialId, "240", null, null));
        Assert.Contains("already exists", ex.Message);
    }

    [Theory]
    [InlineData("application/problem+json", "{\"title\":\"Conflict\",\"status\":409,\"code\":\"something_else\",\"detail\":\"secret internals\"}")]
    [InlineData("application/problem+json", "{not json")]
    [InlineData("application/problem+json", "{\"code\":\"job_locked\"}")]
    [InlineData("text/plain", "Job 1 is locked")]
    public async Task Unrecognized_conflict_bodies_fall_back_to_the_generic_error(string mediaType, string body)
    {
        using var stub = new HttpClient(new StubHandler(HttpStatusCode.Conflict, mediaType, body))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        var api = new ApiClient(stub);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => api.DeleteJobAsync(1));

        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Contains("409 (Conflict)", ex.Message);
        Assert.DoesNotContain("secret internals", ex.Message);
    }

    [Fact]
    public async Task Recognized_conflict_preserves_status_code_and_detail()
    {
        using var stub = new HttpClient(new StubHandler(HttpStatusCode.Conflict, "application/problem+json",
            "{\"title\":\"Job changed\",\"status\":409,\"code\":\"job_changed\",\"jobId\":7,\"detail\":\"Job 7 changed before your change could be saved. Reload the job and try again.\"}"))
        {
            BaseAddress = new Uri("http://localhost/")
        };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => new ApiClient(stub).DeleteJobAsync(7));

        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Equal("Job 7 changed before your change could be saved. Reload the job and try again. (job_changed)", ex.Message);
    }

    private PartEntry[] ThreeParts() =>
    [
        new() { MaterialId = _seed.FlatBarMaterialId, Name = "First", Length = "10", Quantity = 1 },
        new() { MaterialId = _seed.FlatBarMaterialId, Name = "Second", Length = "20", Quantity = 1 },
        new() { MaterialId = _seed.FlatBarMaterialId, Name = "Third", Length = "30", Quantity = 1 }
    ];

    private async Task<(bool Success, string? Error)> InvokeMutationToolAsync(string tool, int jobId, int partId, int stockId)
    {
        switch (tool)
        {
            case "update_job":
                var update = await _tools.UpdateJob(jobId, name: "MCP rename");
                return (update.Success, update.Error);
            case "delete_job":
                var delete = await _tools.DeleteJob(jobId);
                return (delete.Success, delete.Error);
            case "add_job_part":
                var part = await _tools.AddJobPart(jobId, _seed.FlatBarMaterialId, "MCP part", "12", 2);
                return (part.Success, part.Error);
            case "add_job_parts":
                var parts = await _tools.AddJobParts(jobId, ThreeParts());
                return (parts.Success, parts.Error);
            case "delete_job_part":
                var deletePart = await _tools.DeleteJobPart(jobId, partId);
                return (deletePart.Success, deletePart.Error);
            case "add_job_stock":
                var stock = await _tools.AddJobStock(jobId, _seed.FlatBarMaterialId, "96", isCustomLength: true);
                return (stock.Success, stock.Error);
            case "delete_job_stock":
                var deleteStock = await _tools.DeleteJobStock(jobId, stockId);
                return (deleteStock.Success, deleteStock.Error);
            default:
                throw new ArgumentOutOfRangeException(nameof(tool), tool, null);
        }
    }

    private sealed class StubHandler(HttpStatusCode status, string mediaType, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType)
            });
    }
}
