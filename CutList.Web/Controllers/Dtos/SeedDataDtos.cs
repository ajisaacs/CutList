using System.Text.Json.Serialization;

namespace CutList.Web.Controllers.Dtos;

public class SeedExportData
{
    public DateTime ExportedAt { get; set; }
    public List<SeedSupplierDto> Suppliers { get; set; } = [];
    public List<SeedCuttingToolDto> CuttingTools { get; set; } = [];
    public List<SeedMaterialDto> Materials { get; set; } = [];
}

public class SeedSupplierDto
{
    public string Name { get; set; } = "";
    public string? ContactInfo { get; set; }
    public string? Notes { get; set; }
}

public class SeedCuttingToolDto
{
    public string Name { get; set; } = "";
    public decimal KerfInches { get; set; }
    public bool IsDefault { get; set; }
}

public class SeedMaterialDto
{
    public string Shape { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Grade { get; set; }
    public string Size { get; set; } = "";
    public string? Description { get; set; }
    public SeedDimensionsDto? Dimensions { get; set; }
    public List<SeedStockItemDto> StockItems { get; set; } = [];
}

public class SeedDimensionsDto
{
    public decimal? Diameter { get; set; }
    public decimal? OuterDiameter { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Thickness { get; set; }
    public decimal? Wall { get; set; }
    public decimal? Size { get; set; }
    public decimal? Leg1 { get; set; }
    public decimal? Leg2 { get; set; }
    public decimal? Flange { get; set; }
    public decimal? Web { get; set; }
    public decimal? WeightPerFoot { get; set; }
    public decimal? NominalSize { get; set; }
    public string? Schedule { get; set; }
}

public class SeedStockItemDto
{
    public decimal LengthInches { get; set; }
    public string? Name { get; set; }
    public int QuantityOnHand { get; set; }
    public string? Notes { get; set; }
    public List<SeedSupplierOfferingDto> SupplierOfferings { get; set; } = [];
}

public class SeedSupplierOfferingDto
{
    public string SupplierName { get; set; } = "";
    public string? PartNumber { get; set; }
    public string? SupplierDescription { get; set; }
    public decimal? Price { get; set; }
    public string? Notes { get; set; }
}
