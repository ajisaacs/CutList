using CutList.Mcp;
using CutList.Web.Services;
using CutList.Web.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class PartSetMcpTests(SqlServerFixture db) : IAsyncLifetime
{
    private TestSeed seed = null!;
    private HttpClient http = null!;
    private PartSetTools tools = null!;
    private JobTools jobs = null!;
    public async Task InitializeAsync()
    {
        seed = await db.ResetAsync(); http = db.Factory.CreateClient();
        var api = new ApiClient(http); tools = new(api); jobs = new(api);
    }
    public Task DisposeAsync() { http.Dispose(); return Task.CompletedTask; }
    private PartSetMemberInput[] Members(int quantity = 3, decimal length = 24.1234m) =>
        [new() { MaterialId = seed.RoundTubeMaterialId, Name = "Reviewed tube", LengthInches = length,
            Quantity = quantity, SourceItem = " item 1 ", SourceFile = " reviewed.dwg ", SortOrder = 7 }];
    private async Task<int> Create()
    {
        var created = await tools.CreatePartSet("MCP-DR", "Assembly", Members(), true, "A");
        Assert.True(created.Success, created.Error); return created.Data!.Id;
    }

    [Fact]
    public async Task Reviewed_library_and_job_roundtrip_preserves_provenance_and_snapshot()
    {
        var id = await Create();
        Assert.Equal(id, Assert.Single((await tools.ListPartSets("mcp-dr")).Data!).Id);
        var detail = (await tools.GetPartSet(id)).Data!;
        var member = Assert.Single(detail.Members);
        Assert.Equal(24.1234m, member.LengthInches); Assert.Equal(0, member.SortOrder); // Server compacts the supplied order.
        Assert.Equal(" item 1 ", member.SourceItem); Assert.Equal(" reviewed.dwg ", member.SourceFile);
        Assert.Equal(seed.RoundTubeMaterialId, member.MaterialId);
        var added = await tools.AddJobPartSet(seed.UnlockedJobId, id, 2);
        Assert.True(added.Success, added.Error);
        var instance = Assert.Single(added.Job!.PartSetInstances);
        Assert.Equal(id, instance.PartSetId); Assert.Equal("A", instance.Revision); Assert.Equal(2, instance.Quantity);
        var cut = Assert.Single(instance.Parts); Assert.Equal(6, cut.Quantity); Assert.Equal(instance.Id, cut.PartSetInstanceId);
        var flat = Assert.Single(added.Job.Parts, p => p.PartSetInstanceId == instance.Id);
        Assert.Equal(cut.Id, flat.Id); Assert.Equal(cut.SortOrder, flat.SortOrder);
        Assert.Contains(added.Job.Parts, p => p.PartSetInstanceId == null);
        Assert.True((await tools.UpdatePartSet(id, "MCP-DR", "Revised", Members(7), true, "A")).Success);
        Assert.Equal(7, Assert.Single((await tools.GetPartSet(id)).Data!.Members).Quantity);
        Assert.True((await tools.ArchivePartSet(id)).Data!.IsArchived);
        Assert.Empty((await tools.ListPartSets()).Data!);
        Assert.Single((await tools.ListPartSets(includeArchived: true)).Data!);
        var job = (await jobs.GetJob(seed.UnlockedJobId)).Job!;
        Assert.Equal("Assembly", Assert.Single(job.PartSetInstances).DisplayName);
        Assert.Equal(6, Assert.Single(job.PartSetInstances).Parts.Single().Quantity);
        var stockIds = job.Stock.Select(s => s.Id).ToArray();
        var removed = await tools.RemoveJobPartSetInstance(seed.UnlockedJobId, instance.Id);
        Assert.True(removed.Success, removed.Error); Assert.Empty(removed.Job!.PartSetInstances);
        Assert.Single(removed.Job.Parts); Assert.Null(removed.Job.Parts.Single().PartSetInstanceId);
        Assert.Equal(stockIds, removed.Job.Stock.Select(s => s.Id));
        Assert.Empty((await jobs.GetJob(seed.UnlockedJobId)).Job!.PartSetInstances);
    }

    [Fact]
    public async Task Unreviewed_and_invalid_members_fail_without_writes()
    {
        var before = await JobSnapshot.CaptureAsync(db);
        var unreviewed = await tools.CreatePartSet("MCP-DR", "Assembly", Members(), false);
        Assert.False(unreviewed.Success); Assert.Contains("review", unreviewed.Error!);
        foreach (var members in new[] { Members(0), Members(length: 0), Members(length: 24.12345m) })
        {
            var failed = await tools.CreatePartSet("MCP-DR", "Assembly", members, true);
            Assert.False(failed.Success); Assert.Contains("Invalid drawing-set request", failed.Error!);
        }
        var missing = Members(); missing[0].MaterialId = 999999;
        Assert.False((await tools.CreatePartSet("MCP-DR", "Assembly", missing, true)).Success);
        Assert.Empty((await tools.ListPartSets(includeArchived: true)).Data!);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(db));
    }

    [Fact]
    public async Task Locked_instance_removal_is_rejected_with_server_detail_and_no_changes()
    {
        var id = await Create();
        var added = await tools.AddJobPartSet(seed.UnlockedJobId, id, 1);
        var instanceId = Assert.Single(added.Job!.PartSetInstances).Id;
        using (var scope = db.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<JobService>().LockAsync(seed.UnlockedJobId);
        var before = await JobSnapshot.CaptureAsync(db);
        var result = await tools.RemoveJobPartSetInstance(seed.UnlockedJobId, instanceId);
        Assert.False(result.Success); Assert.Contains("job_locked", result.Error!); Assert.Contains("Unlock", result.Error!);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(db));
        var readback = await jobs.GetJob(seed.UnlockedJobId);
        Assert.True(readback.Job!.IsLocked); Assert.Equal(instanceId, Assert.Single(readback.Job.PartSetInstances).Id);
    }

    [Fact]
    public async Task Lock_invalid_expansion_and_wrong_owner_preserve_job_state()
    {
        var id = await Create();
        var added = await tools.AddJobPartSet(seed.UnlockedJobId, id, 1);
        var instance = Assert.Single(added.Job!.PartSetInstances);
        var before = await JobSnapshot.CaptureAsync(db);
        var locked = await tools.AddJobPartSet(seed.LockedJobId, id, 1);
        Assert.False(locked.Success); Assert.Contains("job_locked", locked.Error!); Assert.Contains("Unlock", locked.Error!);
        var owner = await tools.RemoveJobPartSetInstance(seed.LockedJobId, instance.Id);
        Assert.False(owner.Success); Assert.Contains("404", owner.Error!); Assert.DoesNotContain("job_locked", owner.Error!);
        foreach (var qty in new[] { 0, int.MaxValue })
            Assert.False((await tools.AddJobPartSet(seed.UnlockedJobId, id, qty)).Success);
        Assert.False((await tools.GetPartSet(999999)).Success);
        Assert.False((await tools.UpdatePartSet(999999, "missing", "Missing", Members(), true)).Success);
        Assert.False((await tools.ArchivePartSet(999999)).Success);
        Assert.False((await tools.AddJobPartSet(seed.UnlockedJobId, 999999, 1)).Success);
        Assert.False((await tools.RemoveJobPartSetInstance(seed.UnlockedJobId, 999999)).Success);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(db));
        Assert.True((await tools.ArchivePartSet(id)).Success);
        Assert.False((await tools.AddJobPartSet(seed.UnlockedJobId, id, 1)).Success);
    }
}
