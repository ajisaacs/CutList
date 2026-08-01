using System.ComponentModel;
using CutList.Core.Formatting;
using ModelContextProtocol.Server;

namespace CutList.Mcp;

/// <summary>
/// MCP tools for inventory management - materials and stock items.
/// All calls go through the CutList.Web REST API via ApiClient.
/// </summary>
[McpServerToolType]
public class InventoryTools
{
    private readonly ApiClient _api;

    public InventoryTools(ApiClient api)
    {
        _api = api;
    }

    #region Materials

    [McpServerTool(Name = "list_materials"), Description("Lists all materials (shape/size combinations) in the system.")]
    public async Task<MaterialListResult> ListMaterials(
        [Description("Filter by shape (e.g., 'Angle', 'FlatBar', 'RoundTube')")]
        string? shape = null,
        [Description("Include inactive materials (default false)")]
        bool includeInactive = false)
    {
        var materials = await _api.GetMaterialsAsync(shape, includeInactive);

        return new MaterialListResult
        {
            Success = true,
            Materials = materials.Select(MapMaterial).ToList()
        };
    }

    [McpServerTool(Name = "add_material"), Description("Adds a new material (shape/size combination) to the system with optional parsed dimensions.")]
    public async Task<MaterialResult> AddMaterial(
        [Description("Material shape (e.g., 'Angle', 'FlatBar', 'RoundTube', 'SquareTube', 'Channel', 'IBeam', 'Pipe')")]
        string shape,
        [Description("Material size string (e.g., '2 x 2 x 1/4'). If not provided, will be auto-generated from dimensions.")]
        string? size = null,
        [Description("Optional description")]
        string? description = null,
        [Description("Diameter in inches (for Round Bar)")]
        double? diameter = null,
        [Description("Outer diameter in inches (for Round Tube)")]
        double? outerDiameter = null,
        [Description("Width in inches (for Flat Bar, Rectangular Tube)")]
        double? width = null,
        [Description("Height in inches (for Rectangular Tube, Channel, I-Beam)")]
        double? height = null,
        [Description("Size in inches (for Square Bar, Square Tube - the side length)")]
        double? squareSize = null,
        [Description("Thickness in inches (for Flat Bar, Angle)")]
        double? thickness = null,
        [Description("Wall thickness in inches (for tubes, pipe)")]
        double? wall = null,
        [Description("Leg 1 length in inches (for Angle)")]
        double? leg1 = null,
        [Description("Leg 2 length in inches (for Angle)")]
        double? leg2 = null,
        [Description("Flange width in inches (for Channel)")]
        double? flange = null,
        [Description("Web thickness in inches (for Channel)")]
        double? web = null,
        [Description("Weight per foot in lbs (for I-Beam)")]
        double? weightPerFoot = null,
        [Description("Nominal pipe size in inches (for Pipe)")]
        double? nominalSize = null,
        [Description("Schedule (for Pipe, e.g., '40', '80', 'STD')")]
        string? schedule = null)
    {
        // Build dimensions dictionary from individual parameters
        var dimensions = new Dictionary<string, decimal>();
        if (diameter.HasValue) dimensions["Diameter"] = (decimal)diameter.Value;
        if (outerDiameter.HasValue) dimensions["OuterDiameter"] = (decimal)outerDiameter.Value;
        if (width.HasValue) dimensions["Width"] = (decimal)width.Value;
        if (height.HasValue) dimensions["Height"] = (decimal)height.Value;
        if (squareSize.HasValue) dimensions["Size"] = (decimal)squareSize.Value;
        if (thickness.HasValue) dimensions["Thickness"] = (decimal)thickness.Value;
        if (wall.HasValue) dimensions["Wall"] = (decimal)wall.Value;
        if (leg1.HasValue) dimensions["Leg1"] = (decimal)leg1.Value;
        if (leg2.HasValue) dimensions["Leg2"] = (decimal)leg2.Value;
        if (flange.HasValue) dimensions["Flange"] = (decimal)flange.Value;
        if (web.HasValue) dimensions["Web"] = (decimal)web.Value;
        if (weightPerFoot.HasValue) dimensions["WeightPerFoot"] = (decimal)weightPerFoot.Value;
        if (nominalSize.HasValue) dimensions["NominalSize"] = (decimal)nominalSize.Value;

        try
        {
            var material = await _api.CreateMaterialAsync(
                shape, size, description, null, null,
                dimensions.Count > 0 ? dimensions : null);

            if (material == null)
                return new MaterialResult { Success = false, Error = "Failed to create material" };

            return new MaterialResult
            {
                Success = true,
                Material = MapMaterial(material)
            };
        }
        catch (ApiConflictException ex)
        {
            return new MaterialResult { Success = false, Error = ex.Message };
        }
        catch (HttpRequestException ex)
        {
            return new MaterialResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "search_materials"), Description("Search for materials by shape with a target dimension value and tolerance. The primary dimension for each shape is searched (e.g., diameter for RoundBar, leg size for Angle).")]
    public async Task<MaterialListResult> SearchMaterials(
        [Description("Shape to search (e.g., 'RoundBar', 'Angle', 'FlatBar')")]
        string shape,
        [Description("Target dimension value in inches (or lbs for weightPerFoot)")]
        double targetValue,
        [Description("Tolerance value - returns results within +/- this amount (default 0.1)")]
        double tolerance = 0.1)
    {
        var materials = await _api.SearchMaterialsAsync(shape, (decimal)targetValue, (decimal)tolerance);

        return new MaterialListResult
        {
            Success = true,
            Materials = materials.Select(MapMaterial).ToList()
        };
    }

