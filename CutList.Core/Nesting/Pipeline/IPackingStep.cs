namespace CutList.Core.Nesting.Pipeline
{
    /// <summary>
    /// Represents a single step in the packing pipeline.
    /// Each step modifies the PackingContext to progress toward a final result.
    /// </summary>
    public interface IPackingStep
    {
        /// <summary>
        /// Executes this step, modifying the context as needed.
        /// </summary>
        /// <param name="context">The mutable packing context.</param>
        void Execute(PackingContext context);
    }
}
