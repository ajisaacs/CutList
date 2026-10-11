using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CutList.Web.Services;
using CutList.Web.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class PartSetApiTests(SqlServerFixture db) : IAsyncLifetime
{
    private TestSeed seed = null!;
    private HttpClient client = null!;
    public async Task InitializeAsync() { seed = await db.ResetAsync(); client = db.Factory.CreateClient(); }
    public Task DisposeAsync() { client.Dispose(); return Task.CompletedTask; }
    private object Input(int quantity = 3, decimal length = 24m, int? material = null) => new
    {
        drawingNumber = " DR-API ", revision = " A ", displayName = "API assembly",
        members = new[] { new { name = "Tube", materialId = material ?? seed.RoundTubeMaterialId,
            lengthInches = length, quantity, sourceItem = "1", sourceFile = "tube.dwg", sortOrder = 0 } },
        id = 999, isArchived = true, jobId = seed.LockedJobId, lockedAt = "2026-01-01", optimizationResultJson = "forged"
    };
    private async Task<JsonNode> CreateAsync(int quantity = 3)
    {
        var response = await client.PostAsJsonAsync("/api/part-sets", Input(quantity));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        return (await client.GetFromJsonAsync<JsonNode>(response.Headers.Location))!;
    }
    private string Instances(int job) => $"/api/jobs/{job}/part-set-instances";
    private async Task<JsonNode> JobAsync() => (await client.GetFromJsonAsync<JsonNode>($"/api/jobs/{seed.UnlockedJobId}"))!;

    [Fact]
    public async Task Library_and_job_roundtrip_preserves_snapshot_and_manual_clients()
    {
        var set = await CreateAsync(); var id = (int)set["id"]!;
        Assert.Equal("DR-API", (string?)set["drawingNumber"]);
        Assert.False((bool)set["isArchived"]!);
        var search = await client.GetFromJsonAsync<JsonArray>("/api/part-sets?search=dr-api");
        Assert.Equal(id, (int)Assert.Single(search!)!["id"]!);
        var before = await JobSnapshot.CaptureAsync(db);
        var added = await client.PostAsJsonAsync(Instances(seed.UnlockedJobId), new { partSetId = id, quantity = 2,
            jobId = seed.LockedJobId, partSetInstanceId = 999, lockedAt = "2026-01-01", optimizationResultJson = "forged" });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var instance = (await added.Content.ReadFromJsonAsync<JsonNode>())!;
        var instanceId = (int)instance["id"]!;
        Assert.Equal(6, (int)Assert.Single(instance["parts"]!.AsArray())!["quantity"]!);
        var job = await JobAsync();
        Assert.Equal(instanceId, (int)Assert.Single(job["partSetInstances"]!.AsArray())!["id"]!);
        Assert.Equal(2, job["parts"]!.AsArray().Count);
        var expanded = Assert.Single(job["parts"]!.AsArray(), p => p!["partSetInstanceId"] != null)!;
        Assert.Equal(instanceId, (int)expanded["partSetInstanceId"]!);
        var after = await JobSnapshot.CaptureAsync(db);
        Assert.Null(after.Job(seed.UnlockedJobId).OptimizationResultJson);
        Assert.True(after.Job(seed.UnlockedJobId).UpdatedAt > before.Job(seed.UnlockedJobId).UpdatedAt);
        Assert.Null(after.Job(seed.UnlockedJobId).LockedAt);
        Assert.Equal(before.Job(seed.LockedJobId), after.Job(seed.LockedJobId));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/jobs/{seed.UnlockedJobId}/parts/{(int)expanded["id"]!}", new { quantity = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/jobs/{seed.UnlockedJobId}/parts/{(int)expanded["id"]!}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/part-sets/{id}", Input(7))).StatusCode);
        Assert.Equal(7, (int)Assert.Single((await client.GetFromJsonAsync<JsonNode>($"/api/part-sets/{id}"))!["members"]!.AsArray())!["quantity"]!);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/part-sets/{id}/archive", null)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonArray>("/api/part-sets"))!);
        Assert.Single((await client.GetFromJsonAsync<JsonArray>("/api/part-sets?includeArchived=true"))!);
        Assert.True((bool)(await client.GetFromJsonAsync<JsonNode>($"/api/part-sets/{id}"))!["isArchived"]!);
        Assert.Equal(6, (int)Assert.Single(Assert.Single((await JobAsync())["partSetInstances"]!.AsArray())!["parts"]!.AsArray())!["quantity"]!);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Instances(seed.UnlockedJobId)}/{instanceId}")).StatusCode);
        job = await JobAsync(); Assert.Empty(job["partSetInstances"]!.AsArray()); Assert.Single(job["parts"]!.AsArray());
        Assert.Single((await client.GetFromJsonAsync<JsonArray>($"/api/jobs/{seed.UnlockedJobId}/parts"))!);
    }

    [Theory]
    [InlineData(0, 24, false)] [InlineData(-1, 24, false)] [InlineData(1, 0, false)] [InlineData(1, 24, true)]
    public async Task Invalid_library_input_is_400_and_atomic(int quantity, decimal length, bool missingMaterial)
    {
        var before = await JobSnapshot.CaptureAsync(db);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/part-sets", Input(quantity, length, missingMaterial ? 999999 : null))).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonArray>("/api/part-sets?includeArchived=true"))!);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(db));
    }

    [Fact]
    public async Task Search_matches_drawing_and_name_but_excludes_other_sets()
    {
        var match = await CreateAsync();
        var other = await client.PostAsJsonAsync("/api/part-sets", new { drawingNumber = "OTHER", displayName = "Different", members = Array.Empty<object>() });
        other.EnsureSuccessStatusCode();
        foreach (var term in new[] { "dr-api", "assembly" })
        {
            var found = await client.GetFromJsonAsync<JsonArray>($"/api/part-sets?search={term}");
            Assert.Equal((int)match["id"]!, (int)Assert.Single(found!)!["id"]!);
        }
        Assert.Empty((await client.GetFromJsonAsync<JsonArray>("/api/part-sets?search=not-found"))!);
    }

    [Fact]
    public async Task Duplicate_and_missing_library_routes_have_explicit_errors()
    {
        await CreateAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/part-sets", Input())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/part-sets/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/part-sets/999999", Input())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/part-sets/999999/archive", null)).StatusCode);
    }

    [Theory]
    [InlineData("0")] [InlineData("-1")] [InlineData("2147483647")] [InlineData("2147483648")] [InlineData("\"bad\"")] [InlineData("1.5")]
    public async Task Invalid_or_overflow_expansion_is_400_without_writes(string quantity)
    {
        var set = await CreateAsync(); var before = await JobSnapshot.CaptureAsync(db);
        var body = "{\"partSetId\":" + (int)set["id"]! + ",\"quantity\":" + quantity + "}";
        var response = await client.PostAsync(Instances(seed.UnlockedJobId), new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(db));
    }

    [Fact]
    public async Task Wrong_job_and_missing_instances_are_404_even_when_route_job_locked()
    {
        var set = await CreateAsync();
        var response = await client.PostAsJsonAsync(Instances(seed.UnlockedJobId), new { partSetId = (int)set["id"]!, quantity = 1 });
        response.EnsureSuccessStatusCode(); var instance = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        var before = await JobSnapshot.CaptureAsync(db);
        foreach (var job in new[] { seed.LockedJobId, 999999 })
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{Instances(job)}/{(int)instance["id"]!}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"{Instances(seed.UnlockedJobId)}/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Instances(999999), new { partSetId = (int)set["id"]!, quantity = 1 })).StatusCode);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(db));
    }

    [Theory]
    [InlineData("null")] [InlineData("[null]")]
    public async Task Null_members_are_400(string members)
    {
        var body = "{\"drawingNumber\":\"NULL\",\"displayName\":\"Null\",\"members\":" + members + "}";
        var response = await client.PostAsync("/api/part-sets", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonArray>("/api/part-sets"))!);
    }

    [Theory]
    [InlineData("empty")] [InlineData("archived")] [InlineData("inactive")]
    public async Task Unusable_sets_are_400_and_missing_set_is_404(string reason)
    {
        var set = await CreateAsync(); var id = (int)set["id"]!;
        if (reason == "empty")
            (await client.PutAsJsonAsync($"/api/part-sets/{id}", new { drawingNumber = "DR-API", displayName = "Empty", members = Array.Empty<object>() })).EnsureSuccessStatusCode();
        else if (reason == "archived") (await client.PostAsync($"/api/part-sets/{id}/archive", null)).EnsureSuccessStatusCode();
        else
        {
            await using var context = await db.CreateContextAsync();
            (await context.Materials.FindAsync(seed.RoundTubeMaterialId))!.IsActive = false;
            await context.SaveChangesAsync();
        }
        var before = await JobSnapshot.CaptureAsync(db);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Instances(seed.UnlockedJobId), new { partSetId = id, quantity = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Instances(seed.UnlockedJobId), new { partSetId = 999999, quantity = 1 })).StatusCode);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(db));
    }

    [Fact]
    public async Task Service_rejects_wrong_locked_owner_before_lock_check()
    {
        var set = await CreateAsync();
        var service = db.Services.GetRequiredService<JobService>();
        var instance = await service.AddPartSetAsync(seed.UnlockedJobId, (int)set["id"]!, 1);
        var before = await JobSnapshot.CaptureAsync(db);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RemovePartSetInstanceAsync(seed.LockedJobId, instance.Id));
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(db));
    }

    [Fact]
    public async Task Deleted_job_racing_add_returns_job_changed_without_partial_instance()
    {
        var set = await CreateAsync();
        var service = db.Services.GetRequiredService<JobService>();
        JobSnapshot? competing = null;
        db.Factory.SaveGate.BeforeNextSave(async () => { await service.DeleteAsync(seed.UnlockedJobId); competing = await JobSnapshot.CaptureAsync(db); });
        var response = await client.PostAsJsonAsync(Instances(seed.UnlockedJobId), new { partSetId = (int)set["id"]!, quantity = 1 });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("job_changed", (string?)(await response.Content.ReadFromJsonAsync<JsonNode>())!["code"]);
        Assert.NotNull(competing);
        JobSnapshot.AssertUnchanged(competing, await JobSnapshot.CaptureAsync(db));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/jobs/{seed.UnlockedJobId}")).StatusCode);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task Locked_and_racing_add_remove_return_problem_and_preserve_aggregate(bool remove, bool race)
    {
        var set = await CreateAsync(); var id = (int)set["id"]!;
        var service = db.Services.GetRequiredService<JobService>();
        var instanceId = remove ? (await service.AddPartSetAsync(seed.UnlockedJobId, id, 2)).Id : 0;
        if (!race) await service.LockAsync(seed.UnlockedJobId);
        var before = await JobSnapshot.CaptureAsync(db);
        JobSnapshot? competing = null;
        if (race) db.Factory.SaveGate.BeforeNextSave(async () => { await service.LockAsync(seed.UnlockedJobId); competing = await JobSnapshot.CaptureAsync(db); });
        var response = remove ? await client.DeleteAsync($"{Instances(seed.UnlockedJobId)}/{instanceId}")
            : await client.PostAsJsonAsync(Instances(seed.UnlockedJobId), new { partSetId = id, quantity = 2 });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = (await response.Content.ReadFromJsonAsync<JsonNode>())!;
        Assert.Equal("job_locked", (string?)problem["code"]);
        Assert.Equal(seed.UnlockedJobId, (int)problem["jobId"]!);
        JobSnapshot.AssertUnchanged(competing ?? before, await JobSnapshot.CaptureAsync(db));
        Assert.True((bool)(await JobAsync())["isLocked"]!);
        if (race) Assert.Equal(1, db.Factory.SaveGate.TriggeredCount);
    }
}
