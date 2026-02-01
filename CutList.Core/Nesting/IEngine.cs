namespace CutList.Core.Nesting
{
    /// <summary>
    /// Interface for bin packing engines.
    /// Engines are stateless - all configuration is passed via PackingRequest.
    /// </summary>
    public interface IEngine
    {
        /// <summary>
        /// Packs items into bins according to the request configuration.
        /// </summary>
        /// <param name="request">The packing configuration and items.</param>
        /// <returns>The packing result with bins and unused items.</returns>
        PackResult Pack(PackingRequest request);
    }
}
