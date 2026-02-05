namespace CutList.Web.DTOs;

public class SupplierDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactInfo { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public class CreateSupplierDto
{
    public string Name { get; set; } = string.Empty;
    public string? ContactInfo { get; set; }
    public string? Notes { get; set; }
}

public class UpdateSupplierDto
{
    public string? Name { get; set; }
    public string? ContactInfo { get; set; }
    public string? Notes { get; set; }
}

public class OfferingDto
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public int StockItemId { get; set; }
    public string? MaterialName { get; set; }
    public decimal? LengthInches { get; set; }
    public string? LengthFormatted { get; set; }
    public string? PartNumber { get; set; }
    public string? SupplierDescription { get; set; }
    public decimal? Price { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public class CreateOfferingDto
{
    public int StockItemId { get; set; }
    public string? PartNumber { get; set; }
    public string? SupplierDescription { get; set; }
    public decimal? Price { get; set; }
    public string? Notes { get; set; }
}

public class UpdateOfferingDto
{
    public string? PartNumber { get; set; }
    public string? SupplierDescription { get; set; }
    public decimal? Price { get; set; }
    public string? Notes { get; set; }
}
