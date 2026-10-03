using CutList.Core.Nesting.Pipeline;

namespace CutList.Core.Nesting
{
    /// <summary>
    /// First-Fit Decreasing bin packing engine: longest items first, each into the first bin it fits.
    /// When the bin quantity runs out with items left over, a swap pass trades packed items for
    /// leftover items to fill those limited bins tighter.
    /// This is a stateless engine that uses a composable pipeline of steps.
    /// </summary>
    public class FirstFitEngine : IEngine
    {
        private readonly PackingPipeline _pipeline;

        public FirstFitEngine()
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
        /// Packs items into bins using first-fit decreasing, then the leftover swap pass.
        /// </summary>
        public PackResult Pack(PackingRequest request)
        {
            return _pipeline.Execute(request);
        }
    }
}
