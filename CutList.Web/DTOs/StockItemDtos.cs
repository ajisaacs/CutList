namespace CutList.Web.DTOs;

public class StockItemDto
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public class CreateStockItemDto
{
    public int MaterialId { get; set; }
    public string Length { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Notes { get; set; }
}

public class UpdateStockItemDto
{
    public string? Length { get; set; }
    public string? Name { get; set; }
    public string? Notes { get; set; }
}
