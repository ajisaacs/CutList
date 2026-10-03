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
    public void Built_in_catalog_lists_engines_in_order_with_exhaustive_as_default()
    {
        var catalog = PackingEngineCatalog.CreateDefault();

        Assert.Equal(new[] { "exhaustive", "firstfit", "bestfit" }, catalog.Engines.Select(e => e.Id));
        Assert.Equal("exhaustive", catalog.Default.Id);
        Assert.All(catalog.Engines, e => Assert.False(string.IsNullOrWhiteSpace(e.Description)));
    }

    [Theory]
    [InlineData("BestFit", "bestfit")]
    [InlineData(" firstfit ", "firstfit")]
    [InlineData(null, "exhaustive")]
    [InlineData("", "exhaustive")]
    [InlineData("   ", "exhaustive")]
    public void Resolve_is_case_insensitive_and_blank_means_default(string? requested, string expected)
    {
        Assert.Equal(expected, PackingEngineCatalog.CreateDefault().Resolve(requested).Id);
    }

    [Fact]
    public void Unknown_engine_is_rejected_with_the_valid_ids()
    {
        var ex = Assert.Throws<UnknownPackingEngineException>(() => PackingEngineCatalog.CreateDefault().Resolve("optimal"));

        Assert.Equal("optimal", ex.EngineId);
        Assert.Equal("Unknown packing engine 'optimal'. Available engines: exhaustive, firstfit, bestfit.", ex.Message);
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

        Assert.IsType<ExhaustiveSearchEngine>(catalog.Create(null));
        Assert.IsType<FirstFitEngine>(catalog.Create("firstfit"));
        Assert.IsType<BestFitEngine>(catalog.Create("bestfit"));
        Assert.NotSame(catalog.Create("bestfit"), catalog.Create("bestfit"));
    }

    [Fact]
    public void Run_name_notes_a_fallback_engine()
    {
        Assert.Equal("Exhaustive", BuiltInPackingEngines.Exhaustive.RunName(null));
        Assert.Equal("Exhaustive (First Fit fallback)", BuiltInPackingEngines.Exhaustive.RunName(BuiltInPackingEngines.FirstFit));
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
