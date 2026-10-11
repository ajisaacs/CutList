using CutList.Web.Data.Entities;
using CutList.Web.Services;
using CutList.Web.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public class PartSetJobTests(SqlServerFixture fixture)
{
    private JobService Jobs => fixture.Services.GetRequiredService<JobService>();
    private PartSetService Sets => fixture.Services.GetRequiredService<PartSetService>();
    private async Task<PartSet> CreateSet(TestSeed seed)
    {
        var set = PartSetServiceTests.Set(seed.RoundTubeMaterialId);
        set.Members.Add(new PartSetMember { Name = "Brace", MaterialId = seed.FlatBarMaterialId,
            LengthInches = 12.25m, Quantity = 3, SortOrder = 1 });
        return await Sets.CreateAsync(set);
    }

    [Fact]
    public async Task ExpansionSnapshotsAndWholeRemovalPreserveManualParts()
    {
        var seed = await fixture.ResetAsync();
        var set = await CreateSet(seed);
        var before = await JobSnapshot.CaptureAsync(fixture);
        var added = await Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 2);
        var read = (await Jobs.GetByIdAsync(seed.UnlockedJobId))!;
        var instance = Assert.Single(read.PartSetInstances);
        Assert.Equal(added.Id, instance.Id);
        Assert.Equal(set.Id, instance.PartSetId);
        Assert.Equal("DWG-01", instance.DrawingNumber);
        Assert.Equal("a", instance.Revision);
        Assert.Equal("Frame", instance.DisplayName);
        Assert.Equal(2, instance.Quantity);
        Assert.Equal(new[] { 4, 6 }, read.Parts.Where(p => p.PartSetInstanceId == added.Id).Select(p => p.Quantity));
        Assert.Equal(new[] { 1, 2 }, read.Parts.Where(p => p.PartSetInstanceId == added.Id).Select(p => p.SortOrder));
        Assert.Equal(new[] { "Rail", "Brace" }, read.Parts.Where(p => p.PartSetInstanceId == added.Id).Select(p => p.Name));
        Assert.Equal(new[] { 49.75m, 12.25m }, read.Parts.Where(p => p.PartSetInstanceId == added.Id).Select(p => p.LengthInches));
        Assert.Equal(new[] { seed.RoundTubeMaterialId, seed.FlatBarMaterialId }, read.Parts.Where(p => p.PartSetInstanceId == added.Id).Select(p => p.MaterialId));
        Assert.Null(read.OptimizationResultJson);
        Assert.Null(read.OptimizedAt);
        Assert.NotEqual(before.Job(read.Id).UpdatedAt, read.UpdatedAt);
        var snapshot = await JobSnapshot.CaptureAsync(fixture);
        set.DisplayName = "New master";
        set.Members.First().Quantity = 9;
        await Sets.UpdateAsync(set);
        await Sets.ArchiveAsync(set.Id);
        JobSnapshot.AssertUnchanged(snapshot, await JobSnapshot.CaptureAsync(fixture));
        await Jobs.SaveOptimizationResultAsync(read.Id, "saved", DateTime.UtcNow);
        await Jobs.RemovePartSetInstanceAsync(read.Id, added.Id);
        var after = await JobSnapshot.CaptureAsync(fixture);
        Assert.Empty(after.Instances);
        Assert.Equal(before.PartsOf(read.Id), after.PartsOf(read.Id));
        Assert.Null(after.Job(read.Id).OptimizationResultJson);
        Assert.Null(after.Job(read.Id).OptimizedAt);
    }

    [Theory]
    [InlineData("quantity")] [InlineData("negative-quantity")] [InlineData("overflow")] [InlineData("missing-set")]
    [InlineData("empty")] [InlineData("archived")] [InlineData("inactive-material")]
    [InlineData("locked")] [InlineData("missing-job")] [InlineData("sort-overflow")]
    public async Task RejectedExpansionIsAtomic(string reason)
    {
        var seed = await fixture.ResetAsync();
        var input = PartSetServiceTests.Set(seed.RoundTubeMaterialId);
        if (reason == "empty") input.Members.Clear();
        if (reason == "overflow") input.Members.First().Quantity = int.MaxValue;
        var set = await Sets.CreateAsync(input);
        if (reason == "archived") await Sets.ArchiveAsync(set.Id);
        await using (var db = await fixture.CreateContextAsync())
        {
            if (reason == "inactive-material")
                (await db.Materials.FindAsync(seed.RoundTubeMaterialId))!.IsActive = false;
            if (reason == "sort-overflow")
                (await db.JobParts.FindAsync(seed.UnlockedPartId))!.SortOrder = int.MaxValue;
            await db.SaveChangesAsync();
        }
        var before = await JobSnapshot.CaptureAsync(fixture);
        var error = await Record.ExceptionAsync(() => Jobs.AddPartSetAsync(
            reason == "locked" ? seed.LockedJobId : reason == "missing-job" ? int.MaxValue : seed.UnlockedJobId,
            reason == "missing-set" ? int.MaxValue : set.Id,
            reason == "quantity" ? 0 : reason == "negative-quantity" ? -1 : 2));
        if (reason == "locked") Assert.IsType<JobLockedException>(error);
        else if (reason is "missing-set" or "missing-job") Assert.IsType<KeyNotFoundException>(error);
        else Assert.IsType<ArgumentException>(error);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(fixture));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CompetingLockRejectsExpansionOrRemoval(bool remove)
    {
        var seed = await fixture.ResetAsync();
        var set = await CreateSet(seed);
        var instance = remove ? await Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 2) : null;
        await Jobs.SaveOptimizationResultAsync(seed.UnlockedJobId, "plan", DateTime.UtcNow);
        JobSnapshot? locked = null;
        fixture.Factory.SaveGate.BeforeNextSave(async () =>
        {
            await Jobs.LockAsync(seed.UnlockedJobId);
            locked = await JobSnapshot.CaptureAsync(fixture);
        });
        await Assert.ThrowsAsync<JobLockedException>(() => remove
            ? Jobs.RemovePartSetInstanceAsync(seed.UnlockedJobId, instance!.Id)
            : Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 2));
        Assert.Equal(1, fixture.Factory.SaveGate.TriggeredCount);
        JobSnapshot.AssertUnchanged(locked!, await JobSnapshot.CaptureAsync(fixture));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SqlFailureRollsBackWholeMutation(bool remove)
    {
        var seed = await fixture.ResetAsync();
        var set = await CreateSet(seed);
        var instance = remove ? await Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 2) : null;
        await Jobs.SaveOptimizationResultAsync(seed.UnlockedJobId, "plan", DateTime.UtcNow);
        var before = await JobSnapshot.CaptureAsync(fixture);
        fixture.Factory.Sql.FailAfterNextBatch(sql => remove
            ? sql.Contains("DELETE FROM [JobParts]")
            : sql.Contains("INSERT INTO [JobParts]") || sql.Contains("MERGE [JobParts]"));
        await Assert.ThrowsAsync<DbUpdateException>(() => remove
            ? Jobs.RemovePartSetInstanceAsync(seed.UnlockedJobId, instance!.Id)
            : Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 2));
        Assert.NotNull(fixture.Factory.Sql.FaultedCommand);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(fixture));
    }

    [Fact]
    public async Task IndividualMembersAndWrongOwnerRemovalAreRejected()
    {
        var seed = await fixture.ResetAsync();
        var set = await CreateSet(seed);
        var instance = await Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 2);
        var part = (await Jobs.GetByIdAsync(seed.UnlockedJobId))!.Parts.First(p => p.PartSetInstanceId != null);
        var before = await JobSnapshot.CaptureAsync(fixture);
        // A forged null link and caller scalars cannot turn a persisted member into a manual part.
        await Assert.ThrowsAsync<ArgumentException>(() => Jobs.UpdatePartAsync(new JobPart {
            Id = part.Id, JobId = seed.UnlockedJobId, MaterialId = seed.FlatBarMaterialId, Name = "forged", Quantity = 1 }));
        await Assert.ThrowsAsync<ArgumentException>(() => Jobs.DeletePartAsync(part.Id));
        var other = await Jobs.CreateAsync();
        var withOther = await JobSnapshot.CaptureAsync(fixture);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Jobs.RemovePartSetInstanceAsync(other.Id, instance.Id));
        JobSnapshot.AssertUnchanged(withOther, await JobSnapshot.CaptureAsync(fixture));
        await Jobs.DeleteAsync(other.Id);
        // Ignore the deliberately created/deleted job identity sequence; compare persisted data.
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(fixture));
        await Jobs.LockAsync(seed.UnlockedJobId);
        var locked = await JobSnapshot.CaptureAsync(fixture);
        await Assert.ThrowsAsync<JobLockedException>(() => Jobs.RemovePartSetInstanceAsync(seed.UnlockedJobId, instance.Id));
        JobSnapshot.AssertUnchanged(locked, await JobSnapshot.CaptureAsync(fixture));
    }

    [Fact]
    public async Task DuplicateRemapsMultipleInstancesAndJobDeletionCascadesSafely()
    {
        var seed = await fixture.ResetAsync();
        var set = await CreateSet(seed);
        await Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 2);
        await Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 3);
        await Jobs.SaveOptimizationResultAsync(seed.UnlockedJobId, "original plan", DateTime.UtcNow);
        await Jobs.LockAsync(seed.UnlockedJobId);
        var before = await JobSnapshot.CaptureAsync(fixture);
        var original = (await Jobs.GetByIdAsync(seed.UnlockedJobId))!;
        var copy = await Jobs.DuplicateAsync(original.Id);
        var read = (await Jobs.GetByIdAsync(copy.Id))!;
        Assert.False(read.IsLocked);
        Assert.Null(read.OptimizationResultJson);
        Assert.Null(read.OptimizedAt);
        Assert.Equal(2, read.PartSetInstances.Count);
        Assert.Single(read.Parts, p => p.PartSetInstanceId == null);
        foreach (var source in original.PartSetInstances)
        {
            var target = Assert.Single(read.PartSetInstances, i => i.Quantity == source.Quantity);
            Assert.NotEqual(source.Id, target.Id);
            Assert.Equal(source.DrawingNumber, target.DrawingNumber);
            Assert.Equal(source.Revision, target.Revision);
            Assert.Equal(source.DisplayName, target.DisplayName);
            Assert.Equal(source.PartSetId, target.PartSetId);
            Assert.Equal(original.Parts.Where(p => p.PartSetInstanceId == source.Id).Select(p => (p.MaterialId, p.Name, p.LengthInches, p.Quantity, p.SortOrder)),
                read.Parts.Where(p => p.PartSetInstanceId == target.Id).Select(p => (p.MaterialId, p.Name, p.LengthInches, p.Quantity, p.SortOrder)));
        }
        await Jobs.DeleteAsync(copy.Id);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(fixture));
    }

    [Fact]
    public async Task InvalidPersistedMemberAndMissingInstanceLeaveJobUntouched()
    {
        var seed = await fixture.ResetAsync();
        var set = await CreateSet(seed);
        await using (var db = await fixture.CreateContextAsync())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE PartSetMembers SET Name = '' WHERE PartSetId = {set.Id}");
        var before = await JobSnapshot.CaptureAsync(fixture);
        await Assert.ThrowsAsync<ArgumentException>(() => Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 1));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Jobs.RemovePartSetInstanceAsync(seed.UnlockedJobId, int.MaxValue));
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(fixture));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CompetingJobDeletionTranslatesConflictWithoutOrphans(bool remove)
    {
        var seed = await fixture.ResetAsync();
        var set = await CreateSet(seed);
        var instance = remove ? await Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 2) : null;
        JobSnapshot? deleted = null;
        fixture.Factory.SaveGate.BeforeNextSave(async () =>
        {
            await Jobs.DeleteAsync(seed.UnlockedJobId);
            deleted = await JobSnapshot.CaptureAsync(fixture);
        });
        await Assert.ThrowsAsync<JobMutationConflictException>(() => remove
            ? Jobs.RemovePartSetInstanceAsync(seed.UnlockedJobId, instance!.Id)
            : Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 2));
        JobSnapshot.AssertUnchanged(deleted!, await JobSnapshot.CaptureAsync(fixture));
    }

    [Fact]
    public async Task DuplicateFailureDoesNotLeavePartialJobOrProvenance()
    {
        var seed = await fixture.ResetAsync();
        var set = await CreateSet(seed);
        await Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 2);
        var before = await JobSnapshot.CaptureAsync(fixture);
        fixture.Factory.Sql.FailAfterNextBatch(sql => sql.Contains("MERGE [JobParts]") || sql.Contains("INSERT INTO [JobParts]"));
        await Assert.ThrowsAsync<DbUpdateException>(() => Jobs.DuplicateAsync(seed.UnlockedJobId));
        Assert.NotNull(fixture.Factory.Sql.FaultedCommand);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(fixture));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task IndividualMemberHttpMutationReturnsBadRequestWithoutChanges(bool delete)
    {
        var seed = await fixture.ResetAsync();
        var set = await CreateSet(seed);
        var instance = await Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 1);
        var part = instance.Parts.First();
        var before = await JobSnapshot.CaptureAsync(fixture);
        using var client = fixture.Factory.CreateClient();
        var route = $"/api/jobs/{seed.UnlockedJobId}/parts/{part.Id}";
        using var response = delete ? await client.DeleteAsync(route)
            : await System.Net.Http.Json.HttpClientJsonExtensions.PutAsJsonAsync(client, route,
                new CutList.Web.DTOs.UpdateJobPartDto { Name = "Forged edit", Quantity = 9 });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("remove the whole part set instance", await response.Content.ReadAsStringAsync());
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(fixture));
    }

    [Fact]
    public async Task UnrelatedForeignKeyFailureRemainsDatabaseErrorAndRollsBack()
    {
        var seed = await fixture.ResetAsync();
        var before = await JobSnapshot.CaptureAsync(fixture);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => Jobs.AddPartAsync(new JobPart
        {
            JobId = seed.UnlockedJobId, MaterialId = int.MaxValue, Name = "Missing material",
            LengthInches = 10, Quantity = 1
        }));
        Assert.Equal(547, Assert.IsType<Microsoft.Data.SqlClient.SqlException>(error.InnerException).Number);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(fixture));
    }

    [Fact]
    public async Task RemovalPreservesSiblingInstanceAndDatabaseProtectsQuantityAndLibrary()
    {
        var seed = await fixture.ResetAsync();
        var set = await CreateSet(seed);
        var removed = await Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 1);
        var kept = await Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 2);
        var before = await JobSnapshot.CaptureAsync(fixture);
        await using (var db = await fixture.CreateContextAsync())
        {
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE JobPartSetInstances SET Quantity = 0 WHERE Id = {kept.Id}"));
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM PartSets WHERE Id = {set.Id}"));
        }
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(fixture));
        await Jobs.RemovePartSetInstanceAsync(seed.UnlockedJobId, removed.Id);
        var after = await JobSnapshot.CaptureAsync(fixture);
        Assert.Equal(before.Instances.Where(i => i.Id == kept.Id), after.Instances);
        Assert.Equal(before.Parts.Where(p => p.PartSetInstanceId != removed.Id), after.Parts);
    }

    [Fact]
    public async Task DatabaseRejectsCrossJobLinksAndManualAddIgnoresForgedProvenance()
    {
        var seed = await fixture.ResetAsync();
        var set = await CreateSet(seed);
        var instance = await Jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 1);
        await using (var db = await fixture.CreateContextAsync())
        {
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE JobParts SET PartSetInstanceId = {instance.Id} WHERE Id = {seed.LockedPartId}"));
        }
        var manual = await Jobs.AddPartAsync(new JobPart { JobId = seed.UnlockedJobId,
            MaterialId = seed.FlatBarMaterialId, Name = "Manual", LengthInches = 10, Quantity = 1,
            PartSetInstanceId = instance.Id, PartSetInstance = instance });
        await using var fresh = await fixture.CreateContextAsync();
        Assert.Null((await fresh.JobParts.FindAsync(manual.Id))!.PartSetInstanceId);
        await Jobs.UpdatePartAsync(new JobPart { Id = manual.Id, JobId = manual.JobId,
            MaterialId = seed.FlatBarMaterialId, Name = "Updated", LengthInches = 11, Quantity = 2 });
        await Jobs.DeletePartAsync(manual.Id);
        Assert.DoesNotContain((await JobSnapshot.CaptureAsync(fixture)).Parts, p => p.Id == manual.Id);
    }
}
