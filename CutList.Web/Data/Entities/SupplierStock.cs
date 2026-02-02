namespace CutList.Web.Data.Entities;

public class SupplierStock
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public int MaterialId { get; set; }
    public decimal LengthInches { get; set; }
    public decimal? Price { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public Supplier Supplier { get; set; } = null!;
    public Material Material { get; set; } = null!;
}
