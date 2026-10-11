using System.ComponentModel;

namespace CutList.Mcp;

/// <summary>Explicit operator-reviewed cuts; never inferred from files or material labels.</summary>
public class PartSetMemberInput
{
    [Description("Explicit active catalog MaterialId chosen and reviewed by the operator; never inferred.")]
    public int MaterialId { get; set; }
    public string Name { get; set; } = string.Empty;
    [Description("Exact decimal inches, positive, at most four decimal places; no unit strings or rounding.")]
    public decimal LengthInches { get; set; }
    [Description("Positive integer quantity per assembly.")]
    public int Quantity { get; set; }
    public string? SourceItem { get; set; }
    public string? SourceFile { get; set; }
    public int SortOrder { get; set; }
}

public class ApiPartSetDto
{
    public int Id { get; set; }
    public string DrawingNumber { get; set; } = string.Empty;
    public string? Revision { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
}

public class ApiPartSetDetailDto : ApiPartSetDto
{
    public List<ApiPartSetMemberDto> Members { get; set; } = new();
}

public class ApiPartSetMemberDto : PartSetMemberInput
{
    public int Id { get; set; }
    public string MaterialName { get; set; } = string.Empty;
}

public class ApiJobPartSetInstanceDto
{
    public int Id { get; set; }
    public int PartSetId { get; set; }
    public string DrawingNumber { get; set; } = string.Empty;
    public string? Revision { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public List<ApiJobPartDto> Parts { get; set; } = new();
}

public class PartSetResult<T> : SimpleResult
{
    public T? Data { get; set; }
}
