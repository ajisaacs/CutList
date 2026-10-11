namespace CutList.Web.Data.Entities;

public class PartSetMember
{
    public int Id { get; set; }
    public int PartSetId { get; set; }
    public string? SourceItem { get; set; }
    public string? SourceFile { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MaterialId { get; set; }
    public decimal LengthInches { get; set; }
    public int Quantity { get; set; } = 1;
    public int SortOrder { get; set; }
    public PartSet PartSet { get; set; } = null!;
    public Material Material { get; set; } = null!;
}
