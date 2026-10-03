using Bunit;
using CutList.Web.Data;
using CutList.Web.Data.Entities;
using CutList.Web.Services;
using CutList.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EditJobPage = CutList.Web.Components.Pages.Jobs.Edit;
using JobsIndexPage = CutList.Web.Components.Pages.Jobs.Index;

namespace CutList.Web.Tests;

/// <summary>
/// Renders the real job pages (direct JobService callers) against the SQL Server test database and
/// simulates another session locking the job while this page shows stale, unlocked state. bUnit
/// rethrows unhandled handler exceptions, so a passing interaction also proves the circuit survived.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class JobLockingComponentTests : IAsyncLifetime
{
    private const string LockedReason = "is locked because materials have been ordered. Unlock it explicitly";

    private readonly SqlServerFixture _db;
    private TestSeed _seed = null!;
    private BunitContext _ctx = null!;

    public JobLockingComponentTests(SqlServerFixture db)
    {
        _db = db;
    }

    public async Task InitializeAsync()
    {
        _seed = await _db.ResetAsync();
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton(_db.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
        _ctx.Services.AddScoped<JobService>();
        _ctx.Services.AddScoped<MaterialService>();
        _ctx.Services.AddScoped<CutListPackingService>();
        _ctx.Services.AddScoped<ReportService>();
    }

    public async Task DisposeAsync()
    {
        _db.Factory.SaveGate.Reset();
        await _ctx.DisposeAsync();
    }

    [Fact]
    public async Task Stale_details_editor_save_shows_lock_and_keeps_persisted_job()
    {
        var page = await RenderEditorAsync(_seed.UnlockedJobId);
        page.Find("input[placeholder='Descriptive name for this job']")
            .Change(new ChangeEventArgs { Value = "Renamed in a stale editor" });
        await LockInAnotherSessionAsync(_seed.UnlockedJobId);
        var expected = await JobSnapshot.CaptureAsync(_db);

        await page.Find("form").SubmitAsync();

        AssertConflictShown(page);
        Assert.Equal("Open rack", page.Find("input[placeholder='Descriptive name for this job']").GetAttribute("value"));
        JobSnapshot.AssertUnchanged(expected, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Stale_part_edit_modal_is_closed_and_nothing_is_saved()
    {
        var page = await RenderEditorAsync(_seed.UnlockedJobId);
        await ClickTabAsync(page, "Parts");
        await page.Find("button[title='Edit']").ClickAsync(new());
        Assert.NotEmpty(page.FindAll(".modal"));
        await LockInAnotherSessionAsync(_seed.UnlockedJobId);
        var expected = await JobSnapshot.CaptureAsync(_db);

        await page.Find(".modal-footer .btn-primary").ClickAsync(new());

        AssertConflictShown(page);
        Assert.Empty(page.FindAll(".modal"));
        Assert.Empty(page.FindAll("button[title='Delete']"));
        JobSnapshot.AssertUnchanged(expected, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Stale_part_and_stock_delete_buttons_are_rejected_and_rows_remain()
    {
        var page = await RenderEditorAsync(_seed.UnlockedJobId);
        await ClickTabAsync(page, "Parts");
        await LockInAnotherSessionAsync(_seed.UnlockedJobId);
        var expected = await JobSnapshot.CaptureAsync(_db);

        await page.Find("button[title='Delete']").ClickAsync(new());
        AssertConflictShown(page);

        // Simulate the stock tab of a second stale view of the same page.
        await using (var context = await _db.CreateContextAsync())
        {
            await context.Jobs.Where(j => j.Id == _seed.UnlockedJobId)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.LockedAt, (DateTime?)null));
        }
        var stockPage = await RenderEditorAsync(_seed.UnlockedJobId);
        await ClickTabAsync(stockPage, "Stock");
        await LockInAnotherSessionAsync(_seed.UnlockedJobId);
        expected = await JobSnapshot.CaptureAsync(_db);

        await stockPage.Find("button[title='Delete']").ClickAsync(new());

        AssertConflictShown(stockPage);
        JobSnapshot.AssertUnchanged(expected, await JobSnapshot.CaptureAsync(_db));
    }

    [Fact]
    public async Task Multi_row_part_add_stops_at_the_lock_and_reports_rows_actually_saved()
    {
        var page = await RenderEditorAsync(_seed.UnlockedJobId);
        await ClickTabAsync(page, "Parts");
        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Add Part").ClickAsync(new());
        await page.Find(".modal select").ChangeAsync(new ChangeEventArgs { Value = nameof(MaterialShape.FlatBar) });
        await page.FindAll(".modal select")[1].ChangeAsync(new ChangeEventArgs { Value = _seed.FlatBarMaterialId.ToString() });
        var addRow = page.FindAll(".modal button").Single(b => b.TextContent.Contains("Add Row"));
        await addRow.ClickAsync(new());
        await page.FindAll(".modal button").Single(b => b.TextContent.Contains("Add Row")).ClickAsync(new());
        var lengths = page.FindAll(".modal tbody .length-input input");
        Assert.Equal(3, lengths.Count);
        for (var i = 0; i < lengths.Count; i++)
            await page.FindAll(".modal tbody .length-input input")[i].InputAsync(new ChangeEventArgs { Value = $"{10 + i}" });

        var before = await JobSnapshot.CaptureAsync(_db);
        // Another session locks the job just before the second row is saved.
        var savesSeen = 0;
        _db.Factory.SaveGate.BeforeNextSave(
            () => LockInAnotherSessionAsync(_seed.UnlockedJobId),
            when: ctx => ctx.ChangeTracker.Entries<JobPart>().Any(e => e.State == EntityState.Added) && ++savesSeen == 2);

        await page.Find(".modal-footer .btn-primary").ClickAsync(new());

        Assert.Equal(1, _db.Factory.SaveGate.TriggeredCount);
        page.WaitForAssertion(() =>
            Assert.Contains("1 of 3 part rows were saved before that happened", page.Find("#job-conflict-alert").TextContent));
        AssertConflictShown(page);
        Assert.Empty(page.FindAll(".modal"));
        var after = await JobSnapshot.CaptureAsync(_db);
        var added = after.PartsOf(_seed.UnlockedJobId).Except(before.PartsOf(_seed.UnlockedJobId)).ToList();
        Assert.Equal([10m], added.Select(p => p.LengthInches));
        Assert.NotNull(after.Job(_seed.UnlockedJobId).LockedAt);
    }

    [Fact]
    public async Task Optimizer_that_loses_the_race_discards_its_result_and_shows_the_ordered_plan()
    {
        var persistedJson = await SeedPersistedPlanAsync(_seed.UnlockedJobId, "Ordered plan marker");
        var page = await RenderEditorAsync(_seed.UnlockedJobId);
        await ClickTabAsync(page, "Results");
        Assert.Contains("Ordered plan marker", page.Markup);
        Assert.DoesNotContain("Shelf brace", page.Markup);

        var competingLock = new DateTime(2026, 10, 3, 15, 0, 0, DateTimeKind.Utc);
        _db.Factory.SaveGate.BeforeNextSave(async () =>
        {
            await using var context = await _db.CreateContextAsync();
            await context.Jobs.Where(j => j.Id == _seed.UnlockedJobId)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.LockedAt, competingLock));
        });

        await page.Find("button.btn-success").ClickAsync(new());

        Assert.Equal(1, _db.Factory.SaveGate.TriggeredCount);
        AssertConflictShown(page);
        Assert.Contains("Ordered plan marker", page.Markup);
        Assert.DoesNotContain("Shelf brace", page.Markup);
        var after = await JobSnapshot.CaptureAsync(_db);
        Assert.Equal(persistedJson, after.Job(_seed.UnlockedJobId).OptimizationResultJson);
    }

    [Fact]
    public async Task Jobs_index_disables_delete_for_locked_jobs_but_keeps_copy()
    {
        var page = await RenderIndexAsync();

        var lockedRow = RowFor(page, "JOB-SEED-LOCKED");
        Assert.True(lockedRow.QuerySelector("button[title^='Delete']")!.HasAttribute("disabled"));
        Assert.False(lockedRow.QuerySelector("button[title='Copy']")!.HasAttribute("disabled"));

        var openRow = RowFor(page, "JOB-SEED-OPEN");
        Assert.False(openRow.QuerySelector("button[title='Delete']")!.HasAttribute("disabled"));
    }

    [Fact]
    public async Task Jobs_index_stale_delete_confirmation_reports_lock_and_keeps_job()
    {
        var page = await RenderIndexAsync();
        await RowFor(page, "JOB-SEED-OPEN").QuerySelector("button[title='Delete']")!.ClickAsync(new());
        Assert.NotEmpty(page.FindAll(".modal"));
        await LockInAnotherSessionAsync(_seed.UnlockedJobId);
        var expected = await JobSnapshot.CaptureAsync(_db);

        await page.Find(".modal-footer .btn-danger").ClickAsync(new());

        await page.WaitForAssertionAsync(() =>
        {
            var alert = page.Find(".alert-danger");
            Assert.Contains("was not deleted", alert.TextContent);
            Assert.Contains(LockedReason, alert.TextContent);
        });
        Assert.True(RowFor(page, "JOB-SEED-OPEN").QuerySelector("button[title^='Delete']")!.HasAttribute("disabled"));
        JobSnapshot.AssertUnchanged(expected, await JobSnapshot.CaptureAsync(_db));
    }

    private async Task<IRenderedComponent<EditJobPage>> RenderEditorAsync(int jobId)
    {
        var page = _ctx.Render<EditJobPage>(p => p.Add(x => x.Id, jobId));
        await page.WaitForAssertionAsync(() => Assert.NotEmpty(page.FindAll("ul.nav-tabs")));
        Assert.Empty(page.FindAll(".alert-warning .bi-lock-fill"));
        return page;
    }

    private async Task<IRenderedComponent<JobsIndexPage>> RenderIndexAsync()
    {
        var page = _ctx.Render<JobsIndexPage>();
        await page.WaitForAssertionAsync(() => Assert.NotEmpty(page.FindAll("table tbody tr")));
        return page;
    }

    private static AngleSharp.Dom.IElement RowFor(IRenderedComponent<JobsIndexPage> page, string jobNumber) =>
        page.FindAll("table tbody tr").Single(r => r.TextContent.Contains(jobNumber));

    private static Task ClickTabAsync(IRenderedComponent<EditJobPage> page, string tab) =>
        page.FindAll("ul.nav-tabs button").Single(b => b.TextContent.Trim().StartsWith(tab)).ClickAsync(new());

    private static void AssertConflictShown(IRenderedComponent<EditJobPage> page)
    {
        page.WaitForAssertion(() =>
        {
            var alert = page.Find("#job-conflict-alert");
            Assert.Contains(LockedReason, alert.TextContent);
            Assert.Contains("reloaded", alert.TextContent);
            Assert.Contains("This job is locked", page.Markup);
        });
    }

    private async Task<DateTime> LockInAnotherSessionAsync(int jobId)
    {
        var lockedAt = DateTime.UtcNow;
        await using var context = await _db.CreateContextAsync();
        var rows = await context.Jobs.Where(j => j.Id == jobId && j.LockedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.LockedAt, lockedAt));
        Assert.Equal(1, rows);
        return lockedAt;
    }

    /// <summary>Stores a real serialized plan whose only part name is <paramref name="marker"/>.</summary>
    private async Task<string> SeedPersistedPlanAsync(int jobId, string marker)
    {
        var packing = new CutListPackingService(_db.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
        var result = await packing.PackAsync(
            [new JobPart { MaterialId = _seed.FlatBarMaterialId, Name = marker, LengthInches = 50m, Quantity = 1 }],
            0.125m,
            [new JobStock { MaterialId = _seed.FlatBarMaterialId, StockItemId = _seed.FlatBarStockItemId, LengthInches = 240m, Quantity = 1, Priority = 1 }]);
        var json = packing.SerializeResult(result);

        await using var context = await _db.CreateContextAsync();
        await context.Jobs.Where(j => j.Id == jobId).ExecuteUpdateAsync(s => s
            .SetProperty(j => j.OptimizationResultJson, json)
            .SetProperty(j => j.OptimizedAt, DateTime.UtcNow));
        return json;
    }
}
