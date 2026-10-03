namespace CutList.Core.Nesting
{
    /// <summary>
    /// The single list of selectable packing engines. To add an engine, implement IEngine and add a
    /// registration here; EngineContractTests then checks it, and the web picker, REST API and MCP
    /// tools offer it automatically.
    /// </summary>
    public static class BuiltInPackingEngines
    {
        public static PackingEngineInfo FirstFit { get; } = new(
            "firstfit",
            "First Fit",
            "First-fit decreasing: longest parts first, each into the first bar it fits. When bar quantity is limited, swaps parts to fill those bars tighter.");

        public static PackingEngineInfo BestFit { get; } = new(
            "bestfit",
            "Best Fit",
            "Best-fit decreasing: each part goes in the bar with the least room left that still fits it.");

        public static PackingEngineInfo Exhaustive { get; } = new(
            "exhaustive",
            "Exhaustive",
            $"Searches arrangements for the fewest bars, then least waste. Uses First Fit for a stock length with more than {ExhaustiveFitEngine.DefaultMaxItems} parts, when limited stock cannot hold every part, or when its search budget runs out first.");

        /// <summary>Selectable engines in display order; the first is the default.</summary>
        public static IReadOnlyList<PackingEngineRegistration> All { get; } = new[]
        {
            new PackingEngineRegistration(FirstFit, () => new FirstFitEngine()),
            new PackingEngineRegistration(BestFit, () => new BestFitEngine()),
            new PackingEngineRegistration(Exhaustive, () => new ExhaustiveFitEngine()),
        };
    }
}
