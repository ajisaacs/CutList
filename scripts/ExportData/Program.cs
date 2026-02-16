using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

// Build DbContext with the same connection string
var connectionString = "Server=localhost\\SQLEXPRESS;Database=CutListDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(connectionString)
    .Options;

using var db = new ApplicationDbContext(options);

// Load all catalog data
var materials = await db.Materials
    .Include(m => m.Dimensions)
    .Include(m => m.StockItems.Where(s => s.IsActive))
        .ThenInclude(s => s.SupplierOfferings.Where(o => o.IsActive))
    .Where(m => m.IsActive)
    .OrderBy(m => m.Shape).ThenBy(m => m.SortOrder)
    .AsNoTracking()
    .ToListAsync();

var suppliers = await db.Suppliers
    .Where(s => s.IsActive)
    .OrderBy(s => s.Name)
    .AsNoTracking()
    .ToListAsync();

var cuttingTools = await db.CuttingTools
    .Where(t => t.IsActive)
    .OrderBy(t => t.Name)
    .AsNoTracking()
    .ToListAsync();

// Build export DTOs to avoid circular references and keep it clean
var export = new ExportData
{
    ExportedAt = DateTime.UtcNow,
    Suppliers = suppliers.Select(s => new SupplierDto
    {
        Name = s.Name,
        ContactInfo = s.ContactInfo,
        Notes = s.Notes
    }).ToList(),
    CuttingTools = cuttingTools.Select(t => new CuttingToolDto
    {
        Name = t.Name,
        KerfInches = t.KerfInches,
        IsDefault = t.IsDefault
    }).ToList(),
    Materials = materials.Select(m => new MaterialDto
    {
        Shape = m.Shape.ToString(),
        Type = m.Type.ToString(),
        Grade = m.Grade,
        Size = m.Size,
        Description = m.Description,
        Dimensions = MapDimensions(m.Dimensions),
        StockItems = m.StockItems.OrderBy(s => s.LengthInches).Select(s => new StockItemDto
        {
            LengthInches = s.LengthInches,
            Name = s.Name,
            QuantityOnHand = s.QuantityOnHand,
            Notes = s.Notes,
            SupplierOfferings = s.SupplierOfferings.Select(o => new SupplierOfferingDto
            {
                SupplierName = suppliers.FirstOrDefault(sup => sup.Id == o.SupplierId)?.Name ?? "Unknown",
                PartNumber = o.PartNumber,
                SupplierDescription = o.SupplierDescription,
                Price = o.Price,
                Notes = o.Notes
            }).ToList()
        }).ToList()
    }).ToList()
};

// Serialize to JSON
var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};

var json = JsonSerializer.Serialize(export, jsonOptions);

// Determine output path - default to CutList.Web/Data/SeedData/
var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var outputPath = args.Length > 0
    ? args[0]
    : Path.Combine(repoRoot, "CutList.Web", "Data", "SeedData", "oneals-catalog.json");

var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputPath))!;
Directory.CreateDirectory(outputDir);

await File.WriteAllTextAsync(outputPath, json);

var fullPath = Path.GetFullPath(outputPath);
Console.WriteLine($"Exported {export.Materials.Count} materials, {export.Materials.Sum(m => m.StockItems.Count)} stock items, {export.Suppliers.Count} suppliers");
Console.WriteLine($"Written to: {fullPath}");

// --- Helper ---
static DimensionsDto? MapDimensions(MaterialDimensions? dim) => dim switch
{
    RoundBarDimensions d => new DimensionsDto { Diameter = d.Diameter },
    RoundTubeDimensions d => new DimensionsDto { OuterDiameter = d.OuterDiameter, Wall = d.Wall },
    FlatBarDimensions d => new DimensionsDto { Width = d.Width, Thickness = d.Thickness },
    SquareBarDimensions d => new DimensionsDto { Size = d.Size },
    SquareTubeDimensions d => new DimensionsDto { Size = d.Size, Wall = d.Wall },
    RectangularTubeDimensions d => new DimensionsDto { Width = d.Width, Height = d.Height, Wall = d.Wall },
    AngleDimensions d => new DimensionsDto { Leg1 = d.Leg1, Leg2 = d.Leg2, Thickness = d.Thickness },
    ChannelDimensions d => new DimensionsDto { Height = d.Height, Flange = d.Flange, Web = d.Web },
    IBeamDimensions d => new DimensionsDto { Height = d.Height, WeightPerFoot = d.WeightPerFoot },
    PipeDimensions d => new DimensionsDto { NominalSize = d.NominalSize, Wall = d.Wall, Schedule = d.Schedule },
    _ => null
};

// --- DTOs ---
class ExportData
{
    public DateTime ExportedAt { get; set; }
    public List<SupplierDto> Suppliers { get; set; } = [];
    public List<CuttingToolDto> CuttingTools { get; set; } = [];
    public List<MaterialDto> Materials { get; set; } = [];
}

class SupplierDto
{
    public string Name { get; set; } = "";
    public string? ContactInfo { get; set; }
    public string? Notes { get; set; }
}

class CuttingToolDto
{
    public string Name { get; set; } = "";
    public decimal KerfInches { get; set; }
    public bool IsDefault { get; set; }
}

class MaterialDto
{
    public string Shape { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Grade { get; set; }
    public string Size { get; set; } = "";
    public string? Description { get; set; }
    public DimensionsDto? Dimensions { get; set; }
    public List<StockItemDto> StockItems { get; set; } = [];
}

class DimensionsDto
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

class StockItemDto
{
    public decimal LengthInches { get; set; }
    public string? Name { get; set; }
    public int QuantityOnHand { get; set; }
    public string? Notes { get; set; }
    public List<SupplierOfferingDto> SupplierOfferings { get; set; } = [];
}

class SupplierOfferingDto
{
    public string SupplierName { get; set; } = "";
    public string? PartNumber { get; set; }
    public string? SupplierDescription { get; set; }
    public decimal? Price { get; set; }
    public string? Notes { get; set; }
}
