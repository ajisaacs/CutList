using CutList.Core.Nesting.Search;
using Xunit;

namespace CutList.Core.Tests.Nesting.Search;

public class MaxFillSearchTests
{
    private static CutDemand Demand(double stock, double kerf, params double[] lengths) =>
        CutDemand.From(lengths.Select((l, i) => new BinItem($"P{i}", l)), stock, kerf);

    [Fact]
    public void Fills_a_bar_more_fully_than_the_incumbent()
    {
        var search = new MaxFillSearch(Demand(10, 0, 5, 4, 3, 3), new SearchBudget(1_000_000));

        var patterns = search.Solve(bars: 1, incumbentLength: CutFit.Units(9)); // First Fit cuts {5,4}

        Assert.True(search.Completed);
        Assert.Equal(new[] { 0, 1, 2 }, Assert.Single(patterns!)); // {4,3,3} = 10
    }

    [Fact]
    public void Returns_null_when_the_incumbent_is_already_fullest()
    {
        var search = new MaxFillSearch(Demand(10, 0, 5, 4, 3, 3), new SearchBudget(1_000_000));

        Assert.Null(search.Solve(bars: 1, incumbentLength: CutFit.Units(10)));
        Assert.True(search.Completed);
    }

    [Fact]
    public void Running_out_of_budget_is_reported()
    {
        var search = new MaxFillSearch(Demand(10, 0, 5, 4, 3, 3), new SearchBudget(1));

        search.Solve(bars: 1, incumbentLength: 0);

        Assert.False(search.Completed);
    }

    [Fact]
    public void Places_as_much_length_as_a_brute_force_search()
    {
        var rng = new Random(78);
        for (int t = 0; t < 300; t++)
        {
            double kerf = rng.Next(2) == 0 ? 0 : 0.125;
            double stock = rng.Next(8, 41) / 2.0;
            var lengths = Enumerable.Range(0, rng.Next(2, 9))
                .Select(_ => rng.Next(2, (int)(stock * 4) + 1) / 4.0).ToArray();
            int bars = rng.Next(1, 4);
            var d = Demand(stock, kerf, lengths);

            var search = new MaxFillSearch(d, new SearchBudget(50_000_000));
            var patterns = search.Solve(bars, incumbentLength: 0) ?? new List<int[]>();
            long placed = patterns.Sum(p => p.Select((n, g) => n * d.LengthUnits[g]).Sum());

            Assert.True(search.Completed);
            Assert.True(patterns.Count <= bars);
            Assert.Equal(BruteForceMostLength(lengths.Where(l => l <= stock).ToArray(), kerf, stock, bars), placed);
        }
    }

    // Tries every assignment of parts to bars (or to no bar), with CutFit's fit rule.
    private static long BruteForceMostLength(double[] lengths, double kerf, double stock, int bars)
    {
        long capacity = CutFit.Capacity(stock, kerf);
        long best = 0;
        var used = new long[bars];
        Go(0, 0);
        return best;

        void Go(int i, long placed)
        {
            if (i == lengths.Length) { best = Math.Max(best, placed); return; }
            Go(i + 1, placed);
            long size = CutFit.Size(lengths[i], kerf);
            for (int b = 0; b < bars; b++)
            {
                if (used[b] + size > capacity) continue;
                used[b] += size;
                Go(i + 1, placed + CutFit.Units(lengths[i]));
                used[b] -= size;
            }
        }
    }
}
