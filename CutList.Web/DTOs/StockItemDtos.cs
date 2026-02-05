namespace CutList.Web.DTOs;

public class StockItemDto
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int QuantityOnHand { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public class CreateStockItemDto
{
    public int MaterialId { get; set; }
    public string Length { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int QuantityOnHand { get; set; }
    public string? Notes { get; set; }
}

public class UpdateStockItemDto
{
    public string? Length { get; set; }
    public string? Name { get; set; }
    public string? Notes { get; set; }
}

public class StockTransactionDto
{
    public int Id { get; set; }
    public int StockItemId { get; set; }
    public int Quantity { get; set; }
    public string Type { get; set; } = string.Empty;
    public int? JobId { get; set; }
    public string? JobNumber { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AddStockDto
{
    public int Quantity { get; set; }
    public int? SupplierId { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? Notes { get; set; }
}

public class UseStockDto
{
    public int Quantity { get; set; }
    public int? JobId { get; set; }
    public string? Notes { get; set; }
}

public class AdjustStockDto
{
    public int NewQuantity { get; set; }
    public string? Notes { get; set; }
}

public class ScrapStockDto
{
    public int Quantity { get; set; }
    public string? Notes { get; set; }
}

public class StockPricingDto
{
    public decimal? AverageCost { get; set; }
    public decimal? LastPurchasePrice { get; set; }
}
