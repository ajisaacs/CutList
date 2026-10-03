using CutList.Core.Nesting.Search;
using Xunit;

namespace CutList.Core.Tests.Nesting.Search;

public class CutPatternsTests
{
    private static CutDemand Demand(params double[] lengths) =>
        CutDemand.From(lengths.Select((l, i) => new BinItem($"P{i}", l)), 10, 0);

    [Fact]
    public void Lists_maximal_patterns_holding_the_required_group_fullest_first()
    {
        var d = Demand(4, 4, 3, 3, 3, 3);

        var patterns = CutPatterns.Maximal(d, d.Counts, mustInclude: 0, maxLength: long.MaxValue, new SearchBudget(1000));

        Assert.Equal(new[] { new[] { 1, 2 }, new[] { 2, 0 } }, patterns.Select(p => p.Pattern));
        Assert.Equal(new[] { CutFit.Units(10), CutFit.Units(8) }, patterns.Select(p => p.Length));
    }

    [Fact]
    public void Max_length_drops_fuller_patterns_and_bars_with_room_are_not_maximal()
    {
        var d = Demand(4, 4, 3, 3, 3, 3);

        var patterns = CutPatterns.Maximal(d, d.Counts, mustInclude: -1, maxLength: CutFit.Units(9), new SearchBudget(1000));

        // {4,3} still has room for a 3, so it is not maximal; {4,3,3} is longer than 9.
        Assert.Equal(new[] { new[] { 0, 3 }, new[] { 2, 0 } }, patterns.Select(p => p.Pattern));
    }

    [Theory]
    [InlineData(6.000005, true)]  // 10.000005" fits a 10" bar within Tolerance.Epsilon
    [InlineData(6.00002, false)]  // 10.00002" does not
    public void Parts_fit_up_to_the_tolerance_and_no_further(double longPart, bool fitsTogether)
    {
        var d = Demand(longPart, 4);

        var patterns = CutPatterns.Maximal(d, d.Counts, mustInclude: -1, maxLength: long.MaxValue, new SearchBudget(1000));

        Assert.Equal(fitsTogether, patterns.Any(p => p.Pattern.SequenceEqual(new[] { 1, 1 })));
    }

    [Fact]
    public void Budget_stops_the_search_and_says_so()
    {
        var budget = new SearchBudget(1);
        budget.Charge();

        Assert.Throws<SearchBudgetExceededException>(() => budget.Charge());
        Assert.True(budget.Exhausted);
    }
}
