using CutList.Core.Nesting.Search;
using Xunit;

namespace CutList.Core.Tests.Nesting.Search;

public class CutDemandTests
{
    private static CutDemand Demand(double stock, double kerf, params double[] lengths) =>
        CutDemand.From(lengths.Select((l, i) => new BinItem($"P{i}", l)), stock, kerf);

    [Fact]
    public void Groups_parts_by_length_longest_first_with_kerf_sizes()
    {
        var d = Demand(10.25, 0.125, 3, 4, 3, 4, 3, 3);

        Assert.Equal(new[] { 4.0, 3.0 }, d.Lengths);
        Assert.Equal(new[] { 2, 4 }, d.Counts);
        Assert.Equal(new[] { 4.125, 3.125 }, d.Sizes);
        Assert.Equal(10.375, d.Capacity);
    }

    [Theory]
    [InlineData(10.0, 0.0, new[] { 6.0, 6, 6, 6 }, 4)]              // over half a bar: one per bar
    [InlineData(10.0, 0.0, new[] { 5.0, 5, 5, 5 }, 2)]              // exactly half: two per bar
    [InlineData(10.0, 0.0, new[] { 4.0, 4, 3, 3, 3, 3 }, 2)]
    [InlineData(10.25, 0.125, new[] { 4.0, 4, 3, 3, 3, 3 }, 2)] // last kerf may run off the bar
    [InlineData(10.1875, 0.125, new[] { 4.0, 4, 3, 3, 3, 3 }, 3)]
    public void Lower_bound(double stock, double kerf, double[] lengths, int expected)
    {
        var d = Demand(stock, kerf, lengths);

        Assert.Equal(expected, d.LowerBound(d.Counts));
    }
}
