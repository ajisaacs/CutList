using CutList.Core.Nesting;
using Xunit;

namespace CutList.Core.Tests.Nesting;

public class ExhaustiveSearchEngineTests
{
    [Fact]
    public void Exhausted_search_budget_still_places_every_part()
    {
        var engine = new ExhaustiveSearchEngine(searchBudget: 1);
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

        var result = new ExhaustiveSearchEngine(searchBudget: 1).Pack(request);

        Assert.Equal(3, fallbackBars);
        Assert.Equal(fallbackBars, result.Bins.Count);
        Assert.Equal(6, result.Bins.Sum(b => b.Items.Count));
        Assert.Same(BuiltInPackingEngines.FirstFit, result.FallbackEngine);
    }

    [Fact]
    public void Searches_jobs_with_more_than_25_parts()
    {
        // 10 x 4" and 20 x 3" on 10" bars: First Fit cuts {4,4} five times and {3,3,3}/{3,3} seven
        // times (12 bars); {4,3,3} ten times uses 10.
        var items = Enumerable.Repeat(4.0, 10).Concat(Enumerable.Repeat(3.0, 20))
            .Select((length, i) => new BinItem($"P{i}", length)).ToList();
        var request = new PackingRequest(items, 10, 0);

        var result = new ExhaustiveSearchEngine().Pack(request);

        Assert.Equal(12, new FirstFitEngine().Pack(request).Bins.Count);
        Assert.Equal(10, result.Bins.Count);
        Assert.Empty(result.ItemsNotUsed);
        Assert.Null(result.FallbackEngine);
    }

    [Fact]
    public void Packer_records_a_fallback_from_any_stock_length()
    {
        // A one-step budget runs out on the limited 48" stock, so that length keeps First Fit's plan and
        // reports the fallback; the 120" stock's First Fit plan is proven optimal without searching.
        var packer = new MultiBinPacker(new ExhaustiveSearchEngine(searchBudget: 1)) { Spacing = 0.125 };
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
    public void Limited_stock_too_short_keeps_first_fit_when_it_is_already_fullest()
    {
        // 1/16" shorter than the exact fit above, so no complete packing exists. Two bars hold at most
        // {3,3,3} + {4,4} = 17", which is First Fit's plan, so it is kept without a fallback.
        var items = new[] { 4.0, 4, 3, 3, 3, 3 }.Select((length, i) => new BinItem($"P{i}", length)).ToList();
        var request = new PackingRequest(items, 10.1875, 0.125, maxBinCount: 2);

        var result = new ExhaustiveSearchEngine().Pack(request);
        var firstFit = new FirstFitEngine().Pack(request);

        Assert.Null(result.FallbackEngine);
        Assert.Equal(firstFit.Bins.Select(b => b.Items.Count), result.Bins.Select(b => b.Items.Count));
        Assert.Equal(firstFit.ItemsNotUsed.Count, result.ItemsNotUsed.Count);
    }

    [Fact]
    public void Limited_stock_gets_its_fullest_bars()
    {
        // One 10" bar: First Fit cuts {5,4} = 9"; the fullest bar is {4,3,3} = 10".
        var items = new[] { 5.0, 4, 3, 3 }.Select((l, i) => new BinItem($"P{i}", l)).ToList();

        var result = new ExhaustiveSearchEngine().Pack(new PackingRequest(items, 10, 0, maxBinCount: 1));

        Assert.Equal(new[] { 4.0, 3, 3 }, Assert.Single(result.Bins).Items.Select(i => i.Length));
        Assert.Equal(5.0, Assert.Single(result.ItemsNotUsed).Length);
        Assert.Null(result.FallbackEngine);
    }

    [Fact]
    public void Keeps_first_fit_when_the_fullest_bar_strands_parts_that_need_more_bars()
    {
        // One 20" bar. The fullest cut is {7,6,4,3} = 20", but it leaves 15,15,15,12,9,9,8,7, which need
        // 6 more bars; First Fit's {15,4} = 19" leaves parts that need 5.
        var items = new[] { 15.0, 12, 7, 9, 6, 8, 15, 7, 9, 15, 4, 3 }.Select((l, i) => new BinItem($"P{i}", l)).ToList();

        var result = new ExhaustiveSearchEngine().Pack(new PackingRequest(items, 20, 0, maxBinCount: 1));

        Assert.Equal(new[] { 15.0, 4 }, Assert.Single(result.Bins).Items.Select(i => i.Length));
        Assert.Null(result.FallbackEngine);
    }

    [Fact]
    public void Exactly_full_decimal_bars_report_no_negative_waste()
    {
        // Ten {4,3,3} bars of 10.2" with 0.1" kerf are exactly full (the last kerf runs off the end).
        var items = Enumerable.Repeat(4.0, 10).Concat(Enumerable.Repeat(3.0, 20))
            .Select((length, i) => new BinItem($"P{i}", length)).ToList();

        var result = new ExhaustiveSearchEngine().Pack(new PackingRequest(items, 10.2, 0.1));

        Assert.Equal(10, result.Bins.Count);
        Assert.All(result.Bins, b =>
        {
            Assert.Equal(0, b.RemainingLength);
            Assert.Equal(1, b.Utilization);
        });
    }

    [Fact]
    public void Four_limited_bars_hold_parts_that_pair_within_the_tolerance()
    {
        // Three 6.0000000011 + 3.9999999991 pairs (10.0000000002" each, within tolerance) plus a 4".
        var items = new[] { 6.0000000011, 6.0000000011, 6.0000000011, 3.9999999991, 3.9999999991, 3.9999999991, 4.0 }
            .Select((length, i) => new BinItem($"P{i}", length)).ToList();

        var result = new ExhaustiveSearchEngine().Pack(new PackingRequest(items, 10, 0, maxBinCount: 4));

        Assert.Equal(4, result.Bins.Count);
        Assert.Empty(result.ItemsNotUsed);
        Assert.Null(result.FallbackEngine);
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
