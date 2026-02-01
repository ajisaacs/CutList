namespace CutList.Core.Nesting.Pipeline
{
    /// <summary>
    /// Sorts items within each bin by length descending for consistent output.
    /// </summary>
    public class SortBinItemsStep : IPackingStep
    {
        public void Execute(PackingContext context)
        {
            foreach (var bin in context.Bins)
            {
                bin.SortItems((a, b) =>
                {
                    int comparison = b.Length.CompareTo(a.Length);
                    return comparison != 0 ? comparison : a.Name.CompareTo(b.Name);
                });
            }
        }
    }
}
