namespace CutList.Web.Data.Entities;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Customer { get; set; }
    public int? CuttingToolId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public CuttingTool? CuttingTool { get; set; }
    public ICollection<ProjectPart> Parts { get; set; } = new List<ProjectPart>();
}
