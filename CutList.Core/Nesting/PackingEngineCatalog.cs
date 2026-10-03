namespace CutList.Core.Nesting
{
    /// <summary>Id-based catalog of packing engines; see <see cref="IPackingEngineCatalog"/>.</summary>
    public sealed class PackingEngineCatalog : IPackingEngineCatalog
    {
        private readonly List<PackingEngineRegistration> _ordered;
        private readonly Dictionary<string, PackingEngineRegistration> _byId = new(StringComparer.OrdinalIgnoreCase);
        private readonly PackingEngineRegistration _default;

        /// <param name="registrations">Engines in display order; the first is the default unless overridden.</param>
        /// <param name="defaultEngineId">Optional default engine id (null/blank = first registration).</param>
        public PackingEngineCatalog(IEnumerable<PackingEngineRegistration> registrations, string? defaultEngineId = null)
        {
            _ordered = registrations?.ToList() ?? throw new ArgumentNullException(nameof(registrations));
            if (_ordered.Count == 0)
                throw new ArgumentException("At least one packing engine must be registered", nameof(registrations));

            foreach (var registration in _ordered)
            {
                if (!_byId.TryAdd(registration.Info.Id, registration))
                    throw new ArgumentException($"Duplicate packing engine id '{registration.Info.Id}'", nameof(registrations));
            }

            Engines = _ordered.Select(r => r.Info).ToList().AsReadOnly();
            _default = string.IsNullOrWhiteSpace(defaultEngineId) ? _ordered[0] : Lookup(defaultEngineId);
        }

        /// <summary>Catalog of <see cref="BuiltInPackingEngines.All"/>.</summary>
        public static PackingEngineCatalog CreateDefault(string? defaultEngineId = null) =>
            new(BuiltInPackingEngines.All, defaultEngineId);

        public IReadOnlyList<PackingEngineInfo> Engines { get; }

        public PackingEngineInfo Default => _default.Info;

        public PackingEngineInfo Resolve(string? engineId) => Find(engineId).Info;

        public IPackingEngine Create(string? engineId) => Find(engineId).Create();

        private PackingEngineRegistration Find(string? engineId) =>
            string.IsNullOrWhiteSpace(engineId) ? _default : Lookup(engineId);

        private PackingEngineRegistration Lookup(string engineId)
        {
            var id = engineId.Trim();
            return _byId.TryGetValue(id, out var registration)
                ? registration
                : throw new UnknownPackingEngineException(id, _ordered.Select(r => r.Info.Id));
        }
    }
}
