namespace SawCut.Nesting
{
    /// <summary>
    /// Default implementation of IEngineFactory that creates AdvancedFitEngine instances.
    /// Can be extended to support different engine types based on configuration.
    /// </summary>
    public class EngineFactory : IEngineFactory
    {
        public IEngine CreateEngine(double stockLength, double spacing, int maxBinCount)
        {
            return new AdvancedFitEngine
            {
                StockLength = stockLength,
                Spacing = spacing,
                MaxBinCount = maxBinCount
            };
        }
    }
}
