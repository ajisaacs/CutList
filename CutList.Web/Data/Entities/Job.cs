namespace CutList.Web.Data.Entities;

public class Job
{
    public int Id { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Customer { get; set; }
    public int? CuttingToolId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public CuttingTool? CuttingTool { get; set; }
    public ICollection<JobPart> Parts { get; set; } = new List<JobPart>();
    public ICollection<JobStock> Stock { get; set; } = new List<JobStock>();

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? JobNumber : $"{JobNumber} - {Name}";
}
