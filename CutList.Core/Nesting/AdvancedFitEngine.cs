using CutList.Core.Nesting.Pipeline;

namespace CutList.Core.Nesting
{
    /// <summary>
    /// Advanced bin packing engine using First-Fit Decreasing with optimization.
    /// This is a stateless engine that uses a composable pipeline of steps.
    /// </summary>
    public class AdvancedFitEngine : IEngine
    {
        private readonly PackingPipeline _pipeline;

        public AdvancedFitEngine()
        {
            _pipeline = new PackingPipeline()
                .AddStep(new FilterOversizedItemsStep())
                .AddStep(new SortItemsDescendingStep())
                .AddStep(new FirstFitDecreasingStep())
                .AddStep(new OptimizationStep())
                .AddStep(new SortBinItemsStep())
                .AddStep(new DuplicateBinsStep())
                .AddStep(new SortBinsByUtilizationStep());
        }

        /// <summary>
        /// Packs items into bins using the FFD algorithm with optimization passes.
        /// </summary>
        public PackResult Pack(PackingRequest request)
        {
            return _pipeline.Execute(request);
        }
    }
}
