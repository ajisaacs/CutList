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
        Assert.Contains("Available engines: advanced, bestfit, exhaustive", await response.Content.ReadAsStringAsync());
    }
}
