namespace CutList.Web.Data.Entities;

public class ProjectStockBin
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public decimal LengthInches { get; set; }
    public int Quantity { get; set; } = -1;
    public int Priority { get; set; } = 25;
    public int SortOrder { get; set; }

    public Project Project { get; set; } = null!;
}
