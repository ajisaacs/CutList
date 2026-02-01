namespace CutList.Core.Nesting.Pipeline
{
    /// <summary>
    /// Mutable context passed through the packing pipeline.
    /// Contains the working state that pipeline steps modify.
    /// </summary>
    public class PackingContext
    {
        /// <summary>
        /// Creates a new packing context from a request.
        /// </summary>
        public PackingContext(PackingRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            RemainingItems = new List<BinItem>(request.Items);
            Bins = new List<Bin>();
            OversizedItems = new List<BinItem>();
        }

        /// <summary>
        /// The original immutable request.
        /// </summary>
        public PackingRequest Request { get; }

        /// <summary>
        /// Items that have not yet been placed in a bin.
        /// Pipeline steps remove items from this list as they are packed.
        /// </summary>
        public List<BinItem> RemainingItems { get; }

        /// <summary>
        /// Items that are too large to fit in any stock bin.
        /// </summary>
        public List<BinItem> OversizedItems { get; }

        /// <summary>
        /// The bins that have been created and filled.
        /// </summary>
        public List<Bin> Bins { get; }

        /// <summary>
        /// Convenience property for stock length from the request.
        /// </summary>
        public double StockLength => Request.StockLength;

        /// <summary>
        /// Convenience property for spacing from the request.
        /// </summary>
        public double Spacing => Request.Spacing;

        /// <summary>
        /// Convenience property for max bin count from the request.
        /// </summary>
        public int MaxBinCount => Request.MaxBinCount;

        /// <summary>
        /// Checks if more bins can be added without exceeding the limit.
        /// </summary>
        public bool CanAddMoreBins()
        {
            if (MaxBinCount == -1)
                return true;

            return Bins.Count < MaxBinCount;
        }

        /// <summary>
        /// Creates a new bin with the stock length and spacing from the request.
        /// </summary>
        public Bin CreateBin()
        {
            return new Bin(StockLength)
            {
                Spacing = Spacing
            };
        }

        /// <summary>
        /// Builds the final PackResult from the current context state.
        /// </summary>
        public PackResult ToResult()
        {
            var allUnusedItems = new List<BinItem>();
            allUnusedItems.AddRange(OversizedItems);
            allUnusedItems.AddRange(RemainingItems);
            return new PackResult(Bins, allUnusedItems);
        }
    }
}
