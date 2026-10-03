namespace CutList.Core.Nesting
{
    /// <summary>
    /// Packs items across multiple stock types with different lengths, in priority order (then
    /// shortest first), running one single-length engine for every stock type.
    /// </summary>
    public class MultiBinPacker
    {
        private readonly IPackingEngine _engine;
        private readonly List<MultiBin> _bins = new();

        /// <param name="engine">Single-length engine, normally from <see cref="IPackingEngineCatalog.Create"/>.</param>
        public MultiBinPacker(IPackingEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
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

                var packResult = _engine.Pack(request);

                result.AddBins(packResult.Bins);
                result.FallbackEngine ??= packResult.FallbackEngine;
                remainingItems = packResult.ItemsNotUsed.ToList();
            }

            result.AddItemsNotUsed(remainingItems);

            return result;
        }
    }
}
