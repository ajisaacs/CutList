using CutList.Core.Nesting.Search;
using Xunit;

namespace CutList.Core.Tests.Nesting.Search;

public class MinBarsSearchTests
{
    private static CutDemand Demand(params double[] lengths) =>
        CutDemand.From(lengths.Select((l, i) => new BinItem($"P{i}", l)), 10, 0);

    [Fact]
    public void Finds_fewer_bars_than_the_upper_bound()
    {
        var d = Demand(4, 4, 3, 3, 3, 3); // First Fit: {4,4} {3,3,3} {3} = 3 bars

        var patterns = new MinBarsSearch(d, new SearchBudget(1_000_000)).Solve(upperBound: 3, maxBars: int.MaxValue);

        Assert.NotNull(patterns);
        Assert.Equal(2, patterns.Count);
        Assert.All(patterns, p => Assert.Equal(new[] { 1, 2 }, p));
    }

    [Fact]
    public void Returns_null_without_searching_when_the_upper_bound_is_optimal()
    {
        var budget = new SearchBudget(1_000_000);

        Assert.Null(new MinBarsSearch(Demand(4, 4, 3, 3, 3, 3), budget).Solve(upperBound: 2, maxBars: int.MaxValue));
        Assert.False(budget.Exhausted);
    }

    [Fact]
    public void Finds_packings_that_fit_only_within_the_tolerance()
    {
        // Each 6.0000000011 + 3.9999999991 pair is 10.0000000002" on a 10" bar: within tolerance.
        var d = Demand(6.0000000011, 6.0000000011, 6.0000000011, 3.9999999991, 3.9999999991, 3.9999999991, 4.0);

        var patterns = new MinBarsSearch(d, new SearchBudget(1_000_000)).Solve(upperBound: int.MaxValue, maxBars: 4);

        Assert.NotNull(patterns);
        Assert.Equal(4, patterns.Count);
    }

    [Fact]
    public void Respects_the_bar_limit()
    {
        Assert.Null(new MinBarsSearch(Demand(4, 4, 3, 3, 3, 3), new SearchBudget(1_000_000))
            .Solve(upperBound: int.MaxValue, maxBars: 1));
    }

    [Fact]
    public void Running_out_of_budget_returns_null_and_says_so()
    {
        var budget = new SearchBudget(1);

        Assert.Null(new MinBarsSearch(Demand(4, 4, 3, 3, 3, 3), budget).Solve(upperBound: 3, maxBars: int.MaxValue));
        Assert.True(budget.Exhausted);
    }
}
