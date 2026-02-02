namespace CutList.Web.Data.Entities;

public class ProjectPart
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal LengthInches { get; set; }
    public int Quantity { get; set; } = 1;
    public int SortOrder { get; set; }

    public Project Project { get; set; } = null!;
}
