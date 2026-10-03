namespace CutList.Core.Nesting.Search
{
    /// <summary>Parts grouped by distinct length (longest first) with kerf-inclusive sizes.</summary>
    internal sealed class CutDemand
    {
        public const double Eps = 1e-9;

        private CutDemand(double[] lengths, int[] counts, double kerf, double stockLength, List<BinItem>[] items)
        {
            Lengths = lengths;
            Counts = counts;
            Sizes = lengths.Select(l => l + kerf).ToArray();
            Capacity = stockLength + kerf;
            StockLength = stockLength;
            Kerf = kerf;
            Items = items;
        }

        public double[] Lengths { get; }
        public int[] Counts { get; }

        /// <summary>Length plus one kerf: what a part uses of a bar.</summary>
        public double[] Sizes { get; }

        /// <summary>Stock length plus one kerf: the kerf after the last cut may run off the end.</summary>
        public double Capacity { get; }

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
        /// Fewest bars any packing of <paramref name="remaining"/> needs: the Martello-Toth L2 bound
        /// (covers total size and parts over half a bar).
        /// </summary>
        public int LowerBound(int[] remaining)
        {
            double half = Capacity / 2;
            double total = 0;
            for (int i = 0; i < remaining.Length; i++) total += remaining[i] * Sizes[i];
            int best = (int)Math.Ceiling(total / Capacity - Eps);

            // Thresholds: 0 and every remaining size up to half a bar.
            for (int t = -1; t < remaining.Length; t++)
            {
                double alpha = t < 0 ? 0 : Sizes[t];
                if (t >= 0 && (remaining[t] == 0 || alpha > half + Eps)) continue;
                int large = 0, medium = 0;
                double mediumSize = 0, smallSize = 0;
                for (int i = 0; i < remaining.Length; i++)
                {
                    if (remaining[i] == 0) continue;
                    double s = Sizes[i];
                    if (s > Capacity - alpha + Eps) large += remaining[i];
                    else if (s > half + Eps) { medium += remaining[i]; mediumSize += remaining[i] * s; }
                    else if (s >= alpha - Eps) smallSize += remaining[i] * s;
                }
                double overflow = smallSize - (medium * Capacity - mediumSize);
                int bound = large + medium + (overflow > Eps ? (int)Math.Ceiling(overflow / Capacity - Eps) : 0);
                if (bound > best) best = bound;
            }
            return best;
        }

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
