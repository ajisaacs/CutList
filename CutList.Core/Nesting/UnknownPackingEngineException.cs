namespace CutList.Core.Nesting
{
    /// <summary>Thrown when a packing engine id is not registered. The message lists the valid ids.</summary>
    public sealed class UnknownPackingEngineException : ArgumentException
    {
        public UnknownPackingEngineException(string engineId, IEnumerable<string> knownEngineIds)
            : this(engineId, knownEngineIds.ToList())
        {
        }

        private UnknownPackingEngineException(string engineId, List<string> knownEngineIds)
            : base($"Unknown packing engine '{engineId}'. Available engines: {string.Join(", ", knownEngineIds)}.")
        {
            EngineId = engineId;
            KnownEngineIds = knownEngineIds.AsReadOnly();
        }

        public string EngineId { get; }

        public IReadOnlyList<string> KnownEngineIds { get; }
    }
}
