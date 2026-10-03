using CutList.Core.Nesting;
using Xunit;

namespace CutList.Core.Tests.Nesting;

public class ExhaustiveSearchEngineTests
{
    [Fact]
    public void Exhausted_search_budget_still_places_every_part()
    {
        var engine = new ExhaustiveSearchEngine(maxItems: int.MaxValue, maxSearchNodes: 1);
        var items = Enumerable.Range(1, 6).Select(i => new BinItem($"P{i}", 10 + i)).ToList();

        var result = engine.Pack(new PackingRequest(items, 40, 0.125));

        Assert.Equal(6, result.Bins.Sum(b => b.Items.Count));
        Assert.Empty(result.ItemsNotUsed);
    }

    [Fact]
    public void Exhausted_search_budget_stops_the_search_and_uses_the_fallback()
    {
        // Same parts as the optimal-packing test below: a full search finds 2 bars, First Fit needs 3.
        var items = new[] { 4.0, 4, 3, 3, 3, 3 }.Select((length, i) => new BinItem($"P{i}", length)).ToList();
        var request = new PackingRequest(items, 10, 0);
        var fallbackBars = new FirstFitEngine().Pack(request).Bins.Count;

        var result = new ExhaustiveSearchEngine(maxItems: int.MaxValue, maxSearchNodes: 1).Pack(request);

        Assert.Equal(3, fallbackBars);
        Assert.Equal(fallbackBars, result.Bins.Count);
        Assert.Equal(6, result.Bins.Sum(b => b.Items.Count));
        Assert.Same(BuiltInPackingEngines.FirstFit, result.FallbackEngine);
    }

    [Fact]
    public void Above_the_part_threshold_the_fallback_is_recorded()
    {
        var items = new[] { 4.0, 3, 3 }.Select((length, i) => new BinItem($"P{i}", length)).ToList();

        var result = new ExhaustiveSearchEngine(maxItems: 2).Pack(new PackingRequest(items, 10, 0));

        Assert.Same(BuiltInPackingEngines.FirstFit, result.FallbackEngine);
        Assert.Equal(3, result.Bins.Sum(b => b.Items.Count));
    }

    [Fact]
    public void Packer_records_a_fallback_from_any_stock_length()
    {
        // One 48" bar cannot hold both 40" parts, so that stock length falls back to First Fit; the
        // unlimited 120" stock then takes the leftover part with a completed search.
        var packer = new MultiBinPacker(new ExhaustiveSearchEngine()) { Spacing = 0.125 };
        packer.SetBins(new[] { new MultiBin(48, 1, 1), new MultiBin(120, -1, 2) });

        var result = packer.Pack(new List<BinItem> { new("A", 40), new("B", 40) });

        Assert.Same(BuiltInPackingEngines.FirstFit, result.FallbackEngine);
        Assert.Equal(2, result.Bins.Sum(b => b.Items.Count));
        Assert.Empty(result.ItemsNotUsed);
    }

    [Fact]
    public void Default_budget_finds_the_optimal_packing_where_first_fit_needs_an_extra_bar()
    {
        // First-fit decreasing packs {4,4} {3,3,3} {3} = 3 bars; the optimum is {4,3,3} {4,3,3} = 2 bars.
        var items = new[] { 4.0, 4, 3, 3, 3, 3 }.Select((length, i) => new BinItem($"P{i}", length)).ToList();

        var result = new ExhaustiveSearchEngine().Pack(new PackingRequest(items, 10, 0));

        Assert.Equal(2, result.Bins.Count);
        Assert.Equal(6, result.Bins.Sum(b => b.Items.Count));
        Assert.Null(result.FallbackEngine);
    }

    [Fact]
    public void Exactly_enough_limited_stock_still_gets_the_full_search()
    {
        // Two 10.25" bars hold {4,3,3} twice with 1/8" kerfs exactly (the kerf after the last cut runs
        // off the end). First Fit cannot place every part on two bars; the search can.
        var items = new[] { 4.0, 4, 3, 3, 3, 3 }.Select((length, i) => new BinItem($"P{i}", length)).ToList();
        var request = new PackingRequest(items, 10.25, 0.125, maxBinCount: 2);

        var result = new ExhaustiveSearchEngine().Pack(request);

        Assert.Null(result.FallbackEngine);
        Assert.Equal(2, result.Bins.Count);
        Assert.Empty(result.ItemsNotUsed);
        Assert.NotEmpty(new FirstFitEngine().Pack(request).ItemsNotUsed);
    }

    [Fact]
    public void Limited_stock_too_short_for_every_part_gets_the_first_fit_result()
    {
        // 1/16" shorter than the exact fit above, so no complete packing exists.
        var items = new[] { 4.0, 4, 3, 3, 3, 3 }.Select((length, i) => new BinItem($"P{i}", length)).ToList();
        var request = new PackingRequest(items, 10.1875, 0.125, maxBinCount: 2);

        var result = new ExhaustiveSearchEngine().Pack(request);
        var firstFit = new FirstFitEngine().Pack(request);

        Assert.Same(BuiltInPackingEngines.FirstFit, result.FallbackEngine);
        Assert.Equal(firstFit.Bins.Select(b => b.Items.Count), result.Bins.Select(b => b.Items.Count));
        Assert.Equal(firstFit.ItemsNotUsed.Count, result.ItemsNotUsed.Count);
    }

    [Fact]
    public void Lower_bound_lets_the_last_kerf_run_off_the_bar()
    {
        // 4 + 3 + 3 plus two 0.125" kerfs fills a 10.25" bar exactly; the kerf after the last cut may
        // overrun, so {4,3,3} {4,3,3} = 2 bars is optimal.
        var items = new[] { 4.0, 4, 3, 3, 3, 3 }.Select((length, i) => new BinItem($"P{i}", length)).ToList();

        var result = new ExhaustiveSearchEngine().Pack(new PackingRequest(items, 10.25, 0.125));

        Assert.Equal(2, result.Bins.Count);
        Assert.Equal(6, result.Bins.Sum(b => b.Items.Count));
    }
}