    #endregion

    #region Stock Items

    [McpServerTool(Name = "list_stock_items"), Description("Lists stock items (material lengths available in inventory).")]
    public async Task<StockItemListResult> ListStockItems(
        [Description("Filter by material ID")]
        int? materialId = null,
        [Description("Filter by shape (e.g., 'Angle')")]
        string? shape = null,
        [Description("Include inactive stock items (default false)")]
        bool includeInactive = false)
    {
        List<ApiStockItemDto> items;

        if (materialId.HasValue)
        {
            items = await _api.GetStockItemsAsync(materialId, includeInactive);
        }
        else if (!string.IsNullOrEmpty(shape))
        {
            // Get materials for this shape, then get stock items for each
            var materials = await _api.GetMaterialsAsync(shape, includeInactive);
            var allItems = new List<ApiStockItemDto>();
            foreach (var mat in materials)
            {
                var matItems = await _api.GetStockItemsAsync(mat.Id, includeInactive);
                allItems.AddRange(matItems);
            }
            items = allItems;
        }
        else
        {
            items = await _api.GetStockItemsAsync(includeInactive: includeInactive);
        }

        return new StockItemListResult
        {
            Success = true,
            StockItems = items.Select(s => new StockItemDto
            {
                Id = s.Id,
                MaterialId = s.MaterialId,
                MaterialName = s.MaterialName,
                LengthInches = s.LengthInches,
                LengthFormatted = s.LengthFormatted,
                Name = s.Name,
                Notes = s.Notes,
                IsActive = s.IsActive
            }).ToList()
        };
    }

