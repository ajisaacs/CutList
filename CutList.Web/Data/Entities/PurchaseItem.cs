namespace CutList.Web.Data.Entities;

public enum PurchaseItemStatus
{
    Pending,
    Ordered,
    Received
}

public class PurchaseItem
{
    public int Id { get; set; }
    public int StockItemId { get; set; }
    public int? SupplierId { get; set; }
    public int Quantity { get; set; }
    public int? JobId { get; set; }
    public string? Notes { get; set; }
    public PurchaseItemStatus Status { get; set; } = PurchaseItemStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public StockItem StockItem { get; set; } = null!;
    public Supplier? Supplier { get; set; }
    public Job? Job { get; set; }
}
