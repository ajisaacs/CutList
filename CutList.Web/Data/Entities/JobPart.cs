namespace CutList.Web.Data.Entities;

public class JobPart
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public int MaterialId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal LengthInches { get; set; }
    public int Quantity { get; set; } = 1;
    public int SortOrder { get; set; }

    public Job Job { get; set; } = null!;
    public Material Material { get; set; } = null!;
}
