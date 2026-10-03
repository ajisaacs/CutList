using CutList.Core.Nesting;
using Xunit;

namespace CutList.Core.Tests.Nesting;

public class ExhaustiveFitEngineTests
{
    [Fact]
    public void Exhausted_search_budget_still_places_every_part()
    {
        var engine = new ExhaustiveFitEngine(maxItems: int.MaxValue, maxSearchNodes: 1);
        var items = Enumerable.Range(1, 6).Select(i => new BinItem($"P{i}", 10 + i)).ToList();

        var result = engine.Pack(new PackingRequest(items, 40, 0.125));

        Assert.Equal(6, result.Bins.Sum(b => b.Items.Count));
        Assert.Empty(result.ItemsNotUsed);
    }

    [Fact]
    public void Exhausted_search_budget_stops_the_search_and_uses_the_fallback()
    {
        // Same parts as the optimal-packing test below: a full search finds 2 bars, Advanced Fit needs 3.
        var items = new[] { 4.0, 4, 3, 3, 3, 3 }.Select((length, i) => new BinItem($"P{i}", length)).ToList();
        var request = new PackingRequest(items, 10, 0);
        var fallbackBars = new AdvancedFitEngine().Pack(request).Bins.Count;

        var result = new ExhaustiveFitEngine(maxItems: int.MaxValue, maxSearchNodes: 1).Pack(request);

        Assert.Equal(3, fallbackBars);
        Assert.Equal(fallbackBars, result.Bins.Count);
        Assert.Equal(6, result.Bins.Sum(b => b.Items.Count));
    }

    [Fact]
    public void Default_budget_finds_the_optimal_packing_where_first_fit_needs_an_extra_bar()
    {
        // First-fit decreasing packs {4,4} {3,3,3} {3} = 3 bars; the optimum is {4,3,3} {4,3,3} = 2 bars.
        var items = new[] { 4.0, 4, 3, 3, 3, 3 }.Select((length, i) => new BinItem($"P{i}", length)).ToList();

        var result = new ExhaustiveFitEngine().Pack(new PackingRequest(items, 10, 0));

        Assert.Equal(2, result.Bins.Count);
        Assert.Equal(6, result.Bins.Sum(b => b.Items.Count));
    }

    [Fact]
    public void Lower_bound_lets_the_last_kerf_run_off_the_bar()
    {
        // 4 + 3 + 3 plus two 0.125" kerfs fills a 10.25" bar exactly; the kerf after the last cut may
        // overrun, so {4,3,3} {4,3,3} = 2 bars is optimal.
        var items = new[] { 4.0, 4, 3, 3, 3, 3 }.Select((length, i) => new BinItem($"P{i}", length)).ToList();

        var result = new ExhaustiveFitEngine().Pack(new PackingRequest(items, 10.25, 0.125));

        Assert.Equal(2, result.Bins.Count);
        Assert.Equal(6, result.Bins.Sum(b => b.Items.Count));
    }
}
