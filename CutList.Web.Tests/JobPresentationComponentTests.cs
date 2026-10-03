using Bunit;
using CutList.Core.Nesting;
using CutList.Web.Data;
using CutList.Web.Data.Entities;
using CutList.Web.Services;
using CutList.Web.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EditJobPage = CutList.Web.Components.Pages.Jobs.Edit;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class JobPresentationComponentTests : IAsyncLifetime
{
    private readonly SqlServerFixture _db;
    private TestSeed _seed = null!;
    private BunitContext _ctx = null!;

    public JobPresentationComponentTests(SqlServerFixture db)
    {
        _db = db;
    }

    public async Task InitializeAsync()
    {
        _seed = await _db.ResetAsync();
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton(_db.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
        _ctx.Services.AddSingleton(_db.Services.GetRequiredService<IPackingEngineCatalog>());
        _ctx.Services.AddScoped<JobService>();
        _ctx.Services.AddScoped<MaterialService>();
        _ctx.Services.AddScoped<CutListPackingService>();
        _ctx.Services.AddScoped<ReportService>();
    }

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Theory]
    [InlineData(1, "1 bar")]
    [InlineData(2, "2 bars")]
    public async Task Material_list_footer_uses_the_used_bar_count(int quantity, string expected)
    {
        var jobId = await CreateSavedJobAsync(quantity);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Results");

        Assert.Equal(expected, page.Find(".print-material-list tfoot td.text-end").TextContent.Trim());
    }

    [Theory]
    [InlineData(1, ".cutlist-material-screen-header .text-muted", "1 bar · 1 piece")]
    [InlineData(1, ".cutlist-material-print-header .cutlist-material-stats", "1 bar · 1 piece")]
    [InlineData(2, ".cutlist-material-screen-header .text-muted", "2 bars · 2 pieces")]
    [InlineData(2, ".cutlist-material-print-header .cutlist-material-stats", "2 bars · 2 pieces")]
    public async Task Material_headers_use_the_used_bar_and_piece_counts(int quantity, string selector, string expected)
    {
        var jobId = await CreateSavedJobAsync(quantity);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Results");

        var stats = NormalizeWhitespace(page.Find(selector).TextContent).Split(" · ");
        Assert.Equal(expected, string.Join(" · ", stats.Take(2)));
    }

    [Theory]
    [InlineData(1, "Total: 1 piece")]
    [InlineData(2, "Total: 2 pieces")]
    public async Task Parts_total_uses_the_piece_count(int quantity, string expected)
    {
        var jobId = await CreateSavedJobAsync(quantity);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Parts");

        Assert.Equal(expected, NormalizeWhitespace(page.Find(".tab-content .mt-3.text-muted").TextContent));
    }

    [Theory]
    [InlineData(1, "1 item not placed")]
    [InlineData(2, "2 items not placed")]
    public async Task Unplaced_warning_uses_the_item_count(int quantity, string expected)
    {
        var jobId = await CreateSavedJobAsync(quantity, hasStock: false);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Results");

        Assert.Equal(expected, page.Find(".cutlist-material-card .alert-danger strong").TextContent.Trim());
        foreach (var selector in new[] { ".cutlist-material-screen-header .text-muted", ".cutlist-material-print-header .cutlist-material-stats" })
        {
            var stats = NormalizeWhitespace(page.Find(selector).TextContent).Split(" · ");
            Assert.Equal("0 bars · 0 pieces", string.Join(" · ", stats.Take(2)));
        }
    }

    [Theory]
    [InlineData("Parts", "144.0000", "(144\")")]
    [InlineData("Parts", "240.0000", "(240\")")]
    [InlineData("Parts", "12.0625", "(12.0625\")")]
    [InlineData("Stock", "144.0000", "(144\")")]
    [InlineData("Stock", "240.0000", "(240\")")]
    [InlineData("Stock", "12.0625", "(12.0625\")")]
    public async Task Secondary_lengths_strip_only_insignificant_database_zeros(string tab, string storedLength, string expected)
    {
        var length = decimal.Parse(storedLength, System.Globalization.CultureInfo.InvariantCulture);
        var jobId = await CreateSavedJobAsync(partLength: length, stockLength: length);
        var snapshot = await JobSnapshot.CaptureAsync(_db);
        Assert.Equal(length, Assert.Single(snapshot.PartsOf(jobId)).LengthInches);
        Assert.Equal(length, Assert.Single(snapshot.StockOf(jobId)).LengthInches);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, tab);

        Assert.Equal(expected, page.Find(".tab-content tbody td:nth-child(2) .text-muted").TextContent.Trim());
    }

    [Fact]
    public async Task Rendering_and_unit_toggles_preserve_the_entire_locked_saved_job()
    {
        var jobId = await CreateSavedJobAsync(partLength: 12.0625m);
        var before = await JobSnapshot.CaptureAsync(_db);
        Assert.NotNull(before.Job(jobId).LockedAt);
        Assert.NotNull(before.Job(jobId).OptimizedAt);
        Assert.NotNull(before.Job(jobId).OptimizationResultJson);
        var page = await RenderEditorAsync(jobId);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));

        await ClickTabAsync(page, "Parts");
        await ClickTabAsync(page, "Stock");
        await ClickTabAsync(page, "Results");
        var initialLength = page.Find(".cutlist-material-card tbody td:nth-child(2)").TextContent;
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Show feet + inches").ClickAsync(new());
        Assert.NotEqual(initialLength, page.Find(".cutlist-material-card tbody td:nth-child(2)").TextContent);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));

        await page.FindAll("button").Single(b => b.TextContent.Trim() == "Show all inches").ClickAsync(new());
        Assert.Equal(initialLength, page.Find(".cutlist-material-card tbody td:nth-child(2)").TextContent);
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    private async Task<int> CreateSavedJobAsync(
        int quantity = 1, bool hasStock = true, decimal partLength = 144.0000m, decimal stockLength = 240.0000m)
    {
        var jobs = _ctx.Services.GetRequiredService<JobService>();
        var packing = _ctx.Services.GetRequiredService<CutListPackingService>();
        var job = await jobs.CreateAsync(new Job { Name = "Presentation fixture", CuttingToolId = 1 });
        await jobs.AddPartAsync(new JobPart
        {
            JobId = job.Id,
            MaterialId = _seed.FlatBarMaterialId,
            Name = "Presentation part",
            LengthInches = partLength,
            Quantity = quantity
        });
        if (hasStock)
        {
            await jobs.AddStockAsync(new JobStock
            {
                JobId = job.Id,
                MaterialId = _seed.FlatBarMaterialId,
                StockItemId = null,
                IsCustomLength = true,
                LengthInches = stockLength,
                Quantity = quantity,
                Priority = 1
            });
        }

        // Pack freshly loaded persisted rows, save the actual custom-source plan, then lock it.
        job = (await jobs.GetByIdAsync(job.Id))!;
        var result = await packing.PackAsync(job.Parts, job.CuttingTool!.KerfInches, job.Stock);
        var materialResult = Assert.Single(result.MaterialResults);
        Assert.Empty(materialResult.InStockBins);
        Assert.Equal(hasStock ? quantity : 0, materialResult.ToBePurchasedBins.Count);
        Assert.Equal(hasStock ? 0 : quantity, materialResult.PackResult.ItemsNotUsed.Count);
        await jobs.SaveOptimizationResultAsync(job.Id, packing.SerializeResult(result), DateTime.UtcNow);
        await jobs.LockAsync(job.Id);
        return job.Id;
    }

    private async Task<IRenderedComponent<EditJobPage>> RenderEditorAsync(int jobId)
    {
        var page = _ctx.Render<EditJobPage>(p => p.Add(x => x.Id, jobId));
        await page.WaitForAssertionAsync(() => Assert.NotEmpty(page.FindAll("ul.nav-tabs")));
        return page;
    }

    private static Task ClickTabAsync(IRenderedComponent<EditJobPage> page, string tab) =>
        page.FindAll("ul.nav-tabs button").Single(b => b.TextContent.Trim().StartsWith(tab)).ClickAsync(new());

    private static string NormalizeWhitespace(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
