namespace CutList.Core.Nesting
{
    /// <summary>Pairs an engine's public description with a factory for fresh instances.</summary>
    public sealed class PackingEngineRegistration
    {
        private readonly Func<IPackingEngine> _create;

        public PackingEngineRegistration(PackingEngineInfo info, Func<IPackingEngine> create)
        {
            Info = info ?? throw new ArgumentNullException(nameof(info));
            if (string.IsNullOrWhiteSpace(info.Id) || info.Id != info.Id.Trim())
                throw new ArgumentException("Packing engine id is required and must not have surrounding whitespace", nameof(info));
            _create = create ?? throw new ArgumentNullException(nameof(create));
        }

        public PackingEngineInfo Info { get; }

        public IPackingEngine Create() => _create();
    }
}
