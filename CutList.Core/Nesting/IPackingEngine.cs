namespace CutList.Core.Nesting
{
    /// <summary>
    /// A selectable packing engine: packs items into bins of one stock length.
    /// Engines are stateless - all configuration is passed via PackingRequest.
    /// </summary>
    public interface IPackingEngine
    {
        /// <summary>
        /// Packs items into bins according to the request configuration.
        /// </summary>
        /// <param name="request">The packing configuration and items.</param>
        /// <returns>The packing result with bins and unused items.</returns>
        PackResult Pack(PackingRequest request);
    }
}
