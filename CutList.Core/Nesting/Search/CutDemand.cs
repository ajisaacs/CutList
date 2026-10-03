namespace CutList.Core.Nesting.Search
{
    /// <summary>
    /// Parts grouped by distinct length (longest first). Sizes, capacity and lengths are in whole
    /// <see cref="CutFit"/> units, so every fit decision and bound is exact integer arithmetic.
    /// </summary>
    internal sealed class CutDemand
    {
        private CutDemand(double[] lengths, int[] counts, double kerf, double stockLength, List<BinItem>[] items)
        {
            Lengths = lengths;
            Counts = counts;
            LengthUnits = lengths.Select(CutFit.Units).ToArray();
            Sizes = lengths.Select(l => CutFit.Size(l, kerf)).ToArray();
            Capacity = CutFit.Capacity(stockLength, kerf);
            StockLength = stockLength;
            Kerf = kerf;
            Items = items;
        }

        public double[] Lengths { get; }
        public int[] Counts { get; }

        /// <summary>Part lengths in units (the amount of material a pattern uses).</summary>
        public long[] LengthUnits { get; }

        /// <summary>Length plus one kerf, in units: what a part uses of a bar.</summary>
        public long[] Sizes { get; }

        /// <summary>What one bar holds, in units: parts fit when their sizes total at most this (see CutFit).</summary>
        public long Capacity { get; }

        public double StockLength { get; }
        public double Kerf { get; }
        public List<BinItem>[] Items { get; }
        public int GroupCount => Lengths.Length;

        public static CutDemand From(IEnumerable<BinItem> items, double stockLength, double kerf)
        {
            var groups = items.GroupBy(i => i.Length).OrderByDescending(g => g.Key).ToList();
            return new CutDemand(
                groups.Select(g => g.Key).ToArray(),
                groups.Select(g => g.Count()).ToArray(),
                kerf,
                stockLength,
                groups.Select(g => g.ToList()).ToArray());
        }

        /// <summary>
        /// Fewest bars any packing of <paramref name="remaining"/> needs: the Martello-Toth L2 bound,
        /// exact in integer units (covers total size and parts over half a bar).
        /// </summary>
        public int LowerBound(int[] remaining)
        {
            long total = 0;
            for (int i = 0; i < remaining.Length; i++) total += remaining[i] * Sizes[i];
            int best = (int)CeilingDivide(total, Capacity);

            // Thresholds: 0 and every remaining size up to half a bar.
            for (int t = -1; t < remaining.Length; t++)
            {
                long alpha = t < 0 ? 0 : Sizes[t];
                if (t >= 0 && (remaining[t] == 0 || 2 * alpha > Capacity)) continue;
                int large = 0, medium = 0;
                long mediumSize = 0, smallSize = 0;
                for (int i = 0; i < remaining.Length; i++)
                {
                    if (remaining[i] == 0) continue;
                    long s = Sizes[i];
                    if (s > Capacity - alpha) large += remaining[i];                                   // no part >= alpha fits beside it
                    else if (2 * s > Capacity) { medium += remaining[i]; mediumSize += remaining[i] * s; } // one per bar
                    else if (s >= alpha) smallSize += remaining[i] * s;
                }
                long overflow = smallSize - (medium * Capacity - mediumSize);
                int bound = large + medium + (overflow > 0 ? (int)CeilingDivide(overflow, Capacity) : 0);
                if (bound > best) best = bound;
            }
            return best;
        }

        private static long CeilingDivide(long value, long divisor) => (value + divisor - 1) / divisor;

        /// <summary>Builds bars from patterns, taking the actual parts (by reference) group by group.</summary>
        public (List<Bin> Bins, List<BinItem> NotUsed) Build(IEnumerable<int[]> patterns)
        {
            var queues = Items.Select(g => new Queue<BinItem>(g)).ToArray();
            var bins = new List<Bin>();
            foreach (var pattern in patterns)
            {
                var bin = new Bin(StockLength) { Spacing = Kerf };
                for (int i = 0; i < pattern.Length; i++)
                    for (int n = 0; n < pattern[i]; n++)
                        bin.AddItem(queues[i].Dequeue());
                bins.Add(bin);
            }
            return (bins, queues.SelectMany(q => q).ToList());
        }
    }
}
