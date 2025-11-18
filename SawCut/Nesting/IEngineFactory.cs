namespace SawCut.Nesting
{
    /// <summary>
    /// Factory interface for creating bin packing engines.
    /// Allows for dependency injection and testing without hard-coded engine types.
    /// </summary>
    public interface IEngineFactory
    {
        /// <summary>
        /// Creates a configured engine instance for bin packing.
        /// </summary>
        /// <param name="stockLength">The length of stock bins</param>
        /// <param name="spacing">The spacing/kerf between items</param>
        /// <param name="maxBinCount">Maximum number of bins to create</param>
        /// <returns>A configured IEngine instance</returns>
        IEngine CreateEngine(double stockLength, double spacing, int maxBinCount);
    }
}
