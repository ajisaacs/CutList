namespace CutList.Core.Nesting
{
    /// <summary>
    /// Factory interface for creating bin packing engines.
    /// Allows for dependency injection and testing without hard-coded engine types.
    /// </summary>
    public interface IEngineFactory
    {
        /// <summary>
        /// Creates an engine instance for the specified packing strategy.
        /// </summary>
        /// <param name="strategy">The packing strategy to use.</param>
        /// <returns>A configured IEngine instance.</returns>
        IEngine CreateEngine(PackingStrategy strategy = PackingStrategy.AdvancedFit);
    }
}
