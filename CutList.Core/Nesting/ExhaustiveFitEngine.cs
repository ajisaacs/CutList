namespace CutList.Core.Nesting
{
    /// <summary>
    /// Exhaustive bin packing engine that searches arrangements for the fewest bins (then least
    /// waste). Falls back to AdvancedFitEngine when there are more items than the threshold, when
    /// limited stock cannot hold every item, or when the search-node budget runs out before any
    /// complete packing is found.
    /// </summary>
    public class ExhaustiveFitEngine : IEngine
    {
        /// <summary>
        /// Default maximum number of items before falling back to AdvancedFitEngine.
        /// Testing showed 25 items is safe (~84ms worst case), while 30+ can take seconds.
        /// </summary>
        public const int DefaultMaxItems = 25;

        /// <summary>
        /// Search nodes explored before the engine stops and keeps the best complete packing found
        /// so far (or falls back when none is complete). Bounds worst-case time when limited stock
        /// makes bin-count pruning ineffective.
        /// </summary>
        public const int DefaultMaxSearchNodes = 2_000_000;

        private readonly IEngine _fallbackEngine;
        private readonly int _maxItems;
        private readonly int _maxSearchNodes;

        public ExhaustiveFitEngine() : this(DefaultMaxItems)
        {
        }

        /// <summary>
        /// Creates an exhaustive engine with custom limits (used by tests).
        /// </summary>
        /// <param name="maxItems">Maximum items before falling back. Use int.MaxValue to disable fallback.</param>
        /// <param name="maxSearchNodes">Search-node budget; see <see cref="DefaultMaxSearchNodes"/>.</param>
        public ExhaustiveFitEngine(int maxItems, int maxSearchNodes = DefaultMaxSearchNodes)
        {
            _maxItems = maxItems;
            _maxSearchNodes = maxSearchNodes;
            _fallbackEngine = new AdvancedFitEngine();
        }

        public PackResult Pack(PackingRequest request)
        {
            // Filter oversized items first
            var validItems = new List<BinItem>();
            var oversizedItems = new List<BinItem>();

            foreach (var item in request.Items)
            {
                if (item.Length > request.StockLength)
                    oversizedItems.Add(item);
                else
                    validItems.Add(item);
            }

            // Fall back to AdvancedFit for large item counts
            if (validItems.Count > _maxItems)
            {
                var fallbackResult = _fallbackEngine.Pack(request);
                return fallbackResult;
            }

            // Sort items descending for better pruning
            var sortedItems = validItems.OrderByDescending(i => i.Length).ToList();

            // Find optimal solution using exhaustive search
            var bestSolution = new SearchState
            {
                Bins = new List<List<BinItem>>(),
                BinCount = int.MaxValue
            };

            var currentState = new SearchState
            {
                Bins = new List<List<BinItem>>(),
                BinCount = 0
            };

            // Precompute suffix sums of item lengths (including spacing per item)
            // for lower-bound pruning. suffixVolume[i] = total volume of items[i..n-1].
            var suffixVolume = new double[sortedItems.Count + 1];
            for (int i = sortedItems.Count - 1; i >= 0; i--)
            {
                suffixVolume[i] = suffixVolume[i + 1] + sortedItems[i].Length + request.Spacing;
            }

            Search(sortedItems, 0, currentState, bestSolution, request, suffixVolume);

            // The search only scores packings that place every part. When limited stock cannot hold
            // them all, let the fallback engine place what fits and report the rest as not placed.
            if (bestSolution.BinCount == int.MaxValue)
            {
                return _fallbackEngine.Pack(request);
            }

            // Build result from best solution
            var result = new PackResult();
            result.AddItemsNotUsed(oversizedItems);

            foreach (var binItems in bestSolution.Bins)
            {
                var bin = new Bin(request.StockLength) { Spacing = request.Spacing };
                foreach (var item in binItems.OrderByDescending(i => i.Length))
                {
                    bin.AddItem(item);
                }
                result.AddBin(bin);
            }

            // Sort bins by utilization
            var sortedBins = result.Bins
                .OrderByDescending(b => b.Utilization)
                .ThenBy(b => b.Items.Count)
                .ToList();

            var finalResult = new PackResult();
            finalResult.AddItemsNotUsed(oversizedItems);
            finalResult.AddBins(sortedBins);

            return finalResult;
        }

        private void Search(
            List<BinItem> items,
            int itemIndex,
            SearchState current,
            SearchState best,
            PackingRequest request,
            double[] suffixVolume)
        {
            // All items placed - check if this is better
            if (itemIndex >= items.Count)
            {
                if (current.BinCount < best.BinCount ||
                    (current.BinCount == best.BinCount && GetTotalWaste(current, request) < GetTotalWaste(best, request)))
                {
                    best.BinCount = current.BinCount;
                    best.Bins = current.Bins.Select(b => b.ToList()).ToList();
                }
                return;
            }

            // Budget: stop exploring; the caller keeps the best complete packing (or falls back).
            if (++current.NodesVisited > _maxSearchNodes)
                return;

            // Pruning: if we already have more bins than best, stop
            if (current.BinCount >= best.BinCount)
                return;

            // Lower-bound pruning: remaining items need at least this many additional bins
            double remainingVolume = suffixVolume[itemIndex];
            double availableInExisting = 0;
            for (int b = 0; b < current.Bins.Count; b++)
            {
                availableInExisting += request.StockLength - GetBinUsedLength(current.Bins[b], request.Spacing);
            }
            double overflow = remainingVolume - availableInExisting;
            int additionalBinsNeeded = overflow > 0 ? (int)Math.Ceiling(overflow / request.StockLength) : 0;
            if (current.BinCount + additionalBinsNeeded >= best.BinCount)
                return;

            var item = items[itemIndex];

            // Symmetry breaking: if this item has the same length as the previous item,
            // only place it in bins with index >= where previous item went.
            // This avoids redundant exploration of equivalent permutations.
            int minBinIndex = 0;
            if (itemIndex > 0 && items[itemIndex - 1].Length == item.Length)
            {
                minBinIndex = current.LastBinIndexUsed;
            }

            // Try placing in each existing bin (respecting symmetry constraint)
            for (int i = minBinIndex; i < current.Bins.Count; i++)
            {
                var binUsed = GetBinUsedLength(current.Bins[i], request.Spacing);
                var remaining = request.StockLength - binUsed;

                // Item fits if adding it (with spacing) stays within tolerance
                // Bin class allows going over by up to spacing amount
                if (item.Length <= remaining)
                {
                    // Place item in this bin
                    current.Bins[i].Add(item);
                    var prevBinIndex = current.LastBinIndexUsed;
                    current.LastBinIndexUsed = i;
                    Search(items, itemIndex + 1, current, best, request, suffixVolume);
                    current.LastBinIndexUsed = prevBinIndex;
                    current.Bins[i].RemoveAt(current.Bins[i].Count - 1);
                }
            }

            // Try placing in a new bin (if allowed)
            if (current.BinCount < request.MaxBinCount && current.BinCount < best.BinCount)
            {
                int newBinIndex = current.Bins.Count;
                current.Bins.Add(new List<BinItem> { item });
                current.BinCount++;
                var prevBinIndex = current.LastBinIndexUsed;
                current.LastBinIndexUsed = newBinIndex;
                Search(items, itemIndex + 1, current, best, request, suffixVolume);
                current.LastBinIndexUsed = prevBinIndex;
                current.Bins.RemoveAt(current.Bins.Count - 1);
                current.BinCount--;
            }
        }

        private double GetBinUsedLength(List<BinItem> binItems, double spacing)
        {
            if (binItems.Count == 0)
                return 0;

            return binItems.Sum(i => i.Length) + binItems.Count * spacing;
        }

        private double GetTotalWaste(SearchState state, PackingRequest request)
        {
            double totalWaste = 0;
            foreach (var bin in state.Bins)
            {
                var used = GetBinUsedLength(bin, request.Spacing);
                totalWaste += request.StockLength - used;
            }
            return totalWaste;
        }

        private class SearchState
        {
            public List<List<BinItem>> Bins { get; set; } = new();
            public int BinCount { get; set; }
            public int LastBinIndexUsed { get; set; }
            public int NodesVisited { get; set; }
        }
    }
}
