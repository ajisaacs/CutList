namespace CutList.Web.Data.Entities;

/// <summary>
/// Represents stock allocated to a specific job.
/// Can reference an existing StockItem with quantity override,
/// or define a custom length just for this job.
/// </summary>
public class JobStock
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public int MaterialId { get; set; }

    /// <summary>
    /// If set, references an existing stock item. Null for custom job-specific lengths.
    /// </summary>
    public int? StockItemId { get; set; }

    /// <summary>
    /// Length in inches. For stock items, copied from StockItem.LengthInches.
    /// For custom lengths, user-specified.
    /// </summary>
    public decimal LengthInches { get; set; }

    /// <summary>
    /// Quantity to use for this job. Can be less than or equal to available stock.
    /// For custom lengths, represents unlimited available.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// True if this is a custom length just for this job (not from inventory).
    /// </summary>
    public bool IsCustomLength { get; set; }

    /// <summary>
    /// Priority for bin packing. Lower values are used first.
    /// </summary>
    public int Priority { get; set; } = 10;

    public int SortOrder { get; set; }

    public Job Job { get; set; } = null!;
    public Material Material { get; set; } = null!;
    public StockItem? StockItem { get; set; }
}
