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

    // --- Part and stock routes ---

    [Theory]
    [MemberData(nameof(ChildMutations.All), MemberType = typeof(ChildMutations))]
    public async Task Child_route_on_locked_job_returns_job_locked_problem_and_changes_nothing(ChildOp op)
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        var response = await SendChildMutationAsync(op, _seed.LockedJobId, _seed.LockedPartId, _seed.LockedStockId);

        await AssertJobLockedProblemAsync(response, _seed.LockedJobId, _seed.LockedAt);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Theory]
    [MemberData(nameof(ChildMutations.All), MemberType = typeof(ChildMutations))]
    public async Task Child_route_on_unlocked_job_succeeds_and_invalidates_saved_result(ChildOp op)
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        var response = await SendChildMutationAsync(op, _seed.UnlockedJobId, _seed.UnlockedPartId, _seed.UnlockedStockId);

        var expectedStatus = op switch
        {
            ChildOp.AddPart or ChildOp.AddStock => HttpStatusCode.Created,
            ChildOp.DeletePart or ChildOp.DeleteStock => HttpStatusCode.NoContent,
            _ => HttpStatusCode.OK
        };
        Assert.Equal(expectedStatus, response.StatusCode);

        if (op == ChildOp.AddStock)
        {
            var dto = await response.Content.ReadFromJsonAsync<JobStockDto>();
            Assert.NotNull(dto);
            Assert.NotEqual(0, dto.Id);
            Assert.Equal(_seed.UnlockedJobId, dto.JobId);
        }

        var after = await JobSnapshot.CaptureAsync(_db);
        var job = after.Job(_seed.UnlockedJobId);
        Assert.Null(job.OptimizationResultJson);
        Assert.Null(job.OptimizedAt);
        Assert.True(job.UpdatedAt > before.Job(_seed.UnlockedJobId).UpdatedAt);
        Assert.Equal(before.Job(_seed.LockedJobId), after.Job(_seed.LockedJobId));
        Assert.Equal(before.PartsOf(_seed.LockedJobId), after.PartsOf(_seed.LockedJobId));
        Assert.Equal(before.StockOf(_seed.LockedJobId), after.StockOf(_seed.LockedJobId));

        var (partDelta, stockDelta) = op switch
        {
            ChildOp.AddPart => (1, 0),
            ChildOp.DeletePart => (-1, 0),
            ChildOp.AddStock => (0, 1),
            ChildOp.DeleteStock => (0, -1),
            _ => (0, 0)
        };
        Assert.Equal(before.Parts.Count + partDelta, after.Parts.Count);
        Assert.Equal(before.Stock.Count + stockDelta, after.Stock.Count);
    }

    [Theory]
    [InlineData("PUT", "parts")]
    [InlineData("DELETE", "parts")]
    [InlineData("PUT", "stock")]
    [InlineData("DELETE", "stock")]
    public async Task Child_route_with_a_child_from_another_job_is_not_found(string method, string collection)
    {
        var before = await JobSnapshot.CaptureAsync(_db);
        var lockedChildId = collection == "parts" ? _seed.LockedPartId : _seed.LockedStockId;
        var unlockedChildId = collection == "parts" ? _seed.UnlockedPartId : _seed.UnlockedStockId;

        // Locked child addressed through the unlocked job, and vice versa.
        foreach (var (jobId, childId) in new[] { (_seed.UnlockedJobId, lockedChildId), (_seed.LockedJobId, unlockedChildId) })
        {
            var request = new HttpRequestMessage(new HttpMethod(method), $"/api/jobs/{jobId}/{collection}/{childId}");
            if (method == "PUT")
                request.Content = JsonContent.Create(new { name = "Wrong parent", quantity = 1 });

            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Child_routes_for_missing_job_are_not_found()
    {
        var response = await _client.PostAsJsonAsync("/api/jobs/999999/stock", new CreateJobStockDto
        {
            MaterialId = _seed.FlatBarMaterialId, Length = "10", Quantity = 1, IsCustomLength = true
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        response = await _client.DeleteAsync($"/api/jobs/999999/parts/{_seed.UnlockedPartId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Job header and deletion routes ---

    [Fact]
    public async Task Update_and_delete_routes_on_locked_job_return_job_locked_problem()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        var put = await _client.PutAsJsonAsync($"/api/jobs/{_seed.LockedJobId}", new UpdateJobDto
        {
            Name = "Renamed after order", Customer = "Someone else", CuttingToolId = 4, Notes = "changed"
        });
        await AssertJobLockedProblemAsync(put, _seed.LockedJobId, _seed.LockedAt);

        var delete = await _client.DeleteAsync($"/api/jobs/{_seed.LockedJobId}");
        await AssertJobLockedProblemAsync(delete, _seed.LockedJobId, _seed.LockedAt);

        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Update_and_delete_routes_on_unlocked_job_succeed()
    {
        var put = await _client.PutAsJsonAsync($"/api/jobs/{_seed.UnlockedJobId}", new UpdateJobDto
        {
            Name = "Renamed rack", Notes = "API edit"
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var dto = await put.Content.ReadFromJsonAsync<JobDetailDto>();
        Assert.Equal("Renamed rack", dto!.Name);
        Assert.Equal("Open Customer", dto.Customer);

        var afterPut = await JobSnapshot.CaptureAsync(_db);
        Assert.Null(afterPut.Job(_seed.UnlockedJobId).OptimizationResultJson);

        var delete = await _client.DeleteAsync($"/api/jobs/{_seed.UnlockedJobId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var afterDelete = await JobSnapshot.CaptureAsync(_db);
        Assert.DoesNotContain(afterDelete.Jobs, j => j.Id == _seed.UnlockedJobId);
        Assert.Empty(afterDelete.PartsOf(_seed.UnlockedJobId));
        Assert.Empty(afterDelete.StockOf(_seed.UnlockedJobId));
        Assert.Equal(afterPut.Job(_seed.LockedJobId), afterDelete.Job(_seed.LockedJobId));
    }

    [Fact]
    public async Task Update_and_delete_routes_for_missing_job_are_not_found()
    {
        var put = await _client.PutAsJsonAsync("/api/jobs/999999", new UpdateJobDto { Name = "Ghost" });
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);

        var delete = await _client.DeleteAsync("/api/jobs/999999");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    private Task<HttpResponseMessage> SendChildMutationAsync(ChildOp op, int jobId, int partId, int stockId) => op switch
    {
        ChildOp.AddPart => _client.PostAsJsonAsync($"/api/jobs/{jobId}/parts", new CreateJobPartDto
        {
            MaterialId = _seed.RoundTubeMaterialId, Name = "Added part", Length = "55 1/2", Quantity = 7
        }),
        ChildOp.UpdatePart => _client.PutAsJsonAsync($"/api/jobs/{jobId}/parts/{partId}", new UpdateJobPartDto
        {
            MaterialId = _seed.RoundTubeMaterialId, Name = "Edited part", Length = "77 1/8", Quantity = 9
        }),
        ChildOp.DeletePart => _client.DeleteAsync($"/api/jobs/{jobId}/parts/{partId}"),
        ChildOp.AddStock => _client.PostAsJsonAsync($"/api/jobs/{jobId}/stock", new CreateJobStockDto
        {
            MaterialId = _seed.RoundTubeMaterialId, StockItemId = _seed.RoundTubeStockItemId, Length = "288",
            Quantity = 3, IsCustomLength = false, Priority = 4
        }),
        ChildOp.UpdateStock => _client.PutAsJsonAsync($"/api/jobs/{jobId}/stock/{stockId}", new UpdateJobStockDto
        {
            Length = "120", Quantity = 11, Priority = 2
        }),
        ChildOp.DeleteStock => _client.DeleteAsync($"/api/jobs/{jobId}/stock/{stockId}"),
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
    };

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
