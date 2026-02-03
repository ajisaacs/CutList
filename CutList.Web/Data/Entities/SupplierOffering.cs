namespace CutList.Web.Data.Entities;

public class SupplierOffering
{
    public int Id { get; set; }
    public int StockItemId { get; set; }
    public int SupplierId { get; set; }
    public string? PartNumber { get; set; }
    public string? SupplierDescription { get; set; }
    public decimal? Price { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public StockItem StockItem { get; set; } = null!;
    public Supplier Supplier { get; set; } = null!;
}
