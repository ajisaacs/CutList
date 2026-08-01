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

        var cuttingTools = await context.CuttingTools
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .AsNoTracking()
            .ToListAsync();

        var materials = await context.Materials
            .Include(m => m.Dimensions)
            .Include(m => m.StockItems.Where(s => s.IsActive))
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
                var stockItems = MapStockItems(m);

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
            // 1. Cutting tools - upsert by name
            await ImportCuttingToolsAsync(context, data.CuttingTools, result);

            // 2. Materials + stock items
            await ImportAllMaterialsAsync(context, data.Materials, result);

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            result.Errors.Add($"Transaction failed: {ex.Message}");
        }

        return result;
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
        ApplicationDbContext context, CatalogMaterialsDto materials, ImportResultDto result)
    {
        var existingMaterials = await context.Materials
            .Include(m => m.Dimensions)
            .Include(m => m.StockItems)
            .ToListAsync();

        foreach (var dto in materials.Angles)
            await ImportMaterialAsync(context, dto, MaterialShape.Angle, existingMaterials, result,
                () => new AngleDimensions { Leg1 = dto.Leg1, Leg2 = dto.Leg2, Thickness = dto.Thickness },
                dim => { var d = (AngleDimensions)dim; d.Leg1 = dto.Leg1; d.Leg2 = dto.Leg2; d.Thickness = dto.Thickness; });

        foreach (var dto in materials.Channels)
            await ImportMaterialAsync(context, dto, MaterialShape.Channel, existingMaterials, result,
                () => new ChannelDimensions { Height = dto.Height, Flange = dto.Flange, Web = dto.Web },
                dim => { var d = (ChannelDimensions)dim; d.Height = dto.Height; d.Flange = dto.Flange; d.Web = dto.Web; });

        foreach (var dto in materials.FlatBars)
            await ImportMaterialAsync(context, dto, MaterialShape.FlatBar, existingMaterials, result,
                () => new FlatBarDimensions { Width = dto.Width, Thickness = dto.Thickness },
                dim => { var d = (FlatBarDimensions)dim; d.Width = dto.Width; d.Thickness = dto.Thickness; });

        foreach (var dto in materials.IBeams)
            await ImportMaterialAsync(context, dto, MaterialShape.IBeam, existingMaterials, result,
                () => new IBeamDimensions { Height = dto.Height, WeightPerFoot = dto.WeightPerFoot },
                dim => { var d = (IBeamDimensions)dim; d.Height = dto.Height; d.WeightPerFoot = dto.WeightPerFoot; });

        foreach (var dto in materials.Pipes)
            await ImportMaterialAsync(context, dto, MaterialShape.Pipe, existingMaterials, result,
                () => new PipeDimensions { NominalSize = dto.NominalSize, Wall = dto.Wall, Schedule = dto.Schedule },
                dim => { var d = (PipeDimensions)dim; d.NominalSize = dto.NominalSize; d.Wall = (decimal?)dto.Wall; d.Schedule = dto.Schedule; });

        foreach (var dto in materials.RectangularTubes)
            await ImportMaterialAsync(context, dto, MaterialShape.RectangularTube, existingMaterials, result,
                () => new RectangularTubeDimensions { Width = dto.Width, Height = dto.Height, Wall = dto.Wall },
                dim => { var d = (RectangularTubeDimensions)dim; d.Width = dto.Width; d.Height = dto.Height; d.Wall = dto.Wall; });

        foreach (var dto in materials.RoundBars)
            await ImportMaterialAsync(context, dto, MaterialShape.RoundBar, existingMaterials, result,
                () => new RoundBarDimensions { Diameter = dto.Diameter },
                dim => { var d = (RoundBarDimensions)dim; d.Diameter = dto.Diameter; });

        foreach (var dto in materials.RoundTubes)
            await ImportMaterialAsync(context, dto, MaterialShape.RoundTube, existingMaterials, result,
                () => new RoundTubeDimensions { OuterDiameter = dto.OuterDiameter, Wall = dto.Wall },
                dim => { var d = (RoundTubeDimensions)dim; d.OuterDiameter = dto.OuterDiameter; d.Wall = dto.Wall; });

        foreach (var dto in materials.SquareBars)
            await ImportMaterialAsync(context, dto, MaterialShape.SquareBar, existingMaterials, result,
                () => new SquareBarDimensions { Size = dto.SideLength },
                dim => { var d = (SquareBarDimensions)dim; d.Size = dto.SideLength; });

        foreach (var dto in materials.SquareTubes)
            await ImportMaterialAsync(context, dto, MaterialShape.SquareTube, existingMaterials, result,
                () => new SquareTubeDimensions { Size = dto.SideLength, Wall = dto.Wall },
                dim => { var d = (SquareTubeDimensions)dim; d.Size = dto.SideLength; d.Wall = dto.Wall; });
    }

    private async Task ImportMaterialAsync(
        ApplicationDbContext context, CatalogMaterialBaseDto dto, MaterialShape shape,
        List<Material> existingMaterials,
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

            await ImportStockItemsAsync(context, material, dto.StockItems, result);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Material '{shape} - {dto.Size}': {ex.Message}");
        }
    }

    private async Task ImportStockItemsAsync(
        ApplicationDbContext context, Material material, List<CatalogStockItemDto> stockItems,
        ImportResultDto result)
    {
        var existingStockItems = await context.StockItems
            .Where(s => s.MaterialId == material.Id)
            .ToListAsync();

        foreach (var dto in stockItems)
        {
            try
            {
                var existing = existingStockItems.FirstOrDefault(
                    s => s.LengthInches == dto.LengthInches);

                if (existing != null)
                {
                    existing.Name = dto.Name ?? existing.Name;
                    existing.Notes = dto.Notes ?? existing.Notes;
                    existing.IsActive = true;
                    existing.UpdatedAt = DateTime.UtcNow;
                    result.StockItemsUpdated++;
                }
                else
                {
                    var stockItem = new StockItem
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
            }
            catch (Exception ex)
            {
                result.Errors.Add(
                    $"Stock item '{material.DisplayName} @ {dto.LengthInches}\"': {ex.Message}");
            }
        }

        await context.SaveChangesAsync();
    }

    private static List<CatalogStockItemDto> MapStockItems(Material m)
    {
        return m.StockItems.OrderBy(s => s.LengthInches).Select(s => new CatalogStockItemDto
        {
            LengthInches = s.LengthInches,
            Name = s.Name,
            QuantityOnHand = s.QuantityOnHand,
            Notes = s.Notes
        }).ToList();
    }
}
