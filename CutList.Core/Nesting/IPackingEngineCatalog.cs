namespace CutList.Core.Nesting
{
    /// <summary>
    /// Resolves packing engine ids to engines. Callers select engines by id and never reference
    /// concrete engine classes.
    /// </summary>
    public interface IPackingEngineCatalog
    {
        /// <summary>Registered engines in display order.</summary>
        IReadOnlyList<PackingEngineInfo> Engines { get; }

        /// <summary>Engine used when no id is given.</summary>
        PackingEngineInfo Default { get; }

        /// <summary>
        /// Case-insensitive lookup; null/blank returns <see cref="Default"/>.
        /// </summary>
        /// <exception cref="UnknownPackingEngineException">The id is not registered.</exception>
        PackingEngineInfo Resolve(string? engineId);

        /// <summary>Creates a fresh engine instance for the id (null/blank = default).</summary>
        /// <exception cref="UnknownPackingEngineException">The id is not registered.</exception>
        IEngine Create(string? engineId);
    }
}
