namespace CutList.Core.Nesting
{
    /// <summary>
    /// Immutable configuration object for a bin packing operation.
    /// Replaces mutable engine state with a clean request/response pattern.
    /// </summary>
    public sealed class PackingRequest
    {
        /// <summary>
        /// Creates a new packing request.
        /// </summary>
        /// <param name="items">The items to be packed.</param>
        /// <param name="stockLength">The length of stock bins.</param>
        /// <param name="spacing">The spacing/kerf between items (default 0).</param>
        /// <param name="maxBinCount">Maximum number of bins to create (default unlimited).</param>
        public PackingRequest(
            IReadOnlyList<BinItem> items,
            double stockLength,
            double spacing = 0,
            int maxBinCount = int.MaxValue)
        {
            if (stockLength <= 0)
                throw new ArgumentException("Stock length must be greater than 0", nameof(stockLength));

            Items = items ?? throw new ArgumentNullException(nameof(items));
            StockLength = stockLength;
            Spacing = spacing;
            MaxBinCount = maxBinCount;
        }

        /// <summary>
        /// The items to be packed into bins.
        /// </summary>
        public IReadOnlyList<BinItem> Items { get; }

        /// <summary>
        /// The length of each stock bin.
        /// </summary>
        public double StockLength { get; }

        /// <summary>
        /// The spacing/kerf between items (blade width).
        /// </summary>
        public double Spacing { get; }

        /// <summary>
        /// Maximum number of bins to create. Use int.MaxValue for unlimited.
        /// </summary>
        public int MaxBinCount { get; }
    }
}
