namespace CutList.Web.Data.Entities;

public class MaterialStockLength
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public decimal LengthInches { get; set; }
    public int Quantity { get; set; } = 0;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public Material Material { get; set; } = null!;
}
