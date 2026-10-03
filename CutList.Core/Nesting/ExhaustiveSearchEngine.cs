using CutList.Core.Nesting.Search;

namespace CutList.Core.Nesting
{
    /// <summary>
    /// Exhaustive engine: groups parts by length and searches cut patterns for the fewest bars,
    /// starting from the First Fit plan. Returns First Fit's plan, reported as a fallback, when limited
    /// stock cannot hold every part or the search budget runs out before it finds a better plan.
    /// </summary>
    public class ExhaustiveSearchEngine : IPackingEngine
    {
        /// <summary>
        /// Search steps (pattern candidates and search nodes) per stock length before the engine keeps
        /// the best plan found so far. Measured: under 0.2 s at p95 and about 0.5 s worst case.
        /// </summary>
        public const int DefaultSearchBudget = 1_000_000;

        private readonly IPackingEngine _firstFit = new FirstFitEngine();
        private readonly int _searchBudget;

        public ExhaustiveSearchEngine(int searchBudget = DefaultSearchBudget)
        {
            _searchBudget = searchBudget;
        }

        public PackResult Pack(PackingRequest request)
        {
            var firstFit = _firstFit.Pack(request);
            var parts = request.Items.Where(i => i.Length <= request.StockLength).ToList();
            if (parts.Count == 0)
                return firstFit;

            var oversized = request.Items.Where(i => i.Length > request.StockLength).ToList();
            var demand = CutDemand.From(parts, request.StockLength, request.Spacing);
            var budget = new SearchBudget(_searchBudget);
            bool firstFitPlacedAll = firstFit.ItemsNotUsed.Count == oversized.Count;

            if (demand.LowerBound(demand.Counts) <= request.MaxBinCount)
            {
                var patterns = new MinBarsSearch(demand, budget).Solve(
                    firstFitPlacedAll ? firstFit.Bins.Count : int.MaxValue, request.MaxBinCount);
                if (patterns != null)
                    return Build(demand, patterns, oversized);
                if (firstFitPlacedAll && !budget.Exhausted)
                    return firstFit; // proven: First Fit already uses the fewest bars
            }

            return WithFallback(firstFit);
        }

        private static PackResult Build(CutDemand demand, List<int[]> patterns, List<BinItem> oversized)
        {
            var (bins, notPlaced) = demand.Build(patterns);
            var result = new PackResult();
            result.AddBins(bins.OrderByDescending(b => b.Utilization).ThenBy(b => b.Items.Count));
            result.AddItemsNotUsed(oversized);
            result.AddItemsNotUsed(notPlaced);
            return result;
        }

        private static PackResult WithFallback(PackResult firstFit)
        {
            firstFit.FallbackEngine = BuiltInPackingEngines.FirstFit;
            return firstFit;
        }
    }
}
