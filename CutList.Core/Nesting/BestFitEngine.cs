namespace CutList.Core.Nesting
{
    /// <summary>
    /// Best-Fit Decreasing bin packing engine.
    /// Places each item in the bin with the least remaining space that can still fit it.
    /// This is a stateless engine - all state is passed via PackingRequest.
    /// </summary>
    public class BestFitEngine : IEngine
    {
        /// <summary>
        /// Packs items into bins using the Best-Fit Decreasing algorithm.
        /// </summary>
        public PackResult Pack(PackingRequest request)
        {
            var result = new PackResult();
            var items = request.Items.OrderByDescending(i => i.Length).ToList();
            var bins = new List<Bin>();

            // Filter oversized items
            var oversizedItems = items.Where(i => i.Length > request.StockLength).ToList();
            foreach (var item in oversizedItems)
            {
                items.Remove(item);
                result.AddItemNotUsed(item);
            }

            // Pack remaining items using best-fit
            foreach (var item in items)
            {
                if (!TryFindBestBin(bins, item.Length, out var bestBin))
                {
                    if (bins.Count < request.MaxBinCount)
                    {
                        bestBin = CreateBin(request);
                        bins.Add(bestBin);
                    }
                }

                if (bestBin != null)
                {
                    bestBin.AddItem(item);
                }
                else
                {
                    result.AddItemNotUsed(item);
                }
            }

            // Sort bins by utilization
            var sortedBins = bins
                .OrderByDescending(b => b.Utilization)
                .ThenBy(b => b.Items.Count);

            result.AddBins(sortedBins);

            return result;
        }

        private static Bin CreateBin(PackingRequest request)
        {
            return new Bin(request.StockLength)
            {
                Spacing = request.Spacing
            };
        }

        private static bool TryFindBestBin(IEnumerable<Bin> bins, double length, out Bin? found)
        {
            found = null;

            foreach (var bin in bins)
            {
                if (bin.RemainingLength < length)
                    continue;

                if (found == null || bin.RemainingLength < found.RemainingLength)
                {
                    found = bin;
                }
            }

            return found != null;
        }
    }
}
