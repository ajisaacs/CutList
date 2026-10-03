using CutList.Core.Nesting;
using CutList.Web.Data;
using CutList.Web.Data.Entities;
using CutList.Web.Services;
using CutList.Web.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CutList.Web.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class PackingEngineServiceTests : IAsyncLifetime
{
    private readonly SqlServerFixture _db;
    private TestSeed _seed = null!;
    private CutListPackingService _packing = null!;

    public PackingEngineServiceTests(SqlServerFixture db)
    {
        _db = db;
    }

    public async Task InitializeAsync()
    {
        _seed = await _db.ResetAsync();
        _packing = new CutListPackingService(
            _db.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>(),
            _db.Services.GetRequiredService<IPackingEngineCatalog>());
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private List<JobPart> Parts() =>
        [new JobPart { MaterialId = _seed.FlatBarMaterialId, Name = "Brace", LengthInches = 30m, Quantity = 8 }];

    private List<JobStock> UnlimitedStock() =>
        [new JobStock { MaterialId = _seed.FlatBarMaterialId, LengthInches = 240m, Quantity = -1, IsCustomLength = true, Priority = 1 }];

    [Theory]
    [InlineData("advanced")]
    [InlineData("bestfit")]
    [InlineData("exhaustive")]
    public async Task Pack_runs_the_requested_engine_and_places_every_part(string engineId)
    {
        var result = await _packing.PackAsync(Parts(), 0.125m, UnlimitedStock(), engineId);

        Assert.Equal(engineId, result.EngineId);
        var material = Assert.Single(result.MaterialResults);
        Assert.Empty(material.PackResult.ItemsNotUsed);
        Assert.Equal(8, material.PackResult.Bins.Sum(b => b.Items.Count));
    }

    [Fact]
    public async Task Pack_without_an_engine_uses_the_configured_default()
    {
        var result = await _packing.PackAsync(Parts(), 0.125m, UnlimitedStock());

        Assert.Equal(_packing.DefaultEngine.Id, result.EngineId);
        Assert.Equal(_packing.DefaultEngine.DisplayName, result.EngineName);
    }

    [Fact]
    public async Task Pack_with_an_unknown_engine_is_rejected()
    {
        var ex = await Assert.ThrowsAsync<UnknownPackingEngineException>(
            () => _packing.PackAsync(Parts(), 0.125m, UnlimitedStock(), "fastest"));

        Assert.Equal("fastest", ex.EngineId);
    }

    [Fact]
    public async Task Saved_result_keeps_the_engine()
    {
        var result = await _packing.PackAsync(Parts(), 0.125m, UnlimitedStock(), "bestfit");

        var loaded = await _packing.LoadSavedResultAsync(_packing.SerializeResult(result));

        Assert.NotNull(loaded);
        Assert.Equal("bestfit", loaded.EngineId);
        Assert.Equal("Best Fit", loaded.EngineName);
    }

    [Fact]
    public async Task Result_saved_before_engine_selection_loads_as_advanced_fit()
    {
        var loaded = await _packing.LoadSavedResultAsync("""{"OptimizedAt":"2026-09-01T00:00:00Z","MaterialResults":[]}""");

        Assert.NotNull(loaded);
        Assert.Equal(BuiltInPackingEngines.AdvancedFit.Id, loaded.EngineId);
        Assert.Equal(BuiltInPackingEngines.AdvancedFit.DisplayName, loaded.EngineName);
    }
}
