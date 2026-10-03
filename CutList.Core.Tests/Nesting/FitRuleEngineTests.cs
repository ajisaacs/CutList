using CutList.Core.Nesting;
using Xunit;

namespace CutList.Core.Tests.Nesting;

public class FitRuleEngineTests
{
    private static List<BinItem> Items(params double[] lengths) =>
        lengths.Select((l, i) => new BinItem($"P{i}", l)).ToList();

    [Fact]
    public void First_fit_places_a_part_as_long_as_a_bar_with_more_than_eight_decimals()
    {
        // Bin rounds remaining length to 8 decimals, so this part never "fit" an empty bar: First Fit
        // opened empty bars up to the limit (or forever on unlimited stock).
        var item = new BinItem("A", 13.900000001);

        var result = new FirstFitEngine().Pack(new PackingRequest(new[] { item }, 13.900000001, 0, maxBinCount: 2));

        Assert.Same(item, Assert.Single(Assert.Single(result.Bins).Items));
        Assert.Empty(result.ItemsNotUsed);
    }

    [Fact]
    public void Exhaustive_places_a_part_as_long_as_a_bar_on_unlimited_stock()
    {
        // Exhaustive starts from First Fit's plan, so the old rounding made it loop forever here.
        var item = new BinItem("A", 13.900000001);

        var result = new ExhaustiveSearchEngine().Pack(new PackingRequest(new[] { item }, 13.900000001, 0));

        Assert.Same(item, Assert.Single(Assert.Single(result.Bins).Items));
    }

    [Fact]
    public void First_fit_fills_a_bar_to_the_tolerance()
    {
        // {4,3,3} plus two 0.1" kerfs is 10.2": the 10.19999" bar plus exactly the 0.00001" tolerance.
        var result = new FirstFitEngine().Pack(new PackingRequest(Items(4, 3, 3), 10.19999, 0.1));

        Assert.Single(result.Bins);
    }

    [Fact]
    public void Best_fit_fills_a_bar_to_the_tolerance()
    {
        var result = new BestFitEngine().Pack(new PackingRequest(Items(4, 3, 3), 10.19999, 0.1));

        Assert.Single(result.Bins);
    }
}
