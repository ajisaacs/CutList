using System.ComponentModel;
using CutList.Core.Formatting;
using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace CutList.Mcp;

/// <summary>
/// MCP tools for inventory management - suppliers, materials, stock items, and offerings.
/// </summary>
[McpServerToolType]
public class InventoryTools
{
    private readonly ApplicationDbContext _context;

    public InventoryTools(ApplicationDbContext context)
    {
        _context = context;
    }

    #region Suppliers

    [McpServerTool(Name = "list_suppliers"), Description("Lists all suppliers in the system.")]
    public async Task<SupplierListResult> ListSuppliers(
        [Description("Include inactive suppliers (default false)")]
        bool includeInactive = false)
    {
        var query = _context.Suppliers.AsQueryable();
        if (!includeInactive)
            query = query.Where(s => s.IsActive);

        var suppliers = await query.OrderBy(s => s.Name).ToListAsync();

        return new SupplierListResult
        {
            Success = true,
            Suppliers = suppliers.Select(s => new SupplierDto
            {
                Id = s.Id,
                Name = s.Name,
                ContactInfo = s.ContactInfo,
                Notes = s.Notes,
                IsActive = s.IsActive
            }).ToList()
        };
    }

    [McpServerTool(Name = "add_supplier"), Description("Adds a new supplier to the system.")]
    public async Task<SupplierResult> AddSupplier(
        [Description("Supplier name (e.g., 'O'Neal Steel')")]
        string name,
        [Description("Contact info - website, phone, email, etc.")]
        string? contactInfo = null,
        [Description("Notes about the supplier")]
        string? notes = null)
    {
        var supplier = new Supplier
        {
            Name = name,
            ContactInfo = contactInfo,
            Notes = notes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        return new SupplierResult
        {
            Success = true,
            Supplier = new SupplierDto
            {
                Id = supplier.Id,
                Name = supplier.Name,
                ContactInfo = supplier.ContactInfo,
                Notes = supplier.Notes,
                IsActive = supplier.IsActive
            }
        };
    }

    #endregion

    #region Materials

    [McpServerTool(Name = "list_materials"), Description("Lists all materials (shape/size combinations) in the system.")]
    public async Task<MaterialListResult> ListMaterials(
        [Description("Filter by shape (e.g., 'Angle', 'FlatBar', 'RoundTube')")]
        string? shape = null,
        [Description("Include inactive materials (default false)")]
        bool includeInactive = false)
    {
        var query = _context.Materials
            .Include(m => m.Dimensions)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(m => m.IsActive);

        if (!string.IsNullOrEmpty(shape))
        {
            var parsedShape = MaterialShapeExtensions.ParseShape(shape);
            if (parsedShape.HasValue)
            {
                query = query.Where(m => m.Shape == parsedShape.Value);
            }
            else
            {
                // Fallback to string-based display name search
                var shapeLower = shape.ToLower();
                query = query.Where(m => m.Shape.ToString().ToLower().Contains(shapeLower));
            }
        }

        var materials = await query
            .OrderBy(m => m.Shape)
            .ThenBy(m => m.SortOrder)
            .ThenBy(m => m.Size)
            .ToListAsync();

        return new MaterialListResult
        {
            Success = true,
            Materials = materials.Select(m => new MaterialDto
            {
                Id = m.Id,
                Shape = m.Shape.GetDisplayName(),
                Size = m.Size,
                Description = m.Description,
                DisplayName = m.DisplayName,
                IsActive = m.IsActive,
                Dimensions = m.Dimensions != null ? MapDimensions(m.Dimensions) : null
            }).ToList()
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
        var parsedShape = MaterialShapeExtensions.ParseShape(shape);
        if (!parsedShape.HasValue)
        {
            return new MaterialResult
            {
                Success = false,
                Error = $"Unknown shape: {shape}. Valid shapes are: RoundBar, RoundTube, FlatBar, SquareBar, SquareTube, RectangularTube, Angle, Channel, IBeam, Pipe"
            };
        }

        // Create typed dimensions object based on shape
        MaterialDimensions? dimensions = CreateDimensionsForShape(
            parsedShape.Value, diameter, outerDiameter, width, height, squareSize,
            thickness, wall, leg1, leg2, flange, web, weightPerFoot, nominalSize, schedule);

        // Auto-generate size string if not provided
        var finalSize = size ?? dimensions?.GenerateSizeString();
        if (string.IsNullOrWhiteSpace(finalSize))
        {
            return new MaterialResult
            {
                Success = false,
                Error = "Size string is required. Either provide a size parameter or dimension values for auto-generation."
            };
        }

        // Check if material already exists
        var existing = await _context.Materials
            .FirstOrDefaultAsync(m => m.Shape == parsedShape.Value && m.Size.ToLower() == finalSize.ToLower());

        if (existing != null)
        {
            return new MaterialResult
            {
                Success = false,
                Error = $"Material '{parsedShape.Value.GetDisplayName()} - {finalSize}' already exists with ID {existing.Id}"
            };
        }

        var material = new Material
        {
            Shape = parsedShape.Value,
            Size = finalSize,
            Description = description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Materials.Add(material);
        await _context.SaveChangesAsync();

        // Add dimensions
        dimensions.MaterialId = material.Id;
        _context.MaterialDimensions.Add(dimensions);
        await _context.SaveChangesAsync();

        return new MaterialResult
        {
            Success = true,
            Material = new MaterialDto
            {
                Id = material.Id,
                Shape = material.Shape.GetDisplayName(),
                Size = material.Size,
                Description = material.Description,
                DisplayName = material.DisplayName,
                IsActive = material.IsActive,
                Dimensions = MapDimensions(dimensions)
            }
        };
    }

    [McpServerTool(Name = "search_materials"), Description("Search for materials by dimension value with tolerance. Returns materials where the specified dimension is within +/- tolerance of the target value.")]
    public async Task<MaterialListResult> SearchMaterials(
        [Description("Which dimension to search: diameter, outerDiameter, width, height, size, thickness, wall, leg1, leg2, flange, web, weightPerFoot, nominalSize")]
        string dimensionType,
        [Description("Target dimension value in inches (or lbs for weightPerFoot)")]
        double targetValue,
        [Description("Tolerance value - returns results within +/- this amount (default 0.01)")]
        double tolerance = 0.01,
        [Description("Optional shape filter (e.g., 'RoundBar', 'Angle')")]
        string? shape = null)
    {
        var minValue = (decimal)(targetValue - tolerance);
        var maxValue = (decimal)(targetValue + tolerance);

        // Search the appropriate typed dimension table based on dimension type
        List<Material> materials = dimensionType.ToLowerInvariant() switch
        {
            "diameter" => await SearchByDimension<RoundBarDimensions>(d => d.Diameter >= minValue && d.Diameter <= maxValue, shape),
            "outerdiameter" or "outer_diameter" or "od" => await SearchByDimension<RoundTubeDimensions>(d => d.OuterDiameter >= minValue && d.OuterDiameter <= maxValue, shape),
            "width" => await SearchWidthDimensions(minValue, maxValue, shape),
            "height" => await SearchHeightDimensions(minValue, maxValue, shape),
            "size" or "squaresize" => await SearchSizeDimensions(minValue, maxValue, shape),
            "thickness" => await SearchThicknessDimensions(minValue, maxValue, shape),
            "wall" => await SearchWallDimensions(minValue, maxValue, shape),
            "leg1" => await SearchByDimension<AngleDimensions>(d => d.Leg1 >= minValue && d.Leg1 <= maxValue, shape),
            "leg2" => await SearchByDimension<AngleDimensions>(d => d.Leg2 >= minValue && d.Leg2 <= maxValue, shape),
            "flange" => await SearchByDimension<ChannelDimensions>(d => d.Flange >= minValue && d.Flange <= maxValue, shape),
            "web" => await SearchByDimension<ChannelDimensions>(d => d.Web >= minValue && d.Web <= maxValue, shape),
            "weightperfoot" or "weight" => await SearchByDimension<IBeamDimensions>(d => d.WeightPerFoot >= minValue && d.WeightPerFoot <= maxValue, shape),
            "nominalsize" or "nominal_size" or "nps" => await SearchByDimension<PipeDimensions>(d => d.NominalSize >= minValue && d.NominalSize <= maxValue, shape),
            _ => throw new ArgumentException($"Unknown dimension type: {dimensionType}")
        };

        return new MaterialListResult
        {
            Success = true,
            Materials = materials.Select(m => new MaterialDto
            {
                Id = m.Id,
                Shape = m.Shape.GetDisplayName(),
                Size = m.Size,
                Description = m.Description,
                DisplayName = m.DisplayName,
                IsActive = m.IsActive,
                Dimensions = m.Dimensions != null ? MapDimensions(m.Dimensions) : null
            }).ToList()
        };
    }

    private async Task<List<Material>> SearchByDimension<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate, string? shape) where T : MaterialDimensions
    {
        var query = _context.Set<T>()
            .Include(d => d.Material)
            .Where(d => d.Material.IsActive)
            .Where(predicate);

        if (!string.IsNullOrEmpty(shape))
        {
            var parsedShape = MaterialShapeExtensions.ParseShape(shape);
            if (parsedShape.HasValue)
            {
                query = query.Where(d => d.Material.Shape == parsedShape.Value);
            }
        }

        return await query
            .Select(d => d.Material)
            .OrderBy(m => m.Shape)
            .ThenBy(m => m.SortOrder)
            .ThenBy(m => m.Size)
            .ToListAsync();
    }

    // Width is used by FlatBar, RectangularTube
    private async Task<List<Material>> SearchWidthDimensions(decimal minValue, decimal maxValue, string? shape)
    {
        var flatBars = await SearchByDimension<FlatBarDimensions>(d => d.Width >= minValue && d.Width <= maxValue, shape);
        var rectTubes = await SearchByDimension<RectangularTubeDimensions>(d => d.Width >= minValue && d.Width <= maxValue, shape);
        return flatBars.Concat(rectTubes).OrderBy(m => m.Shape).ThenBy(m => m.SortOrder).ThenBy(m => m.Size).ToList();
    }

    // Height is used by RectangularTube, Channel, IBeam
    private async Task<List<Material>> SearchHeightDimensions(decimal minValue, decimal maxValue, string? shape)
    {
        var rectTubes = await SearchByDimension<RectangularTubeDimensions>(d => d.Height >= minValue && d.Height <= maxValue, shape);
        var channels = await SearchByDimension<ChannelDimensions>(d => d.Height >= minValue && d.Height <= maxValue, shape);
        var ibeams = await SearchByDimension<IBeamDimensions>(d => d.Height >= minValue && d.Height <= maxValue, shape);
        return rectTubes.Concat(channels).Concat(ibeams).OrderBy(m => m.Shape).ThenBy(m => m.SortOrder).ThenBy(m => m.Size).ToList();
    }

    // Size is used by SquareBar, SquareTube
    private async Task<List<Material>> SearchSizeDimensions(decimal minValue, decimal maxValue, string? shape)
    {
        var squareBars = await SearchByDimension<SquareBarDimensions>(d => d.Size >= minValue && d.Size <= maxValue, shape);
        var squareTubes = await SearchByDimension<SquareTubeDimensions>(d => d.Size >= minValue && d.Size <= maxValue, shape);
        return squareBars.Concat(squareTubes).OrderBy(m => m.Shape).ThenBy(m => m.SortOrder).ThenBy(m => m.Size).ToList();
    }

    // Thickness is used by FlatBar, Angle
    private async Task<List<Material>> SearchThicknessDimensions(decimal minValue, decimal maxValue, string? shape)
    {
        var flatBars = await SearchByDimension<FlatBarDimensions>(d => d.Thickness >= minValue && d.Thickness <= maxValue, shape);
        var angles = await SearchByDimension<AngleDimensions>(d => d.Thickness >= minValue && d.Thickness <= maxValue, shape);
        return flatBars.Concat(angles).OrderBy(m => m.Shape).ThenBy(m => m.SortOrder).ThenBy(m => m.Size).ToList();
    }

    // Wall is used by RoundTube, SquareTube, RectangularTube, Pipe
    private async Task<List<Material>> SearchWallDimensions(decimal minValue, decimal maxValue, string? shape)
    {
        var roundTubes = await SearchByDimension<RoundTubeDimensions>(d => d.Wall >= minValue && d.Wall <= maxValue, shape);
        var squareTubes = await SearchByDimension<SquareTubeDimensions>(d => d.Wall >= minValue && d.Wall <= maxValue, shape);
        var rectTubes = await SearchByDimension<RectangularTubeDimensions>(d => d.Wall >= minValue && d.Wall <= maxValue, shape);
        var pipes = await SearchByDimension<PipeDimensions>(d => d.Wall != null && d.Wall >= minValue && d.Wall <= maxValue, shape);
        return roundTubes.Concat(squareTubes).Concat(rectTubes).Concat(pipes).OrderBy(m => m.Shape).ThenBy(m => m.SortOrder).ThenBy(m => m.Size).ToList();
    }

    private static MaterialDimensions? CreateDimensionsForShape(
        MaterialShape shape, double? diameter, double? outerDiameter, double? width, double? height,
        double? squareSize, double? thickness, double? wall, double? leg1, double? leg2,
        double? flange, double? web, double? weightPerFoot, double? nominalSize, string? schedule)
    {
        return shape switch
        {
            MaterialShape.RoundBar when diameter.HasValue => new RoundBarDimensions { Diameter = (decimal)diameter.Value },
            MaterialShape.RoundTube when outerDiameter.HasValue && wall.HasValue => new RoundTubeDimensions { OuterDiameter = (decimal)outerDiameter.Value, Wall = (decimal)wall.Value },
            MaterialShape.FlatBar when width.HasValue && thickness.HasValue => new FlatBarDimensions { Width = (decimal)width.Value, Thickness = (decimal)thickness.Value },
            MaterialShape.SquareBar when squareSize.HasValue => new SquareBarDimensions { Size = (decimal)squareSize.Value },
            MaterialShape.SquareTube when squareSize.HasValue && wall.HasValue => new SquareTubeDimensions { Size = (decimal)squareSize.Value, Wall = (decimal)wall.Value },
            MaterialShape.RectangularTube when width.HasValue && height.HasValue && wall.HasValue => new RectangularTubeDimensions { Width = (decimal)width.Value, Height = (decimal)height.Value, Wall = (decimal)wall.Value },
            MaterialShape.Angle when leg1.HasValue && leg2.HasValue && thickness.HasValue => new AngleDimensions { Leg1 = (decimal)leg1.Value, Leg2 = (decimal)leg2.Value, Thickness = (decimal)thickness.Value },
            MaterialShape.Channel when height.HasValue && flange.HasValue && web.HasValue => new ChannelDimensions { Height = (decimal)height.Value, Flange = (decimal)flange.Value, Web = (decimal)web.Value },
            MaterialShape.IBeam when height.HasValue && weightPerFoot.HasValue => new IBeamDimensions { Height = (decimal)height.Value, WeightPerFoot = (decimal)weightPerFoot.Value },
            MaterialShape.Pipe when nominalSize.HasValue => new PipeDimensions { NominalSize = (decimal)nominalSize.Value, Wall = wall.HasValue ? (decimal)wall.Value : null, Schedule = schedule },
            _ => null
        };
    }

    /// <summary>
    /// Parses a size string into MaterialDimensions based on shape.
    /// Format: values separated by 'x' (e.g., "1 1/2 x 1/8", "2 x 2 x 1/4")
    /// </summary>
    private static (MaterialDimensions? dimensions, int sortOrder) ParseSizeStringToDimensions(MaterialShape shape, string sizeString)
    {
        var p = sizeString.Split('x', StringSplitOptions.TrimEntries)
            .Select(ParseDimension)
            .ToArray();

        decimal D(int i) => (decimal)p[i]!.Value;
        int Sort(double v) => (int)(v * 1000);

        return shape switch
        {
            MaterialShape.RoundBar when p.Length >= 1 && p[0].HasValue
                => (new RoundBarDimensions { Diameter = D(0) }, Sort(p[0]!.Value)),

            MaterialShape.RoundTube when p.Length >= 2 && p[0].HasValue && p[1].HasValue
                => (new RoundTubeDimensions { OuterDiameter = D(0), Wall = D(1) }, Sort(p[0]!.Value)),

            MaterialShape.FlatBar when p.Length >= 2 && p[0].HasValue && p[1].HasValue
                => (new FlatBarDimensions { Width = D(0), Thickness = D(1) }, Sort(p[0]!.Value)),

            MaterialShape.SquareBar when p.Length >= 1 && p[0].HasValue
                => (new SquareBarDimensions { Size = D(0) }, Sort(p[0]!.Value)),

            MaterialShape.SquareTube when p.Length >= 2 && p[0].HasValue && p[1].HasValue
                => (new SquareTubeDimensions { Size = D(0), Wall = D(1) }, Sort(p[0]!.Value)),

            MaterialShape.RectangularTube when p.Length >= 3 && p[0].HasValue && p[1].HasValue && p[2].HasValue
                => (new RectangularTubeDimensions { Width = D(0), Height = D(1), Wall = D(2) }, Sort(Math.Max(p[0]!.Value, p[1]!.Value))),

            MaterialShape.Angle when p.Length >= 3 && p[0].HasValue && p[1].HasValue && p[2].HasValue
                => (new AngleDimensions { Leg1 = D(0), Leg2 = D(1), Thickness = D(2) }, Sort(Math.Max(p[0]!.Value, p[1]!.Value))),

            MaterialShape.Channel when p.Length >= 3 && p[0].HasValue && p[1].HasValue && p[2].HasValue
                => (new ChannelDimensions { Height = D(0), Flange = D(1), Web = D(2) }, Sort(p[0]!.Value)),

            MaterialShape.IBeam when p.Length >= 2 && p[0].HasValue && p[1].HasValue
                => (new IBeamDimensions { Height = D(0), WeightPerFoot = D(1) }, Sort(p[0]!.Value)),

            MaterialShape.Pipe when p.Length >= 1 && p[0].HasValue
                => (new PipeDimensions { NominalSize = D(0), Schedule = ParsePipeSchedule(sizeString) }, Sort(p[0]!.Value)),

            _ => (null, 0)
        };

        static double? ParseDimension(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var processed = Fraction.ReplaceFractionsWithDecimals(value.Trim());
            return processed.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Sum(part => double.TryParse(part, out var d) ? d : 0) is > 0 and var total ? total : null;
        }

        static string? ParsePipeSchedule(string s)
        {
            var idx = s.IndexOf("SCH", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var rest = s[(idx + 3)..].Trim();
            var end = rest.IndexOfAny([' ', 'x', 'X']);
            return end > 0 ? rest[..end] : rest;
        }
    }

    private static MaterialDimensionsDto MapDimensions(MaterialDimensions d)
    {
        var dto = new MaterialDimensionsDto();

        switch (d)
        {
            case RoundBarDimensions rb:
                dto.Diameter = (double)rb.Diameter;
                break;
            case RoundTubeDimensions rt:
                dto.OuterDiameter = (double)rt.OuterDiameter;
                dto.Wall = (double)rt.Wall;
                break;
            case FlatBarDimensions fb:
                dto.Width = (double)fb.Width;
                dto.Thickness = (double)fb.Thickness;
                break;
            case SquareBarDimensions sb:
                dto.Size = (double)sb.Size;
                break;
            case SquareTubeDimensions st:
                dto.Size = (double)st.Size;
                dto.Wall = (double)st.Wall;
                break;
            case RectangularTubeDimensions rect:
                dto.Width = (double)rect.Width;
                dto.Height = (double)rect.Height;
                dto.Wall = (double)rect.Wall;
                break;
            case AngleDimensions a:
                dto.Leg1 = (double)a.Leg1;
                dto.Leg2 = (double)a.Leg2;
                dto.Thickness = (double)a.Thickness;
                break;
            case ChannelDimensions c:
                dto.Height = (double)c.Height;
                dto.Flange = (double)c.Flange;
                dto.Web = (double)c.Web;
                break;
            case IBeamDimensions i:
                dto.Height = (double)i.Height;
                dto.WeightPerFoot = (double)i.WeightPerFoot;
                break;
            case PipeDimensions p:
                dto.NominalSize = (double)p.NominalSize;
                dto.Wall = p.Wall.HasValue ? (double)p.Wall.Value : null;
                dto.Schedule = p.Schedule;
                break;
        }

        return dto;
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
        var query = _context.StockItems
            .Include(s => s.Material)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(s => s.IsActive);

        if (materialId.HasValue)
            query = query.Where(s => s.MaterialId == materialId.Value);

        if (!string.IsNullOrEmpty(shape))
        {
            var parsedShape = MaterialShapeExtensions.ParseShape(shape);
            if (parsedShape.HasValue)
            {
                query = query.Where(s => s.Material.Shape == parsedShape.Value);
            }
            else
            {
                var shapeLower = shape.ToLower();
                query = query.Where(s => s.Material.Shape.ToString().ToLower().Contains(shapeLower));
            }
        }

        var items = await query
            .OrderBy(s => s.Material.Shape)
            .ThenBy(s => s.Material.Size)
            .ThenBy(s => s.LengthInches)
            .ToListAsync();

        return new StockItemListResult
        {
            Success = true,
            StockItems = items.Select(s => new StockItemDto
            {
                Id = s.Id,
                MaterialId = s.MaterialId,
                MaterialName = s.Material.DisplayName,
                LengthInches = s.LengthInches,
                LengthFormatted = ArchUnits.FormatFromInches((double)s.LengthInches),
                Name = s.Name,
                QuantityOnHand = s.QuantityOnHand,
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
        [Description("Initial quantity on hand (default 0)")]
        int quantityOnHand = 0,
        [Description("Notes")]
        string? notes = null)
    {
        var material = await _context.Materials.FindAsync(materialId);
        if (material == null)
        {
            return new StockItemResult
            {
                Success = false,
                Error = $"Material with ID {materialId} not found"
            };
        }

        // Parse length
        double lengthInches;
        try
        {
            lengthInches = double.TryParse(length.Trim(), out var plain)
                ? plain
                : ArchUnits.ParseToInches(length);
        }
        catch
        {
            return new StockItemResult
            {
                Success = false,
                Error = $"Could not parse length: {length}"
            };
        }

        // Check for duplicate
        var existing = await _context.StockItems
            .FirstOrDefaultAsync(s => s.MaterialId == materialId && s.LengthInches == (decimal)lengthInches && s.IsActive);

        if (existing != null)
        {
            return new StockItemResult
            {
                Success = false,
                Error = $"Stock item for {material.DisplayName} at {ArchUnits.FormatFromInches(lengthInches)} already exists with ID {existing.Id}"
            };
        }

        var stockItem = new StockItem
        {
            MaterialId = materialId,
            LengthInches = (decimal)lengthInches,
            Name = name,
            QuantityOnHand = quantityOnHand,
            Notes = notes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.StockItems.Add(stockItem);
        await _context.SaveChangesAsync();

        return new StockItemResult
        {
            Success = true,
            StockItem = new StockItemDto
            {
                Id = stockItem.Id,
                MaterialId = stockItem.MaterialId,
                MaterialName = material.DisplayName,
                LengthInches = stockItem.LengthInches,
                LengthFormatted = ArchUnits.FormatFromInches((double)stockItem.LengthInches),
                Name = stockItem.Name,
                QuantityOnHand = stockItem.QuantityOnHand,
                Notes = stockItem.Notes,
                IsActive = stockItem.IsActive
            }
        };
    }

    #endregion

    #region Supplier Offerings

    [McpServerTool(Name = "list_supplier_offerings"), Description("Lists supplier offerings (what suppliers sell for each stock item).")]
    public async Task<SupplierOfferingListResult> ListSupplierOfferings(
        [Description("Filter by supplier ID")]
        int? supplierId = null,
        [Description("Filter by stock item ID")]
        int? stockItemId = null,
        [Description("Filter by material ID")]
        int? materialId = null)
    {
        var query = _context.SupplierOfferings
            .Include(o => o.Supplier)
            .Include(o => o.StockItem)
                .ThenInclude(s => s.Material)
            .AsQueryable();

        if (supplierId.HasValue)
            query = query.Where(o => o.SupplierId == supplierId.Value);

        if (stockItemId.HasValue)
            query = query.Where(o => o.StockItemId == stockItemId.Value);

        if (materialId.HasValue)
            query = query.Where(o => o.StockItem.MaterialId == materialId.Value);

        var offerings = await query
            .OrderBy(o => o.Supplier.Name)
            .ThenBy(o => o.StockItem.Material.Shape)
            .ThenBy(o => o.StockItem.Material.Size)
            .ThenBy(o => o.StockItem.LengthInches)
            .ToListAsync();

        return new SupplierOfferingListResult
        {
            Success = true,
            Offerings = offerings.Select(o => new SupplierOfferingDto
            {
                Id = o.Id,
                SupplierId = o.SupplierId,
                SupplierName = o.Supplier.Name,
                StockItemId = o.StockItemId,
                MaterialName = o.StockItem.Material.DisplayName,
                LengthFormatted = ArchUnits.FormatFromInches((double)o.StockItem.LengthInches),
                PartNumber = o.PartNumber,
                SupplierDescription = o.SupplierDescription,
                Price = o.Price,
                                Notes = o.Notes
            }).ToList()
        };
    }

    [McpServerTool(Name = "add_supplier_offering"), Description("Adds a supplier offering - links a supplier to a stock item with their part number and pricing.")]
    public async Task<SupplierOfferingResult> AddSupplierOffering(
        [Description("Supplier ID (use list_suppliers to find)")]
        int supplierId,
        [Description("Stock item ID (use list_stock_items to find)")]
        int stockItemId,
        [Description("Supplier's part number")]
        string? partNumber = null,
        [Description("Supplier's description of the item")]
        string? supplierDescription = null,
        [Description("Price per unit")]
        decimal? price = null,
        [Description("Notes")]
        string? notes = null)
    {
        var supplier = await _context.Suppliers.FindAsync(supplierId);
        if (supplier == null)
        {
            return new SupplierOfferingResult
            {
                Success = false,
                Error = $"Supplier with ID {supplierId} not found"
            };
        }

        var stockItem = await _context.StockItems
            .Include(s => s.Material)
            .FirstOrDefaultAsync(s => s.Id == stockItemId);

        if (stockItem == null)
        {
            return new SupplierOfferingResult
            {
                Success = false,
                Error = $"Stock item with ID {stockItemId} not found"
            };
        }

        // Check for duplicate
        var existing = await _context.SupplierOfferings
            .FirstOrDefaultAsync(o => o.SupplierId == supplierId && o.StockItemId == stockItemId);

        if (existing != null)
        {
            return new SupplierOfferingResult
            {
                Success = false,
                Error = $"Offering from {supplier.Name} for this stock item already exists with ID {existing.Id}"
            };
        }

        var offering = new SupplierOffering
        {
            SupplierId = supplierId,
            StockItemId = stockItemId,
            PartNumber = partNumber,
            SupplierDescription = supplierDescription,
            Price = price,
            Notes = notes
        };

        _context.SupplierOfferings.Add(offering);
        await _context.SaveChangesAsync();

        return new SupplierOfferingResult
        {
            Success = true,
            Offering = new SupplierOfferingDto
            {
                Id = offering.Id,
                SupplierId = offering.SupplierId,
                SupplierName = supplier.Name,
                StockItemId = offering.StockItemId,
                MaterialName = stockItem.Material.DisplayName,
                LengthFormatted = ArchUnits.FormatFromInches((double)stockItem.LengthInches),
                PartNumber = offering.PartNumber,
                SupplierDescription = offering.SupplierDescription,
                Price = offering.Price,
                                Notes = offering.Notes
            }
        };
    }

    [McpServerTool(Name = "add_stock_with_offering"), Description("Convenience method: adds a material (if needed), stock item (if needed), and supplier offering all at once.")]
    public async Task<AddStockWithOfferingResult> AddStockWithOffering(
        [Description("Supplier ID (use list_suppliers or add_supplier first)")]
        int supplierId,
        [Description("Material shape (e.g., 'Angle', 'FlatBar')")]
        string shape,
        [Description("Material size (e.g., '2 x 2 x 1/4')")]
        string size,
        [Description("Stock length (e.g., '20'', '240')")]
        string length,
        [Description("Material type: Steel, Aluminum, Stainless, Brass, Copper (default: Steel)")]
        string type = "Steel",
        [Description("Grade or specification (e.g., 'A36', 'Hot Roll', '304', '6061-T6')")]
        string? grade = null,
        [Description("Supplier's part number")]
        string? partNumber = null,
        [Description("Supplier's description")]
        string? supplierDescription = null,
        [Description("Price per unit")]
        decimal? price = null)
    {
        var supplier = await _context.Suppliers.FindAsync(supplierId);
        if (supplier == null)
        {
            return new AddStockWithOfferingResult
            {
                Success = false,
                Error = $"Supplier with ID {supplierId} not found"
            };
        }

        // Parse shape
        var parsedShape = MaterialShapeExtensions.ParseShape(shape);
        if (!parsedShape.HasValue)
        {
            return new AddStockWithOfferingResult
            {
                Success = false,
                Error = $"Unknown shape: {shape}. Valid shapes are: RoundBar, RoundTube, FlatBar, SquareBar, SquareTube, RectangularTube, Angle, Channel, IBeam, Pipe"
            };
        }

        // Parse material type (default to Steel if not provided)
        var typeValue = string.IsNullOrWhiteSpace(type) ? "Steel" : type;
        if (!Enum.TryParse<MaterialType>(typeValue, ignoreCase: true, out var parsedType))
        {
            return new AddStockWithOfferingResult
            {
                Success = false,
                Error = $"Unknown material type: {typeValue}. Valid types are: Steel, Aluminum, Stainless, Brass, Copper"
            };
        }

        // Parse length
        double lengthInches;
        try
        {
            lengthInches = double.TryParse(length.Trim(), out var plain)
                ? plain
                : ArchUnits.ParseToInches(length);
        }
        catch
        {
            return new AddStockWithOfferingResult
            {
                Success = false,
                Error = $"Could not parse length: {length}"
            };
        }

        // Find or create material (match on shape, type, grade, and size)
        var material = await _context.Materials
            .Include(m => m.Dimensions)
            .FirstOrDefaultAsync(m => m.Shape == parsedShape.Value
                && m.Type == parsedType
                && m.Grade == grade
                && m.Size.ToLower() == size.ToLower());

        bool materialCreated = false;
        if (material == null)
        {
            // Parse dimensions from size string
            var (dimensions, sortOrder) = ParseSizeStringToDimensions(parsedShape.Value, size);

            material = new Material
            {
                Shape = parsedShape.Value,
                Type = parsedType,
                Grade = grade,
                Size = size,
                SortOrder = sortOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Materials.Add(material);
            await _context.SaveChangesAsync();

            // Add dimensions if parsed successfully
            if (dimensions != null)
            {
                dimensions.MaterialId = material.Id;
                _context.MaterialDimensions.Add(dimensions);
                await _context.SaveChangesAsync();
            }

            materialCreated = true;
        }

        // Find or create stock item
        var stockItem = await _context.StockItems
            .FirstOrDefaultAsync(s => s.MaterialId == material.Id && s.LengthInches == (decimal)lengthInches && s.IsActive);

        bool stockItemCreated = false;
        if (stockItem == null)
        {
            stockItem = new StockItem
            {
                MaterialId = material.Id,
                LengthInches = (decimal)lengthInches,
                QuantityOnHand = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.StockItems.Add(stockItem);
            await _context.SaveChangesAsync();
            stockItemCreated = true;
        }

        // Check if offering already exists
        var existingOffering = await _context.SupplierOfferings
            .FirstOrDefaultAsync(o => o.SupplierId == supplierId && o.StockItemId == stockItem.Id);

        if (existingOffering != null)
        {
            return new AddStockWithOfferingResult
            {
                Success = false,
                Error = $"Offering from {supplier.Name} for {material.DisplayName} at {ArchUnits.FormatFromInches(lengthInches)} already exists",
                MaterialCreated = materialCreated,
                StockItemCreated = stockItemCreated
            };
        }

        // Create offering
        var offering = new SupplierOffering
        {
            SupplierId = supplierId,
            StockItemId = stockItem.Id,
            PartNumber = partNumber,
            SupplierDescription = supplierDescription,
            Price = price
        };

        _context.SupplierOfferings.Add(offering);
        await _context.SaveChangesAsync();

        return new AddStockWithOfferingResult
        {
            Success = true,
            MaterialId = material.Id,
            MaterialName = material.DisplayName,
            MaterialCreated = materialCreated,
            StockItemId = stockItem.Id,
            StockItemCreated = stockItemCreated,
            LengthFormatted = ArchUnits.FormatFromInches(lengthInches),
            OfferingId = offering.Id,
            PartNumber = partNumber,
            SupplierDescription = supplierDescription,
            Price = price
        };
    }

    #endregion
}

#region DTOs

public class SupplierDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactInfo { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public class SupplierListResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<SupplierDto> Suppliers { get; set; } = new();
}

public class SupplierResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public SupplierDto? Supplier { get; set; }
}

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
    public int QuantityOnHand { get; set; }
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

public class SupplierOfferingDto
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int StockItemId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string LengthFormatted { get; set; } = string.Empty;
    public string? PartNumber { get; set; }
    public string? SupplierDescription { get; set; }
    public decimal? Price { get; set; }
    public string? Notes { get; set; }
}

public class SupplierOfferingListResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<SupplierOfferingDto> Offerings { get; set; } = new();
}

public class SupplierOfferingResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public SupplierOfferingDto? Offering { get; set; }
}

public class AddStockWithOfferingResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public bool MaterialCreated { get; set; }
    public int StockItemId { get; set; }
    public bool StockItemCreated { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public int OfferingId { get; set; }
    public string? PartNumber { get; set; }
    public string? SupplierDescription { get; set; }
    public decimal? Price { get; set; }
}

#endregion
