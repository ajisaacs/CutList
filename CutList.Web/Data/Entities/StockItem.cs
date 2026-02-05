namespace CutList.Web.Data.Entities;

public class StockItem
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public decimal LengthInches { get; set; }
    public string? Name { get; set; }
    public int QuantityOnHand { get; set; } = 0;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Material Material { get; set; } = null!;
    public ICollection<SupplierOffering> SupplierOfferings { get; set; } = new List<SupplierOffering>();
    public ICollection<StockTransaction> Transactions { get; set; } = new List<StockTransaction>();
}
