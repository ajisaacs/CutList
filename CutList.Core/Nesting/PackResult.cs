namespace CutList.Core.Nesting
{
    /// <summary>
    /// Represents the result of a bin packing operation.
    /// Contains the packed bins and any items that could not be placed.
    /// </summary>
    public class PackResult
    {
        private readonly List<BinItem> _itemsNotUsed;
        private readonly List<Bin> _bins;

        public PackResult()
        {
            _itemsNotUsed = new List<BinItem>();
            _bins = new List<Bin>();
        }

        public PackResult(IEnumerable<Bin> bins, IEnumerable<BinItem> itemsNotUsed)
        {
            _bins = bins?.ToList() ?? new List<Bin>();
            _itemsNotUsed = itemsNotUsed?.ToList() ?? new List<BinItem>();
        }

        /// <summary>
        /// Items that could not be placed in any bin (e.g., too large for stock).
        /// </summary>
        public IReadOnlyList<BinItem> ItemsNotUsed => _itemsNotUsed;

        /// <summary>
        /// The bins containing packed items.
        /// </summary>
        public IReadOnlyList<Bin> Bins => _bins;

        /// <summary>
        /// Engine that actually packed this result when the selected engine handed it off (for a
        /// <see cref="MultiBinPacker"/> result: for at least one stock length); null otherwise.
        /// </summary>
        public PackingEngineInfo? FallbackEngine { get; set; }

        public void AddItemNotUsed(BinItem item)
        {
            _itemsNotUsed.Add(item);
        }

        public void AddItemsNotUsed(IEnumerable<BinItem> items)
        {
            _itemsNotUsed.AddRange(items);
        }

        public void AddBin(Bin bin)
        {
            _bins.Add(bin);
        }

        public void AddBins(IEnumerable<Bin> bins)
        {
            _bins.AddRange(bins);
        }
    }
}
