using CutList.Core.Nesting.Search;

namespace CutList.Core.Nesting
{
    /// <summary>
    /// Exhaustive engine: groups parts by length and searches cut patterns for the fewest bars,
    /// starting from the First Fit plan. When limited stock cannot hold every part it fills those bars
    /// as fully as possible, unless First Fit's leftovers would need fewer bars of that length. Returns
    /// First Fit's plan, reported as a fallback, when the search budget runs out before it does better.
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

            if (!firstFitPlacedAll && !budget.Exhausted)
                return FillLimitedStock(request, demand, budget, firstFit, oversized);

            return WithFallback(firstFit);
        }

        private PackResult FillLimitedStock(
            PackingRequest request, CutDemand demand, SearchBudget budget, PackResult firstFit, List<BinItem> oversized)
        {
            double firstFitLength = firstFit.Bins.Sum(b => b.Items.Sum(i => i.Length));
            var search = new MaxFillSearch(demand, budget);
            var patterns = search.Solve(request.MaxBinCount, firstFitLength);
            if (patterns == null)
                return search.Completed ? firstFit : WithFallback(firstFit);

            // Fuller bars can strand awkward parts: keep First Fit's plan when its leftovers would need
            // fewer bars of this length.
            var filled = Build(demand, patterns, oversized);
            return LeftoverBars(filled, request) > LeftoverBars(firstFit, request) ? firstFit : filled;
        }

        private int LeftoverBars(PackResult result, PackingRequest request)
        {
            var leftovers = result.ItemsNotUsed.Where(i => i.Length <= request.StockLength).ToList();
            return leftovers.Count == 0
                ? 0
                : _firstFit.Pack(new PackingRequest(leftovers, request.StockLength, request.Spacing)).Bins.Count;
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
