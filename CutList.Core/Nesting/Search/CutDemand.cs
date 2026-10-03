namespace CutList.Core.Nesting.Search
{
    /// <summary>Parts grouped by distinct length (longest first) with kerf-inclusive sizes.</summary>
    internal sealed class CutDemand
    {
        /// <summary>Floating-point noise allowance for comparing computed sums; far below the fit tolerance.</summary>
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

        /// <summary>
        /// What one bar holds: parts fit when their sizes total at most Capacity + Tolerance.Epsilon,
        /// the same tolerance Bin (and OpenNest) uses, i.e. the cut length may exceed the stock by that much.
        /// </summary>
        public double FitCapacity => Capacity + Tolerance.Epsilon;

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
        /// Fewest bars any packing of <paramref name="remaining"/> needs: the Martello-Toth L2 bound for
        /// bars of <see cref="FitCapacity"/>. Every comparison leans toward a smaller bound by Eps, so
        /// floating-point noise can never make it exceed a packing the search accepts.
        /// </summary>
        public int LowerBound(int[] remaining)
        {
            double capacity = FitCapacity;
            double half = capacity / 2;
            double total = 0;
            for (int i = 0; i < remaining.Length; i++) total += remaining[i] * Sizes[i];
            int best = CeilingDown(total / capacity);

            // Thresholds: 0 and every remaining size up to half a bar.
            for (int t = -1; t < remaining.Length; t++)
            {
                double alpha = t < 0 ? 0 : Sizes[t];
                if (t >= 0 && (remaining[t] == 0 || alpha > half)) continue;
                int large = 0, medium = 0;
                double mediumSize = 0, smallSize = 0;
                for (int i = 0; i < remaining.Length; i++)
                {
                    if (remaining[i] == 0) continue;
                    double s = Sizes[i];
                    if (s > capacity - alpha + Eps) large += remaining[i];         // no part >= alpha fits beside it
                    else if (s > half + Eps) { medium += remaining[i]; mediumSize += remaining[i] * s; } // one per bar
                    else if (s >= alpha) smallSize += remaining[i] * s;
                }
                double overflow = smallSize - (medium * capacity - mediumSize);
                int bound = large + medium + (overflow > 0 ? CeilingDown(overflow / capacity) : 0);
                if (bound > best) best = bound;
            }
            return best;
        }

        private static int CeilingDown(double bars) => (int)Math.Ceiling(bars - Eps);

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
