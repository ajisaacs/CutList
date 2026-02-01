namespace CutList.Core.Nesting.Pipeline
{
    /// <summary>
    /// Creates duplicate bins when the same packing pattern can be repeated.
    /// If there are enough remaining items to fill another bin identically,
    /// creates copies to reduce iteration overhead.
    /// </summary>
    public class DuplicateBinsStep : IPackingStep
    {
        public void Execute(PackingContext context)
        {
            // Process bins that were created before this step
            // We need a copy since we'll be adding new bins
            var originalBins = context.Bins.ToList();

            foreach (var originalBin in originalBins)
            {
                CreateDuplicateBins(originalBin, context);
            }
        }

        private static void CreateDuplicateBins(Bin originalBin, PackingContext context)
        {
            int duplicateCount = GetDuplicateCount(originalBin, context.RemainingItems);

            for (int i = 0; i < duplicateCount; i++)
            {
                if (!context.CanAddMoreBins())
                    break;

                var newBin = context.CreateBin();

                foreach (var item in originalBin.Items)
                {
                    var matchingItem = context.RemainingItems.FirstOrDefault(a => a.Length == item.Length);
                    if (matchingItem != null)
                    {
                        newBin.AddItem(matchingItem);
                        context.RemainingItems.Remove(matchingItem);
                    }
                }

                context.Bins.Add(newBin);
            }
        }

        private static int GetDuplicateCount(Bin bin, List<BinItem> remainingItems)
        {
            int count = int.MaxValue;

            foreach (var lengthGroup in bin.Items.GroupBy(i => i.Length))
            {
                int availableCount = remainingItems.Count(i => i.Length == lengthGroup.Key);
                count = Math.Min(count, availableCount / lengthGroup.Count());
            }

            return count == int.MaxValue ? 0 : count;
        }
    }
}
