namespace CutList.Core.Nesting
{
    /// <summary>
    /// Specifies the bin packing strategy/algorithm to use.
    /// </summary>
    public enum PackingStrategy
    {
        /// <summary>
        /// First-Fit Decreasing with optimization passes.
        /// Sorts items by length descending, then packs using first-fit.
        /// Includes optimization to improve packing by swapping items.
        /// Best for general-purpose use cases.
        /// </summary>
        AdvancedFit,

        /// <summary>
        /// Best-Fit Decreasing algorithm.
        /// Sorts items by length descending, then places each item in the bin
        /// with the least remaining space that can still fit it.
        /// Simpler but can be effective for certain distributions.
        /// </summary>
        BestFit,

        /// <summary>
        /// Exhaustive search that tries all possible combinations.
        /// Guarantees optimal solution but has exponential time complexity.
        /// Automatically falls back to AdvancedFit for more than 15 items.
        /// </summary>
        Exhaustive
    }
}
