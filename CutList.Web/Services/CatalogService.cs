using CutList.Web.Data;
using CutList.Web.Data.Entities;
using CutList.Web.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class CatalogService
{
    private readonly ApplicationDbContext _context;
    private readonly MaterialService _materialService;

    public CatalogService(ApplicationDbContext context, MaterialService materialService)
    {
        _context = context;
        _materialService = materialService;
    }

    public async Task<CatalogData> ExportAsync()
    {
        var suppliers = await _context.Suppliers
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .AsNoTracking()
            .ToListAsync();

        var cuttingTools = await _context.CuttingTools
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .AsNoTracking()
            .ToListAsync();

        var materials = await _context.Materials
            .Include(m => m.Dimensions)
            .Include(m => m.StockItems.Where(s => s.IsActive))
                .ThenInclude(s => s.SupplierOfferings.Where(o => o.IsActive))
            .Where(m => m.IsActive)
            .OrderBy(m => m.Shape).ThenBy(m => m.SortOrder)
            .AsNoTracking()
            .ToListAsync();

        return new CatalogData
        {
            ExportedAt = DateTime.UtcNow,
            Suppliers = suppliers.Select(s => new CatalogSupplierDto
            {
                Name = s.Name,
                ContactInfo = s.ContactInfo,
                Notes = s.Notes
            }).ToList(),
            CuttingTools = cuttingTools.Select(t => new CatalogCuttingToolDto
            {
                Name = t.Name,
                KerfInches = t.KerfInches,
                IsDefault = t.IsDefault
            }).ToList(),
            Materials = materials.Select(m => new CatalogMaterialDto
            {
                Shape = m.Shape.ToString(),
                Type = m.Type.ToString(),
                Grade = m.Grade,
                Size = m.Size,
                Description = m.Description,
                Dimensions = MapDimensions(m.Dimensions),
                StockItems = m.StockItems.OrderBy(s => s.LengthInches).Select(s => new CatalogStockItemDto
                {
                    LengthInches = s.LengthInches,
                    Name = s.Name,
                    QuantityOnHand = s.QuantityOnHand,
                    Notes = s.Notes,
                    SupplierOfferings = s.SupplierOfferings.Select(o => new CatalogSupplierOfferingDto
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
    }

    public async Task<ImportResultDto> ImportAsync(CatalogData data)
    {
        var result = new ImportResultDto();

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 1. Suppliers — upsert by name
            var supplierMap = await ImportSuppliersAsync(data.Suppliers, result);

            // 2. Cutting tools — upsert by name
            await ImportCuttingToolsAsync(data.CuttingTools, result);

            // 3. Materials + stock items + offerings
            await ImportMaterialsAsync(data.Materials, supplierMap, result);

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            result.Errors.Add($"Transaction failed: {ex.Message}");
        }

        return result;
    }

    private async Task<Dictionary<string, int>> ImportSuppliersAsync(
        List<CatalogSupplierDto> suppliers, ImportResultDto result)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var existingSuppliers = await _context.Suppliers.ToListAsync();

        foreach (var dto in suppliers)
        {
            try
            {
                var existing = existingSuppliers.FirstOrDefault(
                    s => s.Name.Equals(dto.Name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    existing.ContactInfo = dto.ContactInfo ?? existing.ContactInfo;
                    existing.Notes = dto.Notes ?? existing.Notes;
                    existing.IsActive = true;
                    map[dto.Name] = existing.Id;
                    result.SuppliersUpdated++;
                }
                else
                {
                    var supplier = new Supplier
                    {
                        Name = dto.Name,
                        ContactInfo = dto.ContactInfo,
                        Notes = dto.Notes,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Suppliers.Add(supplier);
                    await _context.SaveChangesAsync();
                    existingSuppliers.Add(supplier);
                    map[dto.Name] = supplier.Id;
                    result.SuppliersCreated++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Supplier '{dto.Name}': {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        return map;
    }

    private async Task ImportCuttingToolsAsync(
        List<CatalogCuttingToolDto> tools, ImportResultDto result)
    {
        var existingTools = await _context.CuttingTools.ToListAsync();

        foreach (var dto in tools)
        {
            try
            {
                var existing = existingTools.FirstOrDefault(
                    t => t.Name.Equals(dto.Name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    existing.KerfInches = dto.KerfInches;
                    existing.IsActive = true;
                    // Skip IsDefault changes to avoid conflicts
                    result.CuttingToolsUpdated++;
                }
                else
                {
                    var tool = new CuttingTool
                    {
                        Name = dto.Name,
                        KerfInches = dto.KerfInches,
                        IsDefault = false // Never import as default to avoid conflicts
                    };
                    _context.CuttingTools.Add(tool);
                    existingTools.Add(tool);
                    result.CuttingToolsCreated++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Cutting tool '{dto.Name}': {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task ImportMaterialsAsync(
        List<CatalogMaterialDto> materials, Dictionary<string, int> supplierMap, ImportResultDto result)
    {
        // Pre-load existing materials with their dimensions
        var existingMaterials = await _context.Materials
            .Include(m => m.Dimensions)
            .Include(m => m.StockItems)
                .ThenInclude(s => s.SupplierOfferings)
            .ToListAsync();

        foreach (var dto in materials)
        {
            try
            {
                if (!Enum.TryParse<MaterialShape>(dto.Shape, ignoreCase: true, out var shape))
                {
                    result.Errors.Add($"Material '{dto.Shape} - {dto.Size}': Unknown shape '{dto.Shape}'");
                    continue;
                }

                if (!Enum.TryParse<MaterialType>(dto.Type, ignoreCase: true, out var type))
                {
                    type = MaterialType.Steel; // Default
                    result.Warnings.Add($"Material '{dto.Shape} - {dto.Size}': Unknown type '{dto.Type}', defaulting to Steel");
                }

                var existing = existingMaterials.FirstOrDefault(
                    m => m.Shape == shape && m.Size.Equals(dto.Size, StringComparison.OrdinalIgnoreCase));

                Material material;

                if (existing != null)
                {
                    // Update existing material
                    existing.Type = type;
                    existing.Grade = dto.Grade ?? existing.Grade;
                    existing.Description = dto.Description ?? existing.Description;
                    existing.IsActive = true;
                    existing.UpdatedAt = DateTime.UtcNow;

                    // Update dimensions if provided
                    if (dto.Dimensions != null && existing.Dimensions != null)
                    {
                        ApplyDimensionValues(existing.Dimensions, dto.Dimensions);
                        existing.SortOrder = existing.Dimensions.GetSortOrder();
                    }

                    material = existing;
                    result.MaterialsUpdated++;
                }
                else
                {
                    // Create new material with dimensions
                    material = new Material
                    {
                        Shape = shape,
                        Type = type,
                        Grade = dto.Grade,
                        Size = dto.Size,
                        Description = dto.Description,
                        CreatedAt = DateTime.UtcNow
                    };

                    if (dto.Dimensions != null)
                    {
                        var dimensions = MaterialService.CreateDimensionsForShape(shape);
                        ApplyDimensionValues(dimensions, dto.Dimensions);
                        material = await _materialService.CreateWithDimensionsAsync(material, dimensions);
                    }
                    else
                    {
                        _context.Materials.Add(material);
                        await _context.SaveChangesAsync();
                    }

                    existingMaterials.Add(material);
                    result.MaterialsCreated++;
                }

                await _context.SaveChangesAsync();

                // Import stock items for this material
                await ImportStockItemsAsync(material, dto.StockItems, supplierMap, result);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Material '{dto.Shape} - {dto.Size}': {ex.Message}");
            }
        }
    }

    private async Task ImportStockItemsAsync(
        Material material, List<CatalogStockItemDto> stockItems,
        Dictionary<string, int> supplierMap, ImportResultDto result)
    {
        // Reload stock items for this material to ensure we have current state
        var existingStockItems = await _context.StockItems
            .Include(s => s.SupplierOfferings)
            .Where(s => s.MaterialId == material.Id)
            .ToListAsync();

        foreach (var dto in stockItems)
        {
            try
            {
                var existing = existingStockItems.FirstOrDefault(
                    s => s.LengthInches == dto.LengthInches);

                StockItem stockItem;

                if (existing != null)
                {
                    existing.Name = dto.Name ?? existing.Name;
                    existing.Notes = dto.Notes ?? existing.Notes;
                    existing.IsActive = true;
                    existing.UpdatedAt = DateTime.UtcNow;
                    // Don't overwrite QuantityOnHand — preserve actual inventory
                    stockItem = existing;
                    result.StockItemsUpdated++;
                }
                else
                {
                    stockItem = new StockItem
                    {
                        MaterialId = material.Id,
                        LengthInches = dto.LengthInches,
                        Name = dto.Name,
                        QuantityOnHand = dto.QuantityOnHand,
                        Notes = dto.Notes,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.StockItems.Add(stockItem);
                    await _context.SaveChangesAsync();
                    existingStockItems.Add(stockItem);
                    result.StockItemsCreated++;
                }

                // Import supplier offerings
                foreach (var offeringDto in dto.SupplierOfferings)
                {
                    try
                    {
                        if (!supplierMap.TryGetValue(offeringDto.SupplierName, out var supplierId))
                        {
                            result.Warnings.Add(
                                $"Offering for stock '{material.DisplayName} @ {dto.LengthInches}\"': " +
                                $"Unknown supplier '{offeringDto.SupplierName}', skipped");
                            continue;
                        }

                        var existingOffering = stockItem.SupplierOfferings.FirstOrDefault(
                            o => o.SupplierId == supplierId);

                        if (existingOffering != null)
                        {
                            existingOffering.PartNumber = offeringDto.PartNumber ?? existingOffering.PartNumber;
                            existingOffering.SupplierDescription = offeringDto.SupplierDescription ?? existingOffering.SupplierDescription;
                            existingOffering.Price = offeringDto.Price ?? existingOffering.Price;
                            existingOffering.Notes = offeringDto.Notes ?? existingOffering.Notes;
                            existingOffering.IsActive = true;
                            result.OfferingsUpdated++;
                        }
                        else
                        {
                            var offering = new SupplierOffering
                            {
                                StockItemId = stockItem.Id,
                                SupplierId = supplierId,
                                PartNumber = offeringDto.PartNumber,
                                SupplierDescription = offeringDto.SupplierDescription,
                                Price = offeringDto.Price,
                                Notes = offeringDto.Notes
                            };
                            _context.SupplierOfferings.Add(offering);
                            stockItem.SupplierOfferings.Add(offering);
                            result.OfferingsCreated++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add(
                            $"Offering for '{material.DisplayName} @ {dto.LengthInches}\"' " +
                            $"from '{offeringDto.SupplierName}': {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                result.Errors.Add(
                    $"Stock item '{material.DisplayName} @ {dto.LengthInches}\"': {ex.Message}");
            }
        }
    }

    private static CatalogDimensionsDto? MapDimensions(MaterialDimensions? dim) => dim switch
    {
        RoundBarDimensions d => new CatalogDimensionsDto { Diameter = d.Diameter },
        RoundTubeDimensions d => new CatalogDimensionsDto { OuterDiameter = d.OuterDiameter, Wall = d.Wall },
        FlatBarDimensions d => new CatalogDimensionsDto { Width = d.Width, Thickness = d.Thickness },
        SquareBarDimensions d => new CatalogDimensionsDto { Size = d.Size },
        SquareTubeDimensions d => new CatalogDimensionsDto { Size = d.Size, Wall = d.Wall },
        RectangularTubeDimensions d => new CatalogDimensionsDto { Width = d.Width, Height = d.Height, Wall = d.Wall },
        AngleDimensions d => new CatalogDimensionsDto { Leg1 = d.Leg1, Leg2 = d.Leg2, Thickness = d.Thickness },
        ChannelDimensions d => new CatalogDimensionsDto { Height = d.Height, Flange = d.Flange, Web = d.Web },
        IBeamDimensions d => new CatalogDimensionsDto { Height = d.Height, WeightPerFoot = d.WeightPerFoot },
        PipeDimensions d => new CatalogDimensionsDto { NominalSize = d.NominalSize, Wall = d.Wall, Schedule = d.Schedule },
        _ => null
    };

    private static void ApplyDimensionValues(MaterialDimensions dimensions, CatalogDimensionsDto dto)
    {
        switch (dimensions)
        {
            case RoundBarDimensions rb:
                if (dto.Diameter.HasValue) rb.Diameter = dto.Diameter.Value;
                break;
            case RoundTubeDimensions rt:
                if (dto.OuterDiameter.HasValue) rt.OuterDiameter = dto.OuterDiameter.Value;
                if (dto.Wall.HasValue) rt.Wall = dto.Wall.Value;
                break;
            case FlatBarDimensions fb:
                if (dto.Width.HasValue) fb.Width = dto.Width.Value;
                if (dto.Thickness.HasValue) fb.Thickness = dto.Thickness.Value;
                break;
            case SquareBarDimensions sb:
                if (dto.Size.HasValue) sb.Size = dto.Size.Value;
                break;
            case SquareTubeDimensions st:
                if (dto.Size.HasValue) st.Size = dto.Size.Value;
                if (dto.Wall.HasValue) st.Wall = dto.Wall.Value;
                break;
            case RectangularTubeDimensions rect:
                if (dto.Width.HasValue) rect.Width = dto.Width.Value;
                if (dto.Height.HasValue) rect.Height = dto.Height.Value;
                if (dto.Wall.HasValue) rect.Wall = dto.Wall.Value;
                break;
            case AngleDimensions a:
                if (dto.Leg1.HasValue) a.Leg1 = dto.Leg1.Value;
                if (dto.Leg2.HasValue) a.Leg2 = dto.Leg2.Value;
                if (dto.Thickness.HasValue) a.Thickness = dto.Thickness.Value;
                break;
            case ChannelDimensions c:
                if (dto.Height.HasValue) c.Height = dto.Height.Value;
                if (dto.Flange.HasValue) c.Flange = dto.Flange.Value;
                if (dto.Web.HasValue) c.Web = dto.Web.Value;
                break;
            case IBeamDimensions ib:
                if (dto.Height.HasValue) ib.Height = dto.Height.Value;
                if (dto.WeightPerFoot.HasValue) ib.WeightPerFoot = dto.WeightPerFoot.Value;
                break;
            case PipeDimensions p:
                if (dto.NominalSize.HasValue) p.NominalSize = dto.NominalSize.Value;
                if (dto.Wall.HasValue) p.Wall = dto.Wall.Value;
                if (dto.Schedule != null) p.Schedule = dto.Schedule;
                break;
        }
    }
}
