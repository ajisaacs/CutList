using CutList.Core.Nesting;
using Xunit;

namespace CutList.Core.Tests.Nesting;

public class ExhaustiveOracleTests
{
    [Fact]
    public void Uses_the_fewest_bars_a_brute_force_search_finds()
    {
        // Dyadic lengths and kerf so float noise cannot hide a wrong bound.
        var rng = new Random(77);
        for (int t = 0; t < 400; t++)
        {
            double kerf = rng.Next(2) == 0 ? 0 : 0.125;
            double stock = rng.Next(8, 41) / 2.0;
            var items = Enumerable.Range(0, rng.Next(1, 10))
                .Select(i => new BinItem($"I{i}", rng.Next(2, (int)(stock * 4) + 1) / 4.0)).ToList();

            var result = new ExhaustiveSearchEngine().Pack(new PackingRequest(items, stock, kerf));

            var sizes = items.Where(i => i.Length <= stock).Select(i => CutFit.Size(i.Length, kerf)).ToArray();
            Assert.Equal(BruteForceFewestBars(sizes, CutFit.Capacity(stock, kerf)), result.Bins.Count);
            Assert.Equal(items.Count - sizes.Length, result.ItemsNotUsed.Count);
        }
    }

    // Tries every assignment of parts (longest first) to bars, with CutFit's fit rule.
    private static int BruteForceFewestBars(long[] sizes, long capacity)
    {
        var s = sizes.OrderByDescending(x => x).ToArray();
        int best = s.Length;
        var bins = new List<long>();
        Go(0);
        return best;

        void Go(int i)
        {
            if (bins.Count >= best) return;
            if (i == s.Length) { best = bins.Count; return; }
            for (int b = 0; b < bins.Count; b++)
            {
                if (bins[b] + s[i] > capacity) continue;
                bins[b] += s[i];
                Go(i + 1);
                bins[b] -= s[i];
            }
            bins.Add(s[i]);
            Go(i + 1);
            bins.RemoveAt(bins.Count - 1);
        }
    }
}
