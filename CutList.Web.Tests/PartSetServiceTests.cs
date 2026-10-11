using CutList.Web.Data.Entities;
using CutList.Web.Services;
using CutList.Web.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public class PartSetServiceTests(SqlServerFixture fixture)
{
    private PartSetService Service => fixture.Services.GetRequiredService<PartSetService>();
    internal static PartSet Set(int materialId) => new()
    {
        DrawingNumber = " DWG-01 ", Revision = " a ", DisplayName = "Frame",
        Members = new List<PartSetMember> {
            new() { Name = "Rail", MaterialId = materialId, LengthInches = 49.75m, Quantity = 2, SourceItem = "1", SourceFile = "rail.sldprt" }
        }
    };

    [Fact]
    public async Task CreateReadSearchEditArchive()
    {
        var seed = await fixture.ResetAsync();
        var set = await Service.CreateAsync(Set(seed.RoundTubeMaterialId));
        var read = await Service.GetByIdAsync(set.Id);
        Assert.Equal("DWG-01", read!.DrawingNumber);
        Assert.Equal("a", read.Revision);
        Assert.Equal(49.75m, Assert.Single(read.Members).LengthInches);
        Assert.Single(await Service.GetAllAsync("dwg-01"));
        read.DisplayName = "Revised frame";
        read.Members.First().Quantity = 3;
        await Service.UpdateAsync(read);
        Assert.Equal(3, (await Service.GetByIdAsync(set.Id))!.Members.First().Quantity);
        await Service.ArchiveAsync(set.Id);
        Assert.Empty(await Service.GetAllAsync());
        Assert.True((await Service.GetByIdAsync(set.Id))!.IsArchived);
    }

    [Fact]
    public async Task DatabaseEnforcesUniqueKeyChecksAndForeignKeys()
    {
        var seed = await fixture.ResetAsync();
        var created = await Service.CreateAsync(Set(seed.RoundTubeMaterialId));
        await using (var db = await fixture.CreateContextAsync())
        {
            var duplicate = Set(seed.RoundTubeMaterialId);
            duplicate.DrawingKey = created.DrawingKey;
            duplicate.RevisionKey = created.RevisionKey;
            db.PartSets.Add(duplicate);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = await fixture.CreateContextAsync())
        {
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($"UPDATE PartSetMembers SET LengthInches = 0 WHERE PartSetId = {created.Id}"));
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($"UPDATE PartSetMembers SET Quantity = 0 WHERE PartSetId = {created.Id}"));
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($"UPDATE PartSetMembers SET MaterialId = {int.MaxValue} WHERE PartSetId = {created.Id}"));
            // Isolate the new Material FK from existing job/stock references.
            await db.Database.ExecuteSqlRawAsync("DELETE FROM JobParts; DELETE FROM JobStocks; DELETE FROM StockItems;");
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Materials WHERE Id = {seed.RoundTubeMaterialId}"));
        }
        await using (var db = await fixture.CreateContextAsync())
        {
            Assert.Single(await db.PartSets.ToListAsync());
            var row = Assert.Single(await db.PartSetMembers.ToListAsync());
            Assert.Equal(49.75m, row.LengthInches);
            Assert.Equal(2, row.Quantity);
            Assert.Equal(seed.RoundTubeMaterialId, row.MaterialId);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM PartSets WHERE Id = {created.Id}");
        }
        await using var readback = await fixture.CreateContextAsync();
        Assert.Empty(await readback.PartSetMembers.ToListAsync());
        Assert.True(await readback.Materials.AnyAsync(m => m.Id == seed.RoundTubeMaterialId));
    }

    [Fact]
    public async Task BlankRevisionCanonicalizesAndDifferentRevisionsRemainDistinct()
    {
        var seed = await fixture.ResetAsync();
        var first = Set(seed.RoundTubeMaterialId); first.Revision = null;
        await Service.CreateAsync(first);
        var duplicate = Set(seed.RoundTubeMaterialId); duplicate.Revision = "   ";
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateAsync(duplicate));
        await Service.CreateAsync(Set(seed.RoundTubeMaterialId));
        Assert.Equal(2, (await Service.GetAllAsync()).Count);
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(10, 0)] [InlineData(-1, 2)]
    [InlineData(10, -1)] [InlineData(1000000, 1)] [InlineData(0.00001, 1)]
    public async Task InvalidRowsAreAtomic(decimal length, int quantity)
    {
        var seed = await fixture.ResetAsync();
        var set = Set(seed.RoundTubeMaterialId);
        // A valid earlier row must not be persisted when a later row fails validation.
        set.Members.Add(new PartSetMember { Name = "Valid brace", MaterialId = seed.RoundTubeMaterialId,
            LengthInches = 12m, Quantity = 1, SortOrder = -1 });
        set.Members.First().LengthInches = length;
        set.Members.First().Quantity = quantity;
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateAsync(set));
        await using var db = await fixture.CreateContextAsync();
        Assert.Empty(await db.PartSets.ToListAsync());
        Assert.Empty(await db.PartSetMembers.ToListAsync());
    }

    [Fact]
    public async Task CanonicalDuplicateAndInvalidMaterialAreRejected()
    {
        var seed = await fixture.ResetAsync();
        await Service.CreateAsync(Set(seed.RoundTubeMaterialId));
        var duplicate = Set(seed.RoundTubeMaterialId);
        duplicate.DrawingNumber = "dwg-01"; duplicate.Revision = "A";
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateAsync(duplicate));
        var invalid = Set(int.MaxValue); invalid.DrawingNumber = "missing-material";
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateAsync(invalid));
        await using var db = await fixture.CreateContextAsync();
        Assert.Single(await db.PartSets.ToListAsync());
        Assert.Single(await db.PartSetMembers.ToListAsync());
    }
}
