namespace CutList.Web.Data.Entities;

public class Material
{
    public int Id { get; set; }
    public MaterialShape Shape { get; set; }

    /// <summary>
    /// Material type (Steel, Aluminum, Stainless, etc.)
    /// </summary>
    public MaterialType Type { get; set; }

    /// <summary>
    /// Grade or specification (e.g., "A36", "Hot Roll", "304", "6061-T6")
    /// </summary>
    public string? Grade { get; set; }

    public string Size { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Sort order based on primary dimension (stored as thousandths of an inch for numeric sorting).
    /// </summary>
    public int SortOrder { get; set; }

    public ICollection<StockItem> StockItems { get; set; } = new List<StockItem>();
    public ICollection<JobPart> JobParts { get; set; } = new List<JobPart>();

    /// <summary>
    /// Optional parsed dimensions for decimal-based searching.
    /// </summary>
    public MaterialDimensions? Dimensions { get; set; }

    public string DisplayName => $"{Shape.GetDisplayName()} - {Size}";
}
