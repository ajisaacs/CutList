namespace CutList.Web.Data.Entities;

public class StockTransaction
{
    public int Id { get; set; }
    public int StockItemId { get; set; }
    public int Quantity { get; set; }
    public StockTransactionType Type { get; set; }
    public int? JobId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public StockItem StockItem { get; set; } = null!;
    public Job? Job { get; set; }
}

public enum StockTransactionType
{
    Received,
    Used,
    Adjustment,
    Scrapped,
    Returned
}
