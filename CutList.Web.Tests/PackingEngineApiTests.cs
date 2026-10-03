using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CutList.Web.DTOs;
using CutList.Web.Tests.Infrastructure;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class PackingEngineApiTests : IAsyncLifetime
{
    private readonly SqlServerFixture _db;
    private TestSeed _seed = null!;
    private HttpClient _client = null!;

    public PackingEngineApiTests(SqlServerFixture db)
    {
        _db = db;
    }

    public async Task InitializeAsync()
    {
        _seed = await _db.ResetAsync();
        _client = _db.Factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Standalone_optimize_rejects_unknown_engine_instead_of_substituting()
    {
        var response = await _client.PostAsJsonAsync("/api/packing/optimize", new StandalonePackRequestDto
        {
            Parts = { new PartInputDto { Name = "A", Length = "24", Quantity = 3 } },
            StockBins = { new StockBinInputDto { Length = "96", Quantity = -1, Priority = 1 } },
            Strategy = "optimal"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Available engines: exhaustive, firstfit, bestfit", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Engines_endpoint_lists_every_engine_with_one_default()
    {
        var engines = await _client.GetFromJsonAsync<List<PackingEngineDto>>("/api/packing/engines");

        Assert.NotNull(engines);
        Assert.Equal(new[] { "exhaustive", "firstfit", "bestfit" }, engines.Select(e => e.Id));
        Assert.Equal("exhaustive", Assert.Single(engines, e => e.IsDefault).Id);
        Assert.All(engines, e => Assert.False(string.IsNullOrWhiteSpace(e.Description)));
    }

    [Theory]
    [InlineData("bestfit", "bestfit")]
    [InlineData(null, "exhaustive")]
    public async Task Job_pack_runs_requested_or_default_engine(string? requested, string expected)
    {
        var response = await _client.PostAsJsonAsync($"/api/jobs/{_seed.UnlockedJobId}/pack", new PackJobRequestDto { Engine = requested });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PackResponseDto>();
        Assert.Equal(expected, result!.EngineId);
        Assert.Equal(0, result.Summary.TotalItemsNotPlaced);
    }

    [Fact]
    public async Task Job_pack_rejects_unknown_engine_and_changes_nothing()
    {
        var before = await JobSnapshot.CaptureAsync(_db);

        var response = await _client.PostAsJsonAsync($"/api/jobs/{_seed.UnlockedJobId}/pack", new PackJobRequestDto { Engine = "fastest" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Unknown packing engine 'fastest'", await response.Content.ReadAsStringAsync());
        JobSnapshot.AssertUnchanged(before, await JobSnapshot.CaptureAsync(_db));
    }

    [Theory]
    [InlineData("exhaustive", null)]   // Engine field
    [InlineData(null, "exhaustive")]   // legacy Strategy field
    public async Task Standalone_optimize_runs_and_reports_the_selected_engine(string? engine, string? strategy)
    {
        var response = await _client.PostAsJsonAsync("/api/packing/optimize", new StandalonePackRequestDto
        {
            Parts = { new PartInputDto { Name = "A", Length = "24", Quantity = 3 } },
            StockBins = { new StockBinInputDto { Length = "96", Quantity = -1, Priority = 1 } },
            Engine = engine,
            Strategy = strategy
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("exhaustive", body.RootElement.GetProperty("engine").GetString());
        Assert.Equal("Exhaustive", body.RootElement.GetProperty("engineName").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("summary").GetProperty("totalBins").GetInt32());
        Assert.Equal(0, body.RootElement.GetProperty("summary").GetProperty("itemsNotPlaced").GetInt32());
    }

    [Fact]
    public async Task Standalone_optimize_fills_limited_stock_without_a_fallback()
    {
        // One 96" bar holds three 24" parts plus kerf; the other two are not placed.
        var response = await _client.PostAsJsonAsync("/api/packing/optimize", new StandalonePackRequestDto
        {
            Parts = { new PartInputDto { Name = "A", Length = "24", Quantity = 5 } },
            StockBins = { new StockBinInputDto { Length = "96", Quantity = 1, Priority = 1 } },
            Engine = "exhaustive"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("exhaustive", body.RootElement.GetProperty("engine").GetString());
        Assert.Equal("Exhaustive", body.RootElement.GetProperty("engineName").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("summary").GetProperty("totalBins").GetInt32());
        Assert.Equal(2, body.RootElement.GetProperty("summary").GetProperty("itemsNotPlaced").GetInt32());
    }
}
