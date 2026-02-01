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
            for (int i = 0; i < remainingItems.Count; i++)
            {
                var item = remainingItems[i];
                if (bin.RemainingLength >= item.Length)
                {
                    bin.AddItem(item);
                    remainingItems.RemoveAt(i);
                    i--;
                }
            }
        }
    }
}
