using System.Net;
using System.Net.Http.Json;
using CutList.Web.Data.Entities;
using CutList.Web.Services;
using CutList.Web.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public class PartSetSafetyTests(SqlServerFixture fixture)
{
    private JobService Jobs => fixture.Services.GetRequiredService<JobService>();
    private PartSetService Sets => fixture.Services.GetRequiredService<PartSetService>();

    [Theory]
    [InlineData(10001)]
    [InlineData(int.MaxValue)]
    public async Task LargeNonOverflowingDemandReturns400WithoutMutation(int pieces)
    {
        var seed = await fixture.ResetAsync();
        var input = PartSetServiceTests.Set(seed.RoundTubeMaterialId);
        input.Members.First().Quantity = pieces;
        var set = await Sets.CreateAsync(input);
        var before = await JobSnapshot.CaptureAsync(fixture);
        using var client = fixture.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/api/jobs/{seed.UnlockedJobId}/part-set-instances",
            new { partSetId = set.Id, quantity = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("10,000", await response.Content.ReadAsStringAsync());
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(fixture));
    }

    [Fact]
    public async Task LimitIncludesManualAndExistingSetRowsAndAcceptsExactBoundary()
    {
        var seed = await fixture.ResetAsync();
        var input = PartSetServiceTests.Set(seed.RoundTubeMaterialId);
        input.Members.First().Quantity = 4996;
        var set = await Sets.CreateAsync(input);
        await Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 1);
        await Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 1);
        var job = (await Jobs.GetByIdAsync(seed.UnlockedJobId))!;
        Assert.Equal(10000L, job.Parts.Sum(p => (long)p.Quantity));
        var before = await JobSnapshot.CaptureAsync(fixture);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 1));
        Assert.Contains("10,000", error.Message);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(fixture));
    }

    [Fact]
    public async Task LegacyLargeDemandUsesBigintForOverviewAndAdmission()
    {
        var seed = await fixture.ResetAsync();
        var set = await Sets.CreateAsync(PartSetServiceTests.Set(seed.RoundTubeMaterialId));
        await using (var db = await fixture.CreateContextAsync())
        {
            (await db.JobParts.FindAsync(seed.UnlockedPartId))!.Quantity = int.MaxValue;
            db.JobParts.Add(new JobPart { JobId = seed.UnlockedJobId, MaterialId = seed.FlatBarMaterialId,
                Name = "Legacy", LengthInches = 10, Quantity = int.MaxValue, SortOrder = 1 });
            await db.SaveChangesAsync();
        }
        var before = await JobSnapshot.CaptureAsync(fixture);
        var overview = await fixture.Services.GetRequiredService<OverviewService>().GetSnapshotAsync();
        Assert.Equal(2L * int.MaxValue, overview.RecentJobs.Single(j => j.Id == seed.UnlockedJobId).PartCount);
        Assert.Equal(2L * int.MaxValue + 10, overview.TotalParts);
        await Assert.ThrowsAsync<ArgumentException>(() => Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 1));
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(fixture));
    }

    [Theory]
    [InlineData(false, "input")] [InlineData(true, "input")]
    [InlineData(false, "members")] [InlineData(true, "members")]
    [InlineData(false, "member")] [InlineData(true, "member")]
    public async Task NullServiceInputsAreArgumentErrorsAndDoNotChangeLibrary(bool update, string target)
    {
        var seed = await fixture.ResetAsync();
        var stored = await Sets.CreateAsync(PartSetServiceTests.Set(seed.RoundTubeMaterialId));
        var input = PartSetServiceTests.Set(seed.RoundTubeMaterialId);
        input.Id = stored.Id;
        input.DisplayName = "Must not persist";
        if (target == "input") input = null!;
        else if (target == "members") input.Members = null!;
        else input.Members.Add(null!);
        var before = await JobSnapshot.CaptureAsync(fixture);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => update ? Sets.UpdateAsync(input) : Sets.CreateAsync(input));
        var read = (await Sets.GetByIdAsync(stored.Id))!;
        Assert.Equal(stored.DisplayName, read.DisplayName);
        Assert.Single(read.Members);
        Assert.Single(await Sets.GetAllAsync(includeArchived: true));
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(fixture));
    }
}
