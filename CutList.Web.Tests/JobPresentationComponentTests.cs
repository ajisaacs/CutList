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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Results_toolbar_keeps_complete_controls_as_direct_items(bool locked)
    {
        var jobId = await CreateSavedJobAsync(locked: locked);
        var before = await JobSnapshot.CaptureAsync(_db);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Results");

        var toolbar = page.Find(".results-toolbar");
        var picker = toolbar.QuerySelector(":scope > .packing-engine-picker")!;
        Assert.NotNull(picker);
        Assert.Equal("Engine", picker.QuerySelector("label[for='packing-engine']")!.TextContent.Trim());
        Assert.Equal(locked, picker.QuerySelector("#packing-engine")!.HasAttribute("disabled"));
        var optimize = toolbar.QuerySelector(":scope > #run-optimization")!;
        Assert.NotNull(optimize);
        Assert.Equal("Re-Optimize", NormalizeWhitespace(optimize.TextContent));
        Assert.Equal(locked, optimize.HasAttribute("disabled"));
        var buttons = toolbar.QuerySelectorAll(":scope > button");
        Assert.Contains(buttons, b => NormalizeWhitespace(b.TextContent) == "Print Report");
        var units = Assert.Single(buttons, b => NormalizeWhitespace(b.TextContent) == "Show feet + inches");
        Assert.True(units.HasAttribute("aria-pressed"));
        Assert.Equal(!locked, buttons.Any(b => NormalizeWhitespace(b.TextContent) == "Lock Job"));
        Assert.Equal(
            NormalizeWhitespace($"Last optimized: {before.Job(jobId).OptimizedAt!.Value.ToLocalTime():g}"),
            NormalizeWhitespace(toolbar.QuerySelector(":scope > .results-optimized-at")!.TextContent));
        Assert.NotNull(toolbar.QuerySelector(":scope > .packing-engine-used"));
        Assert.Equal(locked ? 6 : 7, toolbar.Children.Length);
        Assert.All(toolbar.Children, item => Assert.DoesNotContain(item.ClassList,
            name => name.StartsWith("ms-") || name.StartsWith("me-")));
        await units.ClickAsync(new());
        Assert.Contains(page.FindAll(".results-toolbar > button"), b => NormalizeWhitespace(b.TextContent) == "Show all inches");
        await page.FindAll(".results-toolbar > button").Single(b => NormalizeWhitespace(b.TextContent) == "Print Report").ClickAsync(new());
        _ctx.JSInterop.VerifyInvoke("printWithTitle");
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Theory]
    [InlineData("Exhaustive")]
    [InlineData("Exhaustive (First Fit fallback)")]
    public async Task Results_engine_label_stays_with_the_first_word_of_the_saved_value(string engineName)
    {
        var jobId = await CreateSavedJobAsync(engineName: engineName);
        var page = await RenderEditorAsync(jobId);
        await ClickTabAsync(page, "Results");

        var used = page.Find(".results-toolbar > .packing-engine-used");
        // Keep the label and first word in a no-wrap prefix, while ordinary spaces in
        // the fallback suffix remain available as narrow-screen wrap opportunities.
        Assert.Equal($"Engine: {engineName.Split(' ', 2)[0]}", used.QuerySelector(".packing-engine-used-prefix")!.TextContent);
        Assert.Equal($"Engine: {engineName}", used.TextContent.Trim());
    }

    [Theory]
    [InlineData("exhaustive")]
    [InlineData("firstfit")]
    [InlineData("bestfit")]
    public async Task Engine_help_is_a_closed_native_disclosure_with_the_complete_selected_description(string engineId)
    {
        var page = await RenderEditorAsync(_seed.UnlockedJobId);
        await ClickTabAsync(page, "Results");
        await page.Find("#packing-engine").ChangeAsync(new() { Value = engineId });
        var engine = _ctx.Services.GetRequiredService<IPackingEngineCatalog>().Engines.Single(e => e.Id == engineId);

        var help = page.Find("details.packing-engine-help");
        Assert.False(help.HasAttribute("open"));
        Assert.Contains("print-screen-only", help.ClassList);
        var summary = help.QuerySelector(":scope > summary")!;
        Assert.NotNull(summary);
        Assert.Same(summary, help.FirstElementChild);
        Assert.Equal("About this engine", summary.TextContent.Trim());
        Assert.False(summary.HasAttribute("tabindex")); // Keep native summary keyboard behavior.
        Assert.False(summary.HasAttribute("role"));
        Assert.Equal(engine.Description, help.QuerySelector(".packing-engine-description")!.TextContent.Trim());
        Assert.Single(page.FindAll(".packing-engine-description"));
        Assert.Equal(engine.Description, page.Find("#packing-engine").GetAttribute("title"));
        Assert.Equal("Engine", page.Find("label[for='packing-engine']").TextContent.Trim());
    }

    [Fact]
    public async Task Results_toolbar_preserves_optimize_before_a_plan_is_saved()
    {
        await _ctx.Services.GetRequiredService<JobService>().ClearOptimizationResultAsync(_seed.UnlockedJobId);
        var page = await RenderEditorAsync(_seed.UnlockedJobId);
        await ClickTabAsync(page, "Results");

        var toolbar = page.Find(".results-toolbar");
        Assert.Equal("Optimize", NormalizeWhitespace(toolbar.QuerySelector(":scope > #run-optimization")!.TextContent));
        Assert.NotNull(toolbar.QuerySelector(":scope > .packing-engine-picker"));
        Assert.Empty(toolbar.QuerySelectorAll(".packing-engine-used"));
        Assert.DoesNotContain(toolbar.QuerySelectorAll("button"), b => NormalizeWhitespace(b.TextContent) == "Print Report");
    }

    private async Task<int> CreateSavedJobAsync(
        int quantity = 1, bool hasStock = true, decimal partLength = 144.0000m, decimal stockLength = 240.0000m,
        bool locked = true, string? engineName = null)
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

        // Pack freshly loaded persisted rows and save the actual custom-source plan.
        job = (await jobs.GetByIdAsync(job.Id))!;
        var result = await packing.PackAsync(job.Parts, job.CuttingTool!.KerfInches, job.Stock);
        var materialResult = Assert.Single(result.MaterialResults);
        Assert.Empty(materialResult.InStockBins);
        Assert.Equal(hasStock ? quantity : 0, materialResult.ToBePurchasedBins.Count);
        Assert.Equal(hasStock ? 0 : quantity, materialResult.PackResult.ItemsNotUsed.Count);
        if (engineName != null)
        {
            // Exercise saved display metadata, including the real catalog's longest fallback name;
            // this presentation fixture does not attempt to force the packing search to time out.
            Assert.Contains(engineName, new[]
            {
                BuiltInPackingEngines.Exhaustive.DisplayName,
                BuiltInPackingEngines.Exhaustive.RunName(BuiltInPackingEngines.FirstFit)
            });
            result.EngineName = engineName;
        }
        await jobs.SaveOptimizationResultAsync(job.Id, packing.SerializeResult(result), DateTime.UtcNow);
        if (locked)
        {
            await jobs.LockAsync(job.Id);
        }
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
