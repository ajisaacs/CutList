using Bunit;
using CutList.Core.Nesting;
using CutList.Web.Data;
using CutList.Web.Data.Entities;
using CutList.Web.Services;
using CutList.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LibraryEdit = CutList.Web.Components.Pages.PartSets.Edit;
using LibraryIndex = CutList.Web.Components.Pages.PartSets.Index;
using JobEdit = CutList.Web.Components.Pages.Jobs.Edit;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class PartSetComponentTests(SqlServerFixture db) : IAsyncLifetime
{
    private BunitContext ctx = null!;
    private TestSeed seed = null!;
    public async Task InitializeAsync()
    {
        seed = await db.ResetAsync();
        ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddSingleton(db.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
        ctx.Services.AddSingleton(db.Services.GetRequiredService<IPackingEngineCatalog>());
        ctx.Services.AddScoped<JobService>();
        ctx.Services.AddScoped<PartSetService>();
        ctx.Services.AddScoped<MaterialService>();
        ctx.Services.AddScoped<CutListPackingService>();
        ctx.Services.AddScoped<ReportService>();
    }
    public async Task DisposeAsync() { db.Factory.SaveGate.Reset(); await ctx.DisposeAsync(); }
    private PartSetService Library => ctx.Services.GetRequiredService<PartSetService>();
    private Task<PartSet> CreateSet() => Library.CreateAsync(new PartSet {
        DrawingNumber = "SYN-100", Revision = "A", DisplayName = "Synthetic frame",
        Members = [new PartSetMember { Name = "Frame rail", MaterialId = seed.FlatBarMaterialId, LengthInches = 20, Quantity = 3 }]
    });
    private static Task Parts(IRenderedComponent<JobEdit> page) => page.FindAll("ul.nav-tabs button").Single(b => b.TextContent.Trim().StartsWith("Parts")).ClickAsync(new());
    private async Task<IRenderedComponent<JobEdit>> Job(int id)
    {
        var page = ctx.Render<JobEdit>(p => p.Add(x => x.Id, id));
        await page.WaitForAssertionAsync(() => Assert.NotEmpty(page.FindAll("ul.nav-tabs")));
        await Parts(page);
        return page;
    }
    [Fact]
    public async Task Manual_create_edit_search_and_archive_library_workflow()
    {
        var page = ctx.Render<LibraryEdit>();
        await page.WaitForAssertionAsync(() => Assert.NotEmpty(page.FindAll("#drawing-number")));
        page.Find("#drawing-number").Change("SYN-200");
        page.Find("#set-name").Change("Manual frame");
        await page.Find("#add-member").ClickAsync(new());
        page.Find(".member-name").Change("Rail");
        page.Find(".member-material").Change(seed.FlatBarMaterialId.ToString());
        page.Find(".length-input input").Input("24");
        page.Find(".member-quantity").Change("3");
        await page.Find("form").SubmitAsync();
        var set = Assert.Single(await Library.GetAllAsync("SYN-200"));
        var edit = ctx.Render<LibraryEdit>(p => p.Add(x => x.Id, set.Id));
        await edit.WaitForAssertionAsync(() => Assert.Equal("Rail", edit.Find(".member-name").GetAttribute("value")));
        edit.Find(".member-name").Change("Edited rail");
        await edit.Find("form").SubmitAsync();
        Assert.Equal("Edited rail", Assert.Single((await Library.GetByIdAsync(set.Id))!.Members).Name);
        var index = ctx.Render<LibraryIndex>();
        await index.WaitForAssertionAsync(() => Assert.Contains("Manual frame", index.Markup));
        index.Find("#set-search").Change("missing");
        await index.Find("#search-sets").ClickAsync(new());
        Assert.Empty(index.FindAll("tbody tr"));
        index.Find("#set-search").Change("SYN-200");
        await index.Find("#search-sets").ClickAsync(new());
        Assert.Contains($"part-sets/{set.Id}", index.Find("tbody a").GetAttribute("href"));
        await index.Find("button[title='Archive']").ClickAsync(new());
        await index.Find(".modal-footer .btn-danger").ClickAsync(new());
        Assert.True((await Library.GetByIdAsync(set.Id))!.IsArchived);
        Assert.Empty(index.FindAll("tbody tr"));
    }
    [Fact]
    public async Task Add_two_assemblies_shows_snapshot_group_and_removes_only_the_group()
    {
        var set = await CreateSet();
        var page = await Job(seed.UnlockedJobId);
        page.Find("#job-part-set").Change(set.Id.ToString());
        page.Find("#set-assembly-count").Change("2");
        await page.Find("#add-part-set").ClickAsync(new());
        Assert.Contains("SYN-100", page.Find(".part-set-instance").TextContent);
        Assert.Contains("2 assemblies", page.Find(".part-set-instance").TextContent);
        var row = page.Find("tr[data-set-member]");
        Assert.Contains("Frame rail", row.TextContent);
        Assert.Contains("6", row.TextContent);
        Assert.Empty(row.QuerySelectorAll("button"));
        var job = (await ctx.Services.GetRequiredService<JobService>().GetByIdAsync(seed.UnlockedJobId))!;
        Assert.Equal(6, Assert.Single(job.Parts, p => p.PartSetInstanceId != null).Quantity);
        Assert.NotEmpty(job.Stock);
        set.Members.Single().Name = "Changed master rail";
        await Library.UpdateAsync(set);
        await Library.ArchiveAsync(set.Id);
        Assert.Equal("Frame rail", Assert.Single((await ctx.Services.GetRequiredService<JobService>().GetByIdAsync(job.Id))!.Parts, p => p.PartSetInstanceId != null).Name);
        await page.FindAll("ul.nav-tabs button").Single(b => b.TextContent.Trim().StartsWith("Results")).ClickAsync(new());
        await page.Find("#run-optimization").ClickAsync(new());
        Assert.Contains("Frame rail", page.Markup);
        Assert.DoesNotContain("Changed master rail", page.Markup);
        var persisted = (await ctx.Services.GetRequiredService<JobService>().GetByIdAsync(job.Id))!;
        var plan = await ctx.Services.GetRequiredService<CutListPackingService>().LoadSavedResultAsync(persisted.OptimizationResultJson!);
        Assert.NotNull(plan);
        Assert.Equal(6, plan.MaterialResults.SelectMany(m => m.PackResult.Bins).SelectMany(b => b.Items).Count(i => i.Name == "Frame rail"));
        await Parts(page);
        await page.Find("button[title='Remove drawing set']").ClickAsync(new());
        await page.Find(".modal-footer .btn-danger").ClickAsync(new());
        Assert.Empty(page.FindAll(".part-set-instance"));
        Assert.NotEmpty(page.FindAll("button[title='Edit']"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stale_or_racing_lock_on_add_recovers_without_partial_rows(bool race)
    {
        var set = await CreateSet();
        var page = await Job(seed.UnlockedJobId);
        page.Find("#job-part-set").Change(set.Id.ToString());
        async Task Lock() { await using var c = await db.CreateContextAsync(); await c.Jobs.Where(j => j.Id == seed.UnlockedJobId).ExecuteUpdateAsync(s => s.SetProperty(j => j.LockedAt, DateTime.UtcNow)); }
        if (race) db.Factory.SaveGate.BeforeNextSave(Lock);
        else await Lock();
        await page.Find("#add-part-set").ClickAsync(new());
        Assert.Contains("reloaded", page.Find("#job-conflict-alert").TextContent);
        Assert.Empty(page.FindAll("#add-part-set"));
        Assert.Empty(page.FindAll("button[title='Remove drawing set']"));
        await using var c = await db.CreateContextAsync();
        Assert.False(await c.JobPartSetInstances.AnyAsync());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Locked_job_hides_add_and_remove_and_stale_or_racing_remove_keeps_group(bool race)
    {
        var set = await CreateSet();
        var jobs = ctx.Services.GetRequiredService<JobService>();
        await jobs.AddPartSetAsync(seed.UnlockedJobId, set.Id, 2);
        var stale = await Job(seed.UnlockedJobId);
        await stale.Find("button[title='Remove drawing set']").ClickAsync(new());
        var expected = await JobSnapshot.CaptureAsync(db);
        if (race) db.Factory.SaveGate.BeforeNextSave(() => jobs.LockAsync(seed.UnlockedJobId));
        else await jobs.LockAsync(seed.UnlockedJobId);
        await stale.Find(".modal-footer .btn-danger").ClickAsync(new());
        Assert.NotEmpty(stale.FindAll("#job-conflict-alert"));
        Assert.Empty(stale.FindAll(".modal"));
        var after = await JobSnapshot.CaptureAsync(db);
        Assert.Equal(expected.PartsOf(seed.UnlockedJobId), after.PartsOf(seed.UnlockedJobId));
        Assert.NotNull(after.Job(seed.UnlockedJobId).LockedAt);
        await using (var c = await db.CreateContextAsync()) Assert.Single(await c.JobPartSetInstances.ToListAsync());
        var locked = await Job(seed.UnlockedJobId);
        Assert.Empty(locked.FindAll("#add-part-set"));
        Assert.Empty(locked.FindAll("button[title='Remove drawing set']"));
        Assert.NotEmpty(locked.FindAll(".part-set-instance"));
    }
}
