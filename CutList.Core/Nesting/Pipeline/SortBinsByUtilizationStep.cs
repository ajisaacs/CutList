namespace CutList.Core.Nesting.Pipeline
{
    /// <summary>
    /// Sorts bins by utilization (highest first) for optimal presentation.
    /// Secondary sort by item count (fewer items first for ties).
    /// </summary>
    public class SortBinsByUtilizationStep : IPackingStep
    {
        public void Execute(PackingContext context)
        {
            var sorted = context.Bins
                .OrderByDescending(b => b.Utilization)
                .ThenBy(b => b.Items.Count)
                .ToList();

            context.Bins.Clear();
            context.Bins.AddRange(sorted);
        }
    }
}
