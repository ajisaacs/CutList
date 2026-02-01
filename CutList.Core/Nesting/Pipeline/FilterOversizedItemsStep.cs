namespace CutList.Core.Nesting.Pipeline
{
    /// <summary>
    /// Removes items that are too large to fit in the stock length.
    /// These items are moved to the OversizedItems collection.
    /// </summary>
    public class FilterOversizedItemsStep : IPackingStep
    {
        public void Execute(PackingContext context)
        {
            var oversized = context.RemainingItems
                .Where(item => item.Length > context.StockLength)
                .ToList();

            foreach (var item in oversized)
            {
                context.RemainingItems.Remove(item);
                context.OversizedItems.Add(item);
            }
        }
    }
}
