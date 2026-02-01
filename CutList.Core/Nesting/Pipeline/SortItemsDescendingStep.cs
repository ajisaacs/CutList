namespace CutList.Core.Nesting.Pipeline
{
    /// <summary>
    /// Sorts remaining items by length in descending order.
    /// This is the "Decreasing" part of First-Fit Decreasing (FFD) algorithm.
    /// </summary>
    public class SortItemsDescendingStep : IPackingStep
    {
        public void Execute(PackingContext context)
        {
            var sorted = context.RemainingItems
                .OrderByDescending(item => item.Length)
                .ToList();

            context.RemainingItems.Clear();
            context.RemainingItems.AddRange(sorted);
        }
    }
}
