using CutList.Web.Data;
using CutList.Web.Data.Entities;
using CutList.Web.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class CatalogService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    private readonly MaterialService _materialService;

    public CatalogService(IDbContextFactory<ApplicationDbContext> factory, MaterialService materialService)
    {
        _factory = factory;
        _materialService = materialService;
    }

    public async Task<CatalogData> ExportAsync()
    {
        await using var context = _factory.CreateDbContext();

        var suppliers = await context.Suppliers
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .AsNoTracking()
            .ToListAsync();

        var cuttingTools = await context.CuttingTools
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .AsNoTracking()
            .ToListAsync();

        var materials = await context.Materials
            .Include(m => m.Dimensions)
            .Include(m => m.StockItems.Where(s => s.IsActive))
                .ThenInclude(s => s.SupplierOfferings.Where(o => o.IsActive))
            .Where(m => m.IsActive)
            .OrderBy(m => m.Shape).ThenBy(m => m.SortOrder)
            .AsNoTracking()
            .ToListAsync();

        var grouped = materials.GroupBy(m => m.Shape);
        var materialsDto = new CatalogMaterialsDto();

        foreach (var group in grouped)
        {
            foreach (var m in group)
            {
                var stockItems = MapStockItems(m, suppliers);

                switch (m.Shape)
                {
                    case MaterialShape.Angle when m.Dimensions is AngleDimensions d:
                        materialsDto.Angles.Add(new CatalogAngleDto
                        {
                            Type = m.Type.ToString(), Grade = m.Grade, Size = m.Size, Description = m.Description,
                            Leg1 = d.Leg1, Leg2 = d.Leg2, Thickness = d.Thickness,
                            StockItems = stockItems
                        });
                        break;
                    case MaterialShape.Channel when m.Dimensions is ChannelDimensions d:
                        materialsDto.Channels.Add(new CatalogChannelDto
                        {
                            Type = m.Type.ToString(), Grade = m.Grade, Size = m.Size, Description = m.Description,
                            Height = d.Height, Flange = d.Flange, Web = d.Web,
                            StockItems = stockItems
                        });
                        break;
                    case MaterialShape.FlatBar when m.Dimensions is FlatBarDimensions d:
                        materialsDto.FlatBars.Add(new CatalogFlatBarDto
                        {
                            Type = m.Type.ToString(), Grade = m.Grade, Size = m.Size, Description = m.Description,
                            Width = d.Width, Thickness = d.Thickness,
                            StockItems = stockItems
                        });
                        break;
                    case MaterialShape.IBeam when m.Dimensions is IBeamDimensions d:
                        materialsDto.IBeams.Add(new CatalogIBeamDto
                        {
                            Type = m.Type.ToString(), Grade = m.Grade, Size = m.Size, Description = m.Description,
                            Height = d.Height, WeightPerFoot = d.WeightPerFoot,
                            StockItems = stockItems
                        });
                        break;
                    case MaterialShape.Pipe when m.Dimensions is PipeDimensions d:
                        materialsDto.Pipes.Add(new CatalogPipeDto
                        {
                            Type = m.Type.ToString(), Grade = m.Grade, Size = m.Size, Description = m.Description,
                            NominalSize = d.NominalSize, Wall = d.Wall ?? 0, Schedule = d.Schedule,
                            StockItems = stockItems
                        });
                        break;
                    case MaterialShape.RectangularTube when m.Dimensions is RectangularTubeDimensions d:
                        materialsDto.RectangularTubes.Add(new CatalogRectangularTubeDto
                        {
                            Type = m.Type.ToString(), Grade = m.Grade, Size = m.Size, Description = m.Description,
                            Width = d.Width, Height = d.Height, Wall = d.Wall,
                            StockItems = stockItems
                        });
                        break;
                    case MaterialShape.RoundBar when m.Dimensions is RoundBarDimensions d:
                        materialsDto.RoundBars.Add(new CatalogRoundBarDto
                        {
                            Type = m.Type.ToString(), Grade = m.Grade, Size = m.Size, Description = m.Description,
                            Diameter = d.Diameter,
                            StockItems = stockItems
                        });
                        break;
                    case MaterialShape.RoundTube when m.Dimensions is RoundTubeDimensions d:
                        materialsDto.RoundTubes.Add(new CatalogRoundTubeDto
                        {
                            Type = m.Type.ToString(), Grade = m.Grade, Size = m.Size, Description = m.Description,
                            OuterDiameter = d.OuterDiameter, Wall = d.Wall,
                            StockItems = stockItems
                        });
                        break;
                    case MaterialShape.SquareBar when m.Dimensions is SquareBarDimensions d:
                        materialsDto.SquareBars.Add(new CatalogSquareBarDto
                        {
                            Type = m.Type.ToString(), Grade = m.Grade, Size = m.Size, Description = m.Description,
                            SideLength = d.Size,
                            StockItems = stockItems
                        });
                        break;
                    case MaterialShape.SquareTube when m.Dimensions is SquareTubeDimensions d:
                        materialsDto.SquareTubes.Add(new CatalogSquareTubeDto
                        {
                            Type = m.Type.ToString(), Grade = m.Grade, Size = m.Size, Description = m.Description,
                            SideLength = d.Size, Wall = d.Wall,
                            StockItems = stockItems
                        });
                        break;
                }
            }
        }

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
            Materials = materialsDto
        };
    }

    public async Task<ImportResultDto> ImportAsync(CatalogData data)
    {
        var result = new ImportResultDto();

        await using var context = _factory.CreateDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            // 1. Suppliers - upsert by name
            var supplierMap = await ImportSuppliersAsync(context, data.Suppliers, result);

            // 2. Cutting tools - upsert by name
            await ImportCuttingToolsAsync(context, data.CuttingTools, result);

            // 3. Materials + stock items + offerings
            await ImportAllMaterialsAsync(context, data.Materials, supplierMap, result);

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
        ApplicationDbContext context, List<CatalogSupplierDto> suppliers, ImportResultDto result)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var existingSuppliers = await context.Suppliers.ToListAsync();

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
                    context.Suppliers.Add(supplier);
                    await context.SaveChangesAsync();
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

        await context.SaveChangesAsync();
        return map;
    }

    private async Task ImportCuttingToolsAsync(
        ApplicationDbContext context, List<CatalogCuttingToolDto> tools, ImportResultDto result)
    {
        var existingTools = await context.CuttingTools.ToListAsync();

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
                    result.CuttingToolsUpdated++;
                }
                else
                {
                    var tool = new CuttingTool
                    {
                        Name = dto.Name,
                        KerfInches = dto.KerfInches,
                        IsDefault = false
                    };
                    context.CuttingTools.Add(tool);
                    existingTools.Add(tool);
                    result.CuttingToolsCreated++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Cutting tool '{dto.Name}': {ex.Message}");
            }
        }

        await context.SaveChangesAsync();
    }

    private async Task ImportAllMaterialsAsync(
        ApplicationDbContext context, CatalogMaterialsDto materials, Dictionary<string, int> supplierMap, ImportResultDto result)
    {
        var existingMaterials = await context.Materials
            .Include(m => m.Dimensions)
            .Include(m => m.StockItems)
                .ThenInclude(s => s.SupplierOfferings)
            .ToListAsync();

        foreach (var dto in materials.Angles)
            await ImportMaterialAsync(context, dto, MaterialShape.Angle, existingMaterials, supplierMap, result,
                () => new AngleDimensions { Leg1 = dto.Leg1, Leg2 = dto.Leg2, Thickness = dto.Thickness },
                dim => { var d = (AngleDimensions)dim; d.Leg1 = dto.Leg1; d.Leg2 = dto.Leg2; d.Thickness = dto.Thickness; });

        foreach (var dto in materials.Channels)
            await ImportMaterialAsync(context, dto, MaterialShape.Channel, existingMaterials, supplierMap, result,
                () => new ChannelDimensions { Height = dto.Height, Flange = dto.Flange, Web = dto.Web },
                dim => { var d = (ChannelDimensions)dim; d.Height = dto.Height; d.Flange = dto.Flange; d.Web = dto.Web; });

        foreach (var dto in materials.FlatBars)
            await ImportMaterialAsync(context, dto, MaterialShape.FlatBar, existingMaterials, supplierMap, result,
                () => new FlatBarDimensions { Width = dto.Width, Thickness = dto.Thickness },
                dim => { var d = (FlatBarDimensions)dim; d.Width = dto.Width; d.Thickness = dto.Thickness; });

        foreach (var dto in materials.IBeams)
            await ImportMaterialAsync(context, dto, MaterialShape.IBeam, existingMaterials, supplierMap, result,
                () => new IBeamDimensions { Height = dto.Height, WeightPerFoot = dto.WeightPerFoot },
                dim => { var d = (IBeamDimensions)dim; d.Height = dto.Height; d.WeightPerFoot = dto.WeightPerFoot; });

        foreach (var dto in materials.Pipes)
            await ImportMaterialAsync(context, dto, MaterialShape.Pipe, existingMaterials, supplierMap, result,
                () => new PipeDimensions { NominalSize = dto.NominalSize, Wall = dto.Wall, Schedule = dto.Schedule },
                dim => { var d = (PipeDimensions)dim; d.NominalSize = dto.NominalSize; d.Wall = (decimal?)dto.Wall; d.Schedule = dto.Schedule; });

        foreach (var dto in materials.RectangularTubes)
            await ImportMaterialAsync(context, dto, MaterialShape.RectangularTube, existingMaterials, supplierMap, result,
                () => new RectangularTubeDimensions { Width = dto.Width, Height = dto.Height, Wall = dto.Wall },
                dim => { var d = (RectangularTubeDimensions)dim; d.Width = dto.Width; d.Height = dto.Height; d.Wall = dto.Wall; });

        foreach (var dto in materials.RoundBars)
            await ImportMaterialAsync(context, dto, MaterialShape.RoundBar, existingMaterials, supplierMap, result,
                () => new RoundBarDimensions { Diameter = dto.Diameter },
                dim => { var d = (RoundBarDimensions)dim; d.Diameter = dto.Diameter; });

        foreach (var dto in materials.RoundTubes)
            await ImportMaterialAsync(context, dto, MaterialShape.RoundTube, existingMaterials, supplierMap, result,
                () => new RoundTubeDimensions { OuterDiameter = dto.OuterDiameter, Wall = dto.Wall },
                dim => { var d = (RoundTubeDimensions)dim; d.OuterDiameter = dto.OuterDiameter; d.Wall = dto.Wall; });

        foreach (var dto in materials.SquareBars)
            await ImportMaterialAsync(context, dto, MaterialShape.SquareBar, existingMaterials, supplierMap, result,
                () => new SquareBarDimensions { Size = dto.SideLength },
                dim => { var d = (SquareBarDimensions)dim; d.Size = dto.SideLength; });

        foreach (var dto in materials.SquareTubes)
            await ImportMaterialAsync(context, dto, MaterialShape.SquareTube, existingMaterials, supplierMap, result,
                () => new SquareTubeDimensions { Size = dto.SideLength, Wall = dto.Wall },
                dim => { var d = (SquareTubeDimensions)dim; d.Size = dto.SideLength; d.Wall = dto.Wall; });
    }

    private async Task ImportMaterialAsync(
        ApplicationDbContext context, CatalogMaterialBaseDto dto, MaterialShape shape,
        List<Material> existingMaterials, Dictionary<string, int> supplierMap,
        ImportResultDto result,
        Func<MaterialDimensions> createDimensions,
        Action<MaterialDimensions> updateDimensions)
    {
        try
        {
            if (!Enum.TryParse<MaterialType>(dto.Type, ignoreCase: true, out var type))
            {
                type = MaterialType.Steel;
                result.Warnings.Add($"Material '{shape} - {dto.Size}': Unknown type '{dto.Type}', defaulting to Steel");
            }

            var existing = existingMaterials.FirstOrDefault(
                m => m.Shape == shape && m.Size.Equals(dto.Size, StringComparison.OrdinalIgnoreCase));

            Material material;

            if (existing != null)
            {
                existing.Type = type;
                existing.Grade = dto.Grade ?? existing.Grade;
                existing.Description = dto.Description ?? existing.Description;
                existing.IsActive = true;
                existing.UpdatedAt = DateTime.UtcNow;

                if (existing.Dimensions != null)
                {
                    updateDimensions(existing.Dimensions);
                    existing.SortOrder = existing.Dimensions.GetSortOrder();
                }

                material = existing;
                result.MaterialsUpdated++;
            }
            else
            {
                material = new Material
                {
                    Shape = shape,
                    Type = type,
                    Grade = dto.Grade,
                    Size = dto.Size,
                    Description = dto.Description,
                    CreatedAt = DateTime.UtcNow
                };

                var dimensions = createDimensions();
                material = await _materialService.CreateWithDimensionsAsync(material, dimensions);
                existingMaterials.Add(material);
                result.MaterialsCreated++;
            }

            await context.SaveChangesAsync();

            await ImportStockItemsAsync(context, material, dto.StockItems, supplierMap, result);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Material '{shape} - {dto.Size}': {ex.Message}");
        }
    }

    private async Task ImportStockItemsAsync(
        ApplicationDbContext context, Material material, List<CatalogStockItemDto> stockItems,
        Dictionary<string, int> supplierMap, ImportResultDto result)
    {
        var existingStockItems = await context.StockItems
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
                    context.StockItems.Add(stockItem);
                    await context.SaveChangesAsync();
                    existingStockItems.Add(stockItem);
                    result.StockItemsCreated++;
                }

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
                            context.SupplierOfferings.Add(offering);
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

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                result.Errors.Add(
                    $"Stock item '{material.DisplayName} @ {dto.LengthInches}\"': {ex.Message}");
            }
        }
    }

    private static List<CatalogStockItemDto> MapStockItems(Material m, List<Supplier> suppliers)
    {
        return m.StockItems.OrderBy(s => s.LengthInches).Select(s => new CatalogStockItemDto
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
        }).ToList();
    }
}
