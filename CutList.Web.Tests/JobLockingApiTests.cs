using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CutList.Web.DTOs;
using CutList.Web.Tests.Infrastructure;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class JobLockingApiTests : IAsyncLifetime
{
    private readonly SqlServerFixture _db;
    private TestSeed _seed = null!;
    private HttpClient _client = null!;

    public JobLockingApiTests(SqlServerFixture db)
    {
        _db = db;
    }

    public async Task InitializeAsync()
    {
        _seed = await _db.ResetAsync();
        _client = _db.Factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddPart_to_locked_job_returns_conflict_and_changes_nothing()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        var response = await _client.PostAsJsonAsync($"/api/jobs/{_seed.LockedJobId}/parts", new CreateJobPartDto
        {
            MaterialId = _seed.FlatBarMaterialId,
            Name = "Injected after order",
            Length = "36",
            Quantity = 3
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
        await AssertJobLockedProblemAsync(response, _seed.LockedJobId, _seed.LockedAt);
    }

    private static async Task AssertJobLockedProblemAsync(HttpResponseMessage response, int jobId, DateTime lockedAt)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal("Job is locked", root.GetProperty("title").GetString());
        Assert.Equal(409, root.GetProperty("status").GetInt32());
        Assert.Equal(
            $"Job {jobId} is locked because materials have been ordered. Unlock it explicitly before making changes.",
            root.GetProperty("detail").GetString());
        Assert.Equal("job_locked", root.GetProperty("code").GetString());
        Assert.Equal(jobId, root.GetProperty("jobId").GetInt32());
        Assert.Equal(new DateTimeOffset(lockedAt, TimeSpan.Zero), root.GetProperty("lockedAt").GetDateTimeOffset());
        Assert.False(root.TryGetProperty("exception", out _));
        Assert.DoesNotContain("   at ", root.GetRawText());
    }

    [Fact]
    public async Task AddPart_to_unlocked_job_adds_part_and_invalidates_saved_result()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        var response = await _client.PostAsJsonAsync($"/api/jobs/{_seed.UnlockedJobId}/parts", new CreateJobPartDto
        {
            MaterialId = _seed.FlatBarMaterialId,
            Name = "New upright",
            Length = "36",
            Quantity = 3
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<JobPartDto>();
        Assert.NotNull(dto);
        Assert.NotEqual(0, dto.Id);

        var after = await JobSnapshot.CaptureAsync(_db);
        var added = Assert.Single(after.Parts, p => p.Id == dto.Id);
        Assert.Equal(new PartRow(dto.Id, _seed.UnlockedJobId, _seed.FlatBarMaterialId, "New upright", 36m, 3, 1), added);
        Assert.Equal(before.Parts.Count + 1, after.Parts.Count);

        var job = after.Job(_seed.UnlockedJobId);
        Assert.Null(job.OptimizationResultJson);
        Assert.Null(job.OptimizedAt);
        Assert.Null(job.LockedAt);
        Assert.True(job.UpdatedAt > before.Job(_seed.UnlockedJobId).UpdatedAt);

        // The locked job is untouched by an edit to a different job.
        Assert.Equal(before.Job(_seed.LockedJobId), after.Job(_seed.LockedJobId));
    }
}
