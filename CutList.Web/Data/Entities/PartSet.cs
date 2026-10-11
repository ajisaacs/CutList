namespace CutList.Web.Data.Entities;

public class PartSet
{
    public int Id { get; set; }
    public string DrawingNumber { get; set; } = string.Empty;
    public string? Revision { get; set; }
    public string DrawingKey { get; set; } = string.Empty;
    public string RevisionKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public ICollection<PartSetMember> Members { get; set; } = new List<PartSetMember>();
}
