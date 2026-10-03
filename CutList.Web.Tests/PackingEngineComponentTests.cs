using Bunit;
using CutList.Core.Nesting;
using CutList.Web.Data;
using CutList.Web.Data.Entities;
using CutList.Web.Services;
using CutList.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EditJobPage = CutList.Web.Components.Pages.Jobs.Edit;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class PackingEngineComponentTests : IAsyncLifetime
{
    private readonly SqlServerFixture _db;
    private TestSeed _seed = null!;
    private BunitContext _ctx = null!;

    public PackingEngineComponentTests(SqlServerFixture db)
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

    [Fact]
    public async Task Picker_lists_every_engine_and_optimize_saves_the_selected_one()
    {
        var page = await RenderResultsTabAsync(_seed.UnlockedJobId);
        var options = page.FindAll("select#packing-engine option").Select(o => o.GetAttribute("value"));
        Assert.Equal(new[] { "exhaustive", "firstfit", "bestfit" }, options);

        page.Find("select#packing-engine").Change(new ChangeEventArgs { Value = "bestfit" });
        await page.Find("#run-optimization").ClickAsync(new());

        Assert.Contains("\"EngineId\":\"bestfit\"", await SavedJsonAsync(_seed.UnlockedJobId));
        page.WaitForAssertion(() => Assert.Contains("Engine: Best Fit", page.Find(".packing-engine-used").TextContent));
    }

    [Fact]
    public async Task Reoptimizing_reuses_the_saved_engine_by_default()
    {
        await SavePlanAsync(_seed.UnlockedJobId, "exhaustive");
        var page = await RenderResultsTabAsync(_seed.UnlockedJobId);
        Assert.Contains("Engine: Exhaustive", page.Find(".packing-engine-used").TextContent);

        await page.Find("#run-optimization").ClickAsync(new());

        Assert.Contains("\"EngineId\":\"exhaustive\"", await SavedJsonAsync(_seed.UnlockedJobId));
    }

    [Fact]
    public async Task Plan_saved_under_a_retired_engine_id_still_loads_and_reoptimizes_with_the_default()
    {
        // "advanced" was renamed to "firstfit" without an alias; plans saved under it must keep loading,
        // and the picker then starts on the configured default (exhaustive).
        await SavePlanAsync(_seed.UnlockedJobId, "bestfit");
        var retired = (await SavedJsonAsync(_seed.UnlockedJobId))!
            .Replace("\"EngineId\":\"bestfit\"", "\"EngineId\":\"advanced\"")
            .Replace("\"EngineName\":\"Best Fit\"", "\"EngineName\":\"Advanced Fit\"");
        Assert.Contains("\"EngineId\":\"advanced\"", retired);
        await using (var context = await _db.CreateContextAsync())
        {
            await context.Jobs.Where(j => j.Id == _seed.UnlockedJobId)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.OptimizationResultJson, retired));
        }

        var page = await RenderResultsTabAsync(_seed.UnlockedJobId);
        Assert.Contains("Engine: Advanced Fit", page.Find(".packing-engine-used").TextContent);

        await page.Find("#run-optimization").ClickAsync(new());

        Assert.Contains("\"EngineId\":\"exhaustive\"", await SavedJsonAsync(_seed.UnlockedJobId));
    }

    [Fact]
    public async Task Picker_is_disabled_for_a_locked_job()
    {
        var page = await RenderResultsTabAsync(_seed.LockedJobId);

        Assert.True(page.Find("select#packing-engine").HasAttribute("disabled"));
    }

    private async Task<IRenderedComponent<EditJobPage>> RenderResultsTabAsync(int jobId)
    {
        var page = _ctx.Render<EditJobPage>(p => p.Add(x => x.Id, jobId));
        await page.WaitForAssertionAsync(() => Assert.NotEmpty(page.FindAll("ul.nav-tabs")));
        await page.FindAll("ul.nav-tabs button").Single(b => b.TextContent.Trim().StartsWith("Results")).ClickAsync(new());
        return page;
    }

    private async Task<string?> SavedJsonAsync(int jobId)
    {
        await using var context = await _db.CreateContextAsync();
        return await context.Jobs.Where(j => j.Id == jobId).Select(j => j.OptimizationResultJson).SingleAsync();
    }

    private async Task SavePlanAsync(int jobId, string engineId)
    {
        var packing = new CutListPackingService(
            _db.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>(),
            _db.Services.GetRequiredService<IPackingEngineCatalog>());
        var result = await packing.PackAsync(
            [new JobPart { MaterialId = _seed.FlatBarMaterialId, Name = "Seeded", LengthInches = 50m, Quantity = 1 }],
            0.125m,
            [new JobStock { MaterialId = _seed.FlatBarMaterialId, StockItemId = _seed.FlatBarStockItemId, LengthInches = 240m, Quantity = 1, Priority = 1 }],
            engineId);

        await using var context = await _db.CreateContextAsync();
        await context.Jobs.Where(j => j.Id == jobId).ExecuteUpdateAsync(s => s
            .SetProperty(j => j.OptimizationResultJson, packing.SerializeResult(result))
            .SetProperty(j => j.OptimizedAt, DateTime.UtcNow));
    }
}
