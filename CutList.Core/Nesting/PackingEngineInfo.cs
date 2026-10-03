namespace CutList.Core.Nesting
{
    /// <summary>Public description of a selectable packing engine.</summary>
    public sealed record PackingEngineInfo(string Id, string DisplayName, string Description)
    {
        /// <summary>Name to report for a run, noting the fallback engine when one packed any of it.</summary>
        public string RunName(PackingEngineInfo? fallback) =>
            fallback == null ? DisplayName : $"{DisplayName} ({fallback.DisplayName} fallback)";
    }
}
