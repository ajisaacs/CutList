using System.ComponentModel.DataAnnotations;

namespace CutList.Web.DTOs;

/// <summary>Full replacement of editable library fields; identities and archive state are server-owned.</summary>
public class SavePartSetDto
{
    [Required, StringLength(100)] public string DrawingNumber { get; set; } = string.Empty;
    [StringLength(50)] public string? Revision { get; set; }
    [Required, StringLength(100)] public string DisplayName { get; set; } = string.Empty;
    [Required] public List<SavePartSetMemberDto> Members { get; set; } = new();
}

public class SavePartSetMemberDto
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [StringLength(100)] public string? SourceItem { get; set; }
    [StringLength(255)] public string? SourceFile { get; set; }
    public int MaterialId { get; set; }
    public decimal LengthInches { get; set; }
    public int Quantity { get; set; }
    public int SortOrder { get; set; }
}

public class PartSetDto
{
    public int Id { get; set; }
    public string DrawingNumber { get; set; } = string.Empty;
    public string? Revision { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
}

public class PartSetDetailDto : PartSetDto
{
    public List<PartSetMemberDto> Members { get; set; } = new();
}

public class PartSetMemberDto : SavePartSetMemberDto
{
    public int Id { get; set; }
    public string MaterialName { get; set; } = string.Empty;
}

public class AddPartSetInstanceDto
{
    [Range(1, int.MaxValue)] public int PartSetId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
}

public class JobPartSetInstanceDto
{
    public int Id { get; set; }
    public int PartSetId { get; set; }
    public string DrawingNumber { get; set; } = string.Empty;
    public string? Revision { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public List<JobPartDto> Parts { get; set; } = new();
}