    [McpServerTool(Name = "add_stock_item"), Description("Adds a new stock item (a specific length of material that can be stocked).")]
    public async Task<StockItemResult> AddStockItem(
        [Description("Material ID (use list_materials to find IDs)")]
        int materialId,
        [Description("Stock length (e.g., '20'', '240', '20 ft')")]
        string length,
        [Description("Optional name/label for this stock item")]
        string? name = null,
        [Description("Notes")]
        string? notes = null)
    {
        try
        {
            var stockItem = await _api.CreateStockItemAsync(materialId, length, name, notes);

            if (stockItem == null)
                return new StockItemResult { Success = false, Error = "Failed to create stock item" };

            return new StockItemResult
            {
                Success = true,
                StockItem = new StockItemDto
                {
                    Id = stockItem.Id,
                    MaterialId = stockItem.MaterialId,
                    MaterialName = stockItem.MaterialName,
                    LengthInches = stockItem.LengthInches,
                    LengthFormatted = stockItem.LengthFormatted,
                    Name = stockItem.Name,
                    Notes = stockItem.Notes,
                    IsActive = stockItem.IsActive
                }
            };
        }
        catch (ApiConflictException ex)
        {
            return new StockItemResult { Success = false, Error = ex.Message };
        }
        catch (HttpRequestException ex)
        {
            return new StockItemResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Convenience

    [McpServerTool(Name = "add_stock"), Description("Convenience method: adds a material (if needed) and a stock item (if needed), all in one call.")]
    public async Task<AddStockResult> AddStock(
        [Description("Material shape (e.g., 'Angle', 'FlatBar')")]
        string shape,
        [Description("Material size (e.g., '2 x 2 x 1/4')")]
        string size,
        [Description("Stock length (e.g., '20'', '240')")]
        string length,
        [Description("Material type: Steel, Aluminum, Stainless, Brass, Copper (default: Steel)")]
        string type = "Steel",
        [Description("Grade or specification (e.g., 'A36', 'Hot Roll', '304', '6061-T6')")]
        string? grade = null)
    {
        // Parse length for formatted display
        double lengthInches;
        try
        {
            lengthInches = double.TryParse(length.Trim(), out var plain)
                ? plain
                : ArchUnits.ParseToInches(length);
        }
        catch
        {
            return new AddStockResult
            {
                Success = false,
                Error = $"Could not parse length: {length}"
            };
        }

        // Step 1: Find or create material
        bool materialCreated = false;
        ApiMaterialDto? material = null;

        // Search for existing material by shape and size
        var materials = await _api.GetMaterialsAsync(shape);
        material = materials.FirstOrDefault(m =>
            m.Size.Equals(size, StringComparison.OrdinalIgnoreCase) &&
            m.Type.Equals(type, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(m.Grade, grade, StringComparison.OrdinalIgnoreCase));

        if (material == null)
        {
            // Parse dimensions from size string for the API
            var dimensions = ParseSizeStringToDimensions(shape, size);

            try
            {
                material = await _api.CreateMaterialAsync(shape, size, null, type, grade, dimensions);
                materialCreated = true;
            }
            catch (ApiConflictException)
            {
                // Race condition - material was created between check and create, re-fetch
                materials = await _api.GetMaterialsAsync(shape);
                material = materials.FirstOrDefault(m =>
                    m.Size.Equals(size, StringComparison.OrdinalIgnoreCase));
            }
            catch (HttpRequestException ex)
            {
                return new AddStockResult { Success = false, Error = $"Failed to create material: {ex.Message}" };
            }
        }

        if (material == null)
            return new AddStockResult { Success = false, Error = "Failed to find or create material" };

        // Step 2: Find or create stock item
        bool stockItemCreated = false;
        var stockItems = await _api.GetStockItemsAsync(material.Id);
        var stockItem = stockItems.FirstOrDefault(s => Math.Abs((double)s.LengthInches - lengthInches) < 0.01);

        if (stockItem == null)
        {
            try
            {
                stockItem = await _api.CreateStockItemAsync(material.Id, length, null, null);
                stockItemCreated = true;
            }
            catch (ApiConflictException)
            {
                // Race condition - re-fetch
                stockItems = await _api.GetStockItemsAsync(material.Id);
                stockItem = stockItems.FirstOrDefault(s => Math.Abs((double)s.LengthInches - lengthInches) < 0.01);
            }
            catch (HttpRequestException ex)
            {
                return new AddStockResult
                {
                    Success = false,
                    Error = $"Failed to create stock item: {ex.Message}",
                    MaterialCreated = materialCreated
                };
            }
        }

        if (stockItem == null)
            return new AddStockResult
            {
                Success = false,
                Error = "Failed to find or create stock item",
                MaterialCreated = materialCreated
            };

        return new AddStockResult
        {
            Success = true,
            MaterialId = material.Id,
            MaterialName = $"{material.Shape} - {material.Size}",
            MaterialCreated = materialCreated,
            StockItemId = stockItem.Id,
            StockItemCreated = stockItemCreated,
            LengthFormatted = ArchUnits.FormatFromInches(lengthInches)
        };
    }

    #endregion

    #region Helpers

    private static MaterialDto MapMaterial(ApiMaterialDto m)
    {
        var dto = new MaterialDto
        {
            Id = m.Id,
            Shape = m.Shape,
            Size = m.Size,
            Description = m.Description,
            DisplayName = $"{m.Shape} - {m.Size}",
            IsActive = m.IsActive,
        };

        if (m.Dimensions != null)
        {
            dto.Dimensions = MapDimensions(m.Dimensions);
        }

        return dto;
    }

    private static MaterialDimensionsDto MapDimensions(ApiMaterialDimensionsDto d)
    {
        var dto = new MaterialDimensionsDto();
        var v = d.Values;

        if (v.TryGetValue("Diameter", out var diameter)) dto.Diameter = (double)diameter;
        if (v.TryGetValue("OuterDiameter", out var od)) dto.OuterDiameter = (double)od;
        if (v.TryGetValue("Width", out var width)) dto.Width = (double)width;
        if (v.TryGetValue("Height", out var height)) dto.Height = (double)height;
        if (v.TryGetValue("Size", out var size)) dto.Size = (double)size;
        if (v.TryGetValue("Thickness", out var thickness)) dto.Thickness = (double)thickness;
        if (v.TryGetValue("Wall", out var wall)) dto.Wall = (double)wall;
        if (v.TryGetValue("Leg1", out var leg1)) dto.Leg1 = (double)leg1;
        if (v.TryGetValue("Leg2", out var leg2)) dto.Leg2 = (double)leg2;
        if (v.TryGetValue("Flange", out var flange)) dto.Flange = (double)flange;
        if (v.TryGetValue("Web", out var web)) dto.Web = (double)web;
        if (v.TryGetValue("WeightPerFoot", out var wpf)) dto.WeightPerFoot = (double)wpf;
        if (v.TryGetValue("NominalSize", out var ns)) dto.NominalSize = (double)ns;

        return dto;
    }

    /// <summary>
    /// Parses a size string into a dimensions dictionary based on shape.
    /// Format: values separated by 'x' (e.g., "1 1/2 x 1/8", "2 x 2 x 1/4")
    /// </summary>
    private static Dictionary<string, decimal>? ParseSizeStringToDimensions(string shape, string sizeString)
    {
        var parts = sizeString.Split('x', StringSplitOptions.TrimEntries)
            .Select(ParseDimension)
            .ToArray();

        if (parts.Length == 0 || parts.All(p => !p.HasValue))
            return null;

        decimal D(int i) => (decimal)parts[i]!.Value;

        var shapeLower = shape.ToLowerInvariant().Replace(" ", "");

        return shapeLower switch
        {
            "roundbar" when parts.Length >= 1 && parts[0].HasValue
                => new Dictionary<string, decimal> { ["Diameter"] = D(0) },

            "roundtube" when parts.Length >= 2 && parts[0].HasValue && parts[1].HasValue
                => new Dictionary<string, decimal> { ["OuterDiameter"] = D(0), ["Wall"] = D(1) },

            "flatbar" when parts.Length >= 2 && parts[0].HasValue && parts[1].HasValue
                => new Dictionary<string, decimal> { ["Width"] = D(0), ["Thickness"] = D(1) },

            "squarebar" when parts.Length >= 1 && parts[0].HasValue
                => new Dictionary<string, decimal> { ["Size"] = D(0) },

            "squaretube" when parts.Length >= 2 && parts[0].HasValue && parts[1].HasValue
                => new Dictionary<string, decimal> { ["Size"] = D(0), ["Wall"] = D(1) },

            "rectangulartube" or "recttube" when parts.Length >= 3 && parts[0].HasValue && parts[1].HasValue && parts[2].HasValue
                => new Dictionary<string, decimal> { ["Width"] = D(0), ["Height"] = D(1), ["Wall"] = D(2) },

            "angle" when parts.Length >= 3 && parts[0].HasValue && parts[1].HasValue && parts[2].HasValue
                => new Dictionary<string, decimal> { ["Leg1"] = D(0), ["Leg2"] = D(1), ["Thickness"] = D(2) },

            "channel" when parts.Length >= 3 && parts[0].HasValue && parts[1].HasValue && parts[2].HasValue
                => new Dictionary<string, decimal> { ["Height"] = D(0), ["Flange"] = D(1), ["Web"] = D(2) },

            "ibeam" when parts.Length >= 2 && parts[0].HasValue && parts[1].HasValue
                => new Dictionary<string, decimal> { ["Height"] = D(0), ["WeightPerFoot"] = D(1) },

            "pipe" when parts.Length >= 1 && parts[0].HasValue
                => new Dictionary<string, decimal> { ["NominalSize"] = D(0) },

            _ => null
        };

        static double? ParseDimension(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var processed = Fraction.ReplaceFractionsWithDecimals(value.Trim());
            return processed.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Sum(part => double.TryParse(part, out var d) ? d : 0) is > 0 and var total ? total : null;
        }
    }

    #endregion
}

#region DTOs

public class MaterialDimensionsDto
{
    public double? Diameter { get; set; }
    public double? OuterDiameter { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public double? Size { get; set; }
    public double? Thickness { get; set; }
    public double? Wall { get; set; }
    public double? Leg1 { get; set; }
    public double? Leg2 { get; set; }
    public double? Flange { get; set; }
    public double? Web { get; set; }
    public double? WeightPerFoot { get; set; }
    public double? NominalSize { get; set; }
    public string? Schedule { get; set; }
}

public class MaterialDto
{
    public int Id { get; set; }
    public string Shape { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public MaterialDimensionsDto? Dimensions { get; set; }
}

public class MaterialListResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<MaterialDto> Materials { get; set; } = new();
}

public class MaterialResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public MaterialDto? Material { get; set; }
}

public class StockItemDto
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public class StockItemListResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<StockItemDto> StockItems { get; set; } = new();
}

public class StockItemResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public StockItemDto? StockItem { get; set; }
}

public class AddStockResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public bool MaterialCreated { get; set; }
    public int StockItemId { get; set; }
    public bool StockItemCreated { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
}

#endregion
