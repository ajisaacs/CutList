using CutList.Core.Nesting.Pipeline;

namespace CutList.Core.Nesting
{
    /// <summary>
    /// First-Fit Decreasing bin packing engine: longest items first, each into the first bin it fits.
    /// Improves each bin with better-fitting combinations of remaining parts before opening the next
    /// bin. A final swap pass still tries to fill limited bins tighter when parts are left over.
    /// Retains the original plan if local improvement worsens stock use or unplaced demand.
    /// This is a stateless engine that uses a composable pipeline of steps.
    /// </summary>
    public class FirstFitEngine : IPackingEngine
    {
        private readonly PackingPipeline _pipeline;
        private readonly PackingPipeline _baselinePipeline;

        public FirstFitEngine()
        {
            _pipeline = CreatePipeline(improveEachBin: true);
            _baselinePipeline = CreatePipeline(improveEachBin: false);
        }

        private static PackingPipeline CreatePipeline(bool improveEachBin) =>
            new PackingPipeline()
                .AddStep(new FilterOversizedItemsStep())
                .AddStep(new SortItemsDescendingStep())
                .AddStep(new FirstFitDecreasingStep(improveEachBin))
                .AddStep(new SwapInLeftoversStep())
                .AddStep(new SortBinItemsStep())
                .AddStep(new SortBinsByUtilizationStep());

        /// <summary>
        /// Packs and improves each bin, then runs the final leftover swap pass.
        /// </summary>
        public PackResult Pack(PackingRequest request)
        {
            var baseline = _baselinePipeline.Execute(request);
            var improved = _pipeline.Execute(request);

            // A tighter local combination can strand parts and worsen the whole job. Keep the
            // original plan if the candidate uses more stock or leaves more demand unplaced.
            if (improved.Bins.Count > baseline.Bins.Count ||
                improved.ItemsNotUsed.Count > baseline.ItemsNotUsed.Count ||
                improved.ItemsNotUsed.Sum(i => CutFit.Units(i.Length)) >
                    baseline.ItemsNotUsed.Sum(i => CutFit.Units(i.Length)))
                return baseline;

            return improved;
        }
    }
}
