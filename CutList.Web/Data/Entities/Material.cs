namespace CutList.Web.Data.Entities;

public class Material
{
    public int Id { get; set; }
    public string Shape { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<SupplierStock> SupplierStocks { get; set; } = new List<SupplierStock>();
    public ICollection<MaterialStockLength> StockLengths { get; set; } = new List<MaterialStockLength>();
    public ICollection<ProjectPart> ProjectParts { get; set; } = new List<ProjectPart>();

    public string DisplayName => $"{Shape} - {Size}";
}
