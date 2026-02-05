namespace CutList.Web.DTOs;

public class MaterialDto
{
    public int Id { get; set; }
    public string Shape { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Grade { get; set; }
    public string Size { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public MaterialDimensionsDto? Dimensions { get; set; }
}

public class CreateMaterialDto
{
    public string Shape { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Grade { get; set; }
    public string? Size { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, decimal>? Dimensions { get; set; }
}

public class UpdateMaterialDto
{
    public string? Type { get; set; }
    public string? Grade { get; set; }
    public string? Size { get; set; }
    public string? Description { get; set; }
    public bool? RegenerateSize { get; set; }
    public Dictionary<string, decimal>? Dimensions { get; set; }
}

public class BulkCreateResult
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class MaterialDimensionsDto
{
    public string DimensionType { get; set; } = string.Empty;
    public Dictionary<string, decimal> Values { get; set; } = new();
}

public class MaterialSearchDto
{
    public string Shape { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public decimal Tolerance { get; set; } = 0.1m;
}
