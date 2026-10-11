using CutList.Core.Nesting;
using Xunit;

namespace CutList.Core.Tests.Nesting;

public class PackingEngineCatalogTests
{
    private sealed class FakeEngine : IPackingEngine
    {
        public PackResult Pack(PackingRequest request) => new();
    }

    private static PackingEngineRegistration Registration(string id) =>
        new(new PackingEngineInfo(id, id.ToUpperInvariant(), $"{id} engine"), () => new FakeEngine());

    [Fact]
    public void Built_in_catalog_lists_engines_in_order_with_last_bar_as_default()
    {
        var catalog = PackingEngineCatalog.CreateDefault();

        Assert.Equal(new[] { "boundedlastbar", "bounded", "firstfit", "bestfit" }, catalog.Engines.Select(e => e.Id));
        Assert.Equal(new[] { "Bounded Search + Last-Bar", "Bounded Search", "First Fit", "Best Fit" },
            catalog.Engines.Select(e => e.DisplayName));
        Assert.Equal("boundedlastbar", catalog.Default.Id);
        Assert.All(catalog.Engines, e => Assert.False(string.IsNullOrWhiteSpace(e.Description)));
    }

    [Theory]
    [InlineData("BestFit", "bestfit")]
    [InlineData(" firstfit ", "firstfit")]
    [InlineData(null, "boundedlastbar")]
    [InlineData("", "boundedlastbar")]
    [InlineData("   ", "boundedlastbar")]
    public void Resolve_is_case_insensitive_and_blank_means_default(string? requested, string expected)
    {
        Assert.Equal(expected, PackingEngineCatalog.CreateDefault().Resolve(requested).Id);
    }

    [Fact]
    public void Opt_in_last_bar_engine_saves_a_bar_through_the_catalog_and_multi_bin_path()
    {
        var scenario = Benchmarks.SearchBaselineBenchmark.Scenarios.Single(s => s.Name == "repeated");
        var random = new Random(scenario.Seed);
        List<BinItem>? items = null;
        for (var job = 0; job <= 36; job++) items = scenario.Generate(random);
        var catalog = PackingEngineCatalog.CreateDefault();
        PackResult Pack(string? id)
        {
            var packer = new MultiBinPacker(catalog.Create(id)) { Spacing = 0.125 };
            packer.SetBins([new MultiBin(240, -1, 1)]);
            return packer.Pack(items!);
        }

        var baseline = Pack("bounded");
        var trial = Pack("boundedlastbar");
        Assert.Equal(98, baseline.Bins.Count);
        Assert.Equal(97, trial.Bins.Count);
        Assert.Equal("boundedlastbar", catalog.Default.Id);
        Assert.Equal(trial.Bins.Count, Pack(null).Bins.Count);
        var returned = trial.Bins.SelectMany(b => b.Items).Concat(trial.ItemsNotUsed).ToList();
        Assert.Equal(items!.Count, returned.Count);
        Assert.All(items, item => Assert.Equal(1, returned.Count(i => ReferenceEquals(i, item))));
    }

    [Fact]
    public void Unknown_engine_is_rejected_with_the_valid_ids()
    {
        var ex = Assert.Throws<UnknownPackingEngineException>(() => PackingEngineCatalog.CreateDefault().Resolve("optimal"));

        Assert.Equal("optimal", ex.EngineId);
        Assert.Equal("Unknown packing engine 'optimal'. Available engines: boundedlastbar, bounded, firstfit, bestfit.", ex.Message);
    }

    [Fact]
    public void Retired_engine_ids_are_not_accepted()
    {
        var catalog = PackingEngineCatalog.CreateDefault();
        Assert.Throws<UnknownPackingEngineException>(() => catalog.Resolve("exhaustive"));
        Assert.Throws<UnknownPackingEngineException>(() => catalog.Resolve("lastbar"));
    }

    [Fact]
    public void Configured_default_is_honored_and_validated()
    {
        Assert.Equal("firstfit", PackingEngineCatalog.CreateDefault("FirstFit").Default.Id);
        Assert.Throws<UnknownPackingEngineException>(() => PackingEngineCatalog.CreateDefault("fastest"));
    }

    [Fact]
    public void Create_returns_a_fresh_engine_of_the_registered_type()
    {
        var catalog = PackingEngineCatalog.CreateDefault();

        Assert.IsType<LastBarEliminationEngine>(catalog.Create(null));
        Assert.IsType<LastBarEliminationEngine>(catalog.Create("boundedlastbar"));
        Assert.IsType<FirstFitEngine>(catalog.Create("firstfit"));
        Assert.IsType<BestFitEngine>(catalog.Create("bestfit"));
        Assert.NotSame(catalog.Create("bestfit"), catalog.Create("bestfit"));
    }

    [Fact]
    public void Run_name_notes_a_fallback_engine()
    {
        Assert.Equal("Bounded Search", BuiltInPackingEngines.Bounded.RunName(null));
        Assert.Equal("Bounded Search (First Fit fallback)", BuiltInPackingEngines.Bounded.RunName(BuiltInPackingEngines.FirstFit));
    }

    [Fact]
    public void Any_registered_engine_can_be_created_by_id()
    {
        var catalog = new PackingEngineCatalog(new[] { Registration("first"), Registration("custom") });

        Assert.IsType<FakeEngine>(catalog.Create("custom"));
        Assert.Equal("first", catalog.Default.Id);
    }

    [Fact]
    public void Duplicate_ids_are_rejected_case_insensitively()
    {
        Assert.Throws<ArgumentException>(() => new PackingEngineCatalog(new[] { Registration("a"), Registration("A") }));
    }

    [Fact]
    public void Empty_catalog_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new PackingEngineCatalog(Array.Empty<PackingEngineRegistration>()));
    }
}
