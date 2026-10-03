namespace CutList.Core.Nesting.Pipeline
{
    /// <summary>
    /// Implements the First-Fit Decreasing (FFD) bin packing algorithm.
    /// Assumes items are already sorted by length descending.
    /// Places each item in the first bin that has enough space.
    /// </summary>
    public class FirstFitDecreasingStep : IPackingStep
    {
        public void Execute(PackingContext context)
        {
            while (context.RemainingItems.Count > 0 && context.CanAddMoreBins())
            {
                var bin = context.CreateBin();
                FillBin(bin, context.RemainingItems);
                context.Bins.Add(bin);
            }
        }

        private static void FillBin(Bin bin, List<BinItem> remainingItems)
        {
            // The bar starts empty; track its use in CutFit units so the fit check is exact.
            long capacity = CutFit.Capacity(bin.Length, bin.Spacing);
            long used = 0;
            for (int i = 0; i < remainingItems.Count; i++)
            {
                var item = remainingItems[i];
                long size = CutFit.Size(item.Length, bin.Spacing);
                if (used + size <= capacity)
                {
                    bin.AddItem(item);
                    used += size;
                    remainingItems.RemoveAt(i);
                    i--;
                }
            }
        }
    }
}
