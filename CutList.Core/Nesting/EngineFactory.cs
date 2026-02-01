namespace CutList.Core.Nesting
{
    /// <summary>
    /// Default implementation of IEngineFactory that creates packing engines
    /// based on the specified strategy.
    /// </summary>
    public class EngineFactory : IEngineFactory
    {
        public IEngine CreateEngine(PackingStrategy strategy = PackingStrategy.AdvancedFit)
        {
            return strategy switch
            {
                PackingStrategy.AdvancedFit => new AdvancedFitEngine(),
                PackingStrategy.BestFit => new BestFitEngine(),
                PackingStrategy.Exhaustive => new ExhaustiveFitEngine(),
                _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown packing strategy")
            };
        }
    }
}
