namespace CutList.Web.DTOs;

public class CatalogData
{
    public DateTime ExportedAt { get; set; }
    public List<CatalogSupplierDto> Suppliers { get; set; } = [];
    public List<CatalogCuttingToolDto> CuttingTools { get; set; } = [];
    public List<CatalogMaterialDto> Materials { get; set; } = [];
}

public class CatalogSupplierDto
{
    public string Name { get; set; } = "";
    public string? ContactInfo { get; set; }
    public string? Notes { get; set; }
}

public class CatalogCuttingToolDto
{
    public string Name { get; set; } = "";
    public decimal KerfInches { get; set; }
    public bool IsDefault { get; set; }
}

public class CatalogMaterialDto
{
    public string Shape { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Grade { get; set; }
    public string Size { get; set; } = "";
    public string? Description { get; set; }
    public CatalogDimensionsDto? Dimensions { get; set; }
    public List<CatalogStockItemDto> StockItems { get; set; } = [];
}

public class CatalogDimensionsDto
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

public class CatalogStockItemDto
{
    public decimal LengthInches { get; set; }
    public string? Name { get; set; }
    public int QuantityOnHand { get; set; }
    public string? Notes { get; set; }
    public List<CatalogSupplierOfferingDto> SupplierOfferings { get; set; } = [];
}

public class CatalogSupplierOfferingDto
{
    public string SupplierName { get; set; } = "";
    public string? PartNumber { get; set; }
    public string? SupplierDescription { get; set; }
    public decimal? Price { get; set; }
    public string? Notes { get; set; }
}

public class ImportResultDto
{
    public int SuppliersCreated { get; set; }
    public int SuppliersUpdated { get; set; }
    public int CuttingToolsCreated { get; set; }
    public int CuttingToolsUpdated { get; set; }
    public int MaterialsCreated { get; set; }
    public int MaterialsUpdated { get; set; }
    public int StockItemsCreated { get; set; }
    public int StockItemsUpdated { get; set; }
    public int OfferingsCreated { get; set; }
    public int OfferingsUpdated { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}
