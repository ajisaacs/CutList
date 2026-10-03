using CutList.Mcp;
using CutList.Web.Tests.Infrastructure;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class PackingEngineMcpTests : IAsyncLifetime
{
    private readonly SqlServerFixture _db;
    private TestSeed _seed = null!;
    private HttpClient _http = null!;
    private JobTools _tools = null!;

    public PackingEngineMcpTests(SqlServerFixture db)
    {
        _db = db;
    }

    public async Task InitializeAsync()
    {
        _seed = await _db.ResetAsync();
        _http = _db.Factory.CreateClient();
        _tools = new JobTools(new ApiClient(_http));
    }

    public Task DisposeAsync()
    {
        _http.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task List_packing_engines_returns_the_server_catalog()
    {
        var engines = await _tools.ListPackingEngines();

        Assert.Equal(new[] { "firstfit", "bestfit", "exhaustive" }, engines.Select(e => e.Id));
        Assert.Single(engines, e => e.IsDefault);
    }

    [Fact]
    public async Task Optimize_job_runs_and_reports_the_requested_engine()
    {
        var result = await _tools.OptimizeJob(_seed.UnlockedJobId, engine: "bestfit");

        Assert.True(result.Success, result.Error);
        Assert.Equal("bestfit", result.EngineId);
        Assert.Equal("Best Fit", result.EngineName);
    }

    [Fact]
    public async Task Optimize_job_with_unknown_engine_explains_the_choices()
    {
        var result = await _tools.OptimizeJob(_seed.UnlockedJobId, engine: "fastest");

        Assert.False(result.Success);
        Assert.Contains("Unknown packing engine 'fastest'. Available engines: firstfit, bestfit, exhaustive.", result.Error);
    }

    [Theory]
    [InlineData("bestfit")]
    [InlineData("exhaustive")]
    public void Create_cutlist_places_every_part_on_unlimited_stock(string engine)
    {
        var result = CutListTools.CreateCutList(
            [new PartInput { Name = "A", Length = "30", Quantity = 5 }],
            [new StockBinInput { Length = "96", Quantity = -1, Priority = 1 }],
            0.125,
            engine);

        Assert.True(result.Success, result.Error);
        Assert.Equal(engine, result.EngineId);
        Assert.Empty(result.UnusedItems);
        Assert.Equal(5, result.Bins.Sum(b => b.Items.Count));
    }

    [Fact]
    public void Create_cutlist_rejects_unknown_engine()
    {
        var result = CutListTools.CreateCutList(
            [new PartInput { Name = "A", Length = "30", Quantity = 1 }],
            [new StockBinInput { Length = "96", Quantity = -1 }],
            0.125,
            "optimal");

        Assert.False(result.Success);
        Assert.Contains("Available engines: firstfit, bestfit, exhaustive", result.Error);
    }
}
