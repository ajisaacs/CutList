namespace CutList.Core.Nesting.Pipeline
{
    /// <summary>
    /// Executes a sequence of packing steps to produce a final result.
    /// Provides a composable, testable approach to bin packing algorithms.
    /// </summary>
    public class PackingPipeline
    {
        private readonly List<IPackingStep> _steps = new();

        /// <summary>
        /// Adds a step to the pipeline.
        /// </summary>
        /// <param name="step">The step to add.</param>
        /// <returns>This pipeline for fluent chaining.</returns>
        public PackingPipeline AddStep(IPackingStep step)
        {
            _steps.Add(step ?? throw new ArgumentNullException(nameof(step)));
            return this;
        }

        /// <summary>
        /// Executes all steps in sequence and returns the result.
        /// </summary>
        /// <param name="request">The packing request to process.</param>
        /// <returns>The packing result.</returns>
        public PackResult Execute(PackingRequest request)
        {
            var context = new PackingContext(request);

            foreach (var step in _steps)
            {
                step.Execute(context);
            }

            return context.ToResult();
        }
    }
}
