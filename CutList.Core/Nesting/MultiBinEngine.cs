namespace CutList.Core.Nesting
{
    /// <summary>
    /// Engine that coordinates packing across multiple bin types with different sizes.
    /// Uses priority ordering to determine which bin types to fill first.
    /// </summary>
    public class MultiBinEngine
    {
        private readonly IEngineFactory _engineFactory;
        private readonly List<MultiBin> _bins;

        public MultiBinEngine() : this(new EngineFactory())
        {
        }

        public MultiBinEngine(IEngineFactory engineFactory)
        {
            _engineFactory = engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));
            _bins = new List<MultiBin>();
        }

        /// <summary>
        /// Gets the read-only collection of bin types.
        /// Use SetBins() to configure bins for packing.
        /// </summary>
        public IReadOnlyList<MultiBin> Bins => _bins.AsReadOnly();

        /// <summary>
        /// Sets the bin types to use for packing.
        /// </summary>
        public void SetBins(IEnumerable<MultiBin> bins)
        {
            _bins.Clear();
            if (bins != null)
            {
                _bins.AddRange(bins);
            }
        }

        /// <summary>
        /// The spacing/kerf between items.
        /// </summary>
        public double Spacing { get; set; }

        /// <summary>
        /// The packing strategy to use for each bin type.
        /// </summary>
        public PackingStrategy Strategy { get; set; } = PackingStrategy.AdvancedFit;

        /// <summary>
        /// Packs items across all configured bin types.
        /// </summary>
        public PackResult Pack(List<BinItem> items)
        {
            var sortedBinTypes = _bins
                .Where(b => b.Length > 0)
                .OrderBy(b => b.Priority)
                .ThenBy(b => b.Length)
                .ToList();

            var result = new PackResult();
            var remainingItems = new List<BinItem>(items);

            var engine = _engineFactory.CreateEngine(Strategy);

            foreach (var binType in sortedBinTypes)
            {
                if (remainingItems.Count == 0)
                    break;

                var request = new PackingRequest(
                    items: remainingItems,
                    stockLength: binType.Length,
                    spacing: Spacing,
                    maxBinCount: binType.Quantity
                );

                var packResult = engine.Pack(request);

                result.AddBins(packResult.Bins);
                remainingItems = packResult.ItemsNotUsed.ToList();
            }

            result.AddItemsNotUsed(remainingItems);

            return result;
        }
    }
}
