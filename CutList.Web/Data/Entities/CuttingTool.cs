namespace CutList.Web.Data.Entities;

public class CuttingTool
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal KerfInches { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}
