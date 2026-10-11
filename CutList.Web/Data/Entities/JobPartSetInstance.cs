namespace CutList.Web.Data.Entities;

/// <summary>Immutable provenance captured when a library set is expanded into a job.</summary>
public class JobPartSetInstance
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public int PartSetId { get; set; }
    public string DrawingNumber { get; set; } = string.Empty;
    public string? Revision { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public Job Job { get; set; } = null!;
    public PartSet PartSet { get; set; } = null!;
    public ICollection<JobPart> Parts { get; set; } = new List<JobPart>();
}
