using CutList.Web.Data.Entities;

namespace CutList.Web.Components.Shared;

public class MaterialFilterState
{
    public MaterialShape? Shape { get; set; }
    public MaterialType? Type { get; set; }
    public string? Grade { get; set; }
    public string? SearchText { get; set; }
}
