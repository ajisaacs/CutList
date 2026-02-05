namespace CutList.Web.DTOs;

public class CuttingToolDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal KerfInches { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCuttingToolDto
{
    public string Name { get; set; } = string.Empty;
    public decimal KerfInches { get; set; }
    public bool IsDefault { get; set; }
}

public class UpdateCuttingToolDto
{
    public string? Name { get; set; }
    public decimal? KerfInches { get; set; }
    public bool? IsDefault { get; set; }
}
