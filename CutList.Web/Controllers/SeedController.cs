using CutList.Web.Controllers.Dtos;
using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SeedController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("export")]
    public async Task<ActionResult<SeedExportData>> Export()
    {
        var materials = await _context.Materials
            .Include(m => m.Dimensions)
            .Include(m => m.StockItems.Where(s => s.IsActive))
                .ThenInclude(s => s.SupplierOfferings.Where(o => o.IsActive))
            .Where(m => m.IsActive)
            .OrderBy(m => m.Shape).ThenBy(m => m.SortOrder)
            .AsNoTracking()
            .ToListAsync();

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

        var export = new SeedExportData
        {
            ExportedAt = DateTime.UtcNow,
            Suppliers = suppliers.Select(s => new SeedSupplierDto
            {
                Name = s.Name,
                ContactInfo = s.ContactInfo,
                Notes = s.Notes
            }).ToList(),
            CuttingTools = cuttingTools.Select(t => new SeedCuttingToolDto
            {
                Name = t.Name,
                KerfInches = t.KerfInches,
                IsDefault = t.IsDefault
            }).ToList(),
            Materials = materials.Select(m => new SeedMaterialDto
            {
                Shape = m.Shape.ToString(),
                Type = m.Type.ToString(),
                Grade = m.Grade,
                Size = m.Size,
                Description = m.Description,
                Dimensions = MapDimensionsToDto(m.Dimensions),
                StockItems = m.StockItems.OrderBy(s => s.LengthInches).Select(s => new SeedStockItemDto
                {
                    LengthInches = s.LengthInches,
                    Name = s.Name,
                    QuantityOnHand = s.QuantityOnHand,
                    Notes = s.Notes,
                    SupplierOfferings = s.SupplierOfferings.Select(o => new SeedSupplierOfferingDto
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

        return Ok(export);
    }

    [HttpPost("import")]
    public async Task<ActionResult> Import([FromBody] SeedExportData data)
    {
        var suppliersCreated = 0;
        var toolsCreated = 0;
        var materialsCreated = 0;
        var materialsSkipped = 0;
        var stockCreated = 0;
        var offeringsCreated = 0;

        // 1. Suppliers - match by name
        var supplierMap = new Dictionary<string, Supplier>();
        foreach (var dto in data.Suppliers)
        {
            var existing = await _context.Suppliers.FirstOrDefaultAsync(s => s.Name == dto.Name);
            if (existing != null)
            {
                supplierMap[dto.Name] = existing;
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
                supplierMap[dto.Name] = supplier;
                suppliersCreated++;
            }
        }
        await _context.SaveChangesAsync();

        // 2. Cutting tools - match by name
        foreach (var dto in data.CuttingTools)
        {
            var exists = await _context.CuttingTools.AnyAsync(t => t.Name == dto.Name);
            if (!exists)
            {
                _context.CuttingTools.Add(new CuttingTool
                {
                    Name = dto.Name,
                    KerfInches = dto.KerfInches,
                    IsDefault = dto.IsDefault
                });
                toolsCreated++;
            }
        }
        await _context.SaveChangesAsync();

        // 3. Materials - match by shape + size + grade
        foreach (var dto in data.Materials)
        {
            if (!Enum.TryParse<MaterialShape>(dto.Shape, out var shape))
            {
                materialsSkipped++;
                continue;
            }

            Enum.TryParse<MaterialType>(dto.Type, out var type);

            var existing = await _context.Materials
                .Include(m => m.StockItems)
                .FirstOrDefaultAsync(m => m.Shape == shape && m.Size == dto.Size && m.Grade == dto.Grade && m.IsActive);

            Material material;
            if (existing != null)
            {
                material = existing;
                materialsSkipped++;
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

                if (dto.Dimensions != null)
                    material.Dimensions = MapDtoToDimensions(shape, dto.Dimensions);

                _context.Materials.Add(material);
                await _context.SaveChangesAsync();
                materialsCreated++;
            }

            // 4. Stock items - match by material + length
            foreach (var stockDto in dto.StockItems)
            {
                var existingStock = material.StockItems
                    .FirstOrDefault(s => s.LengthInches == stockDto.LengthInches && s.IsActive);

                StockItem stockItem;
                if (existingStock != null)
                {
                    stockItem = existingStock;
                }
                else
                {
                    stockItem = new StockItem
                    {
                        MaterialId = material.Id,
                        LengthInches = stockDto.LengthInches,
                        Name = stockDto.Name,
                        QuantityOnHand = stockDto.QuantityOnHand,
                        Notes = stockDto.Notes,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.StockItems.Add(stockItem);
                    await _context.SaveChangesAsync();
                    stockCreated++;
                }

                // 5. Supplier offerings
                foreach (var offeringDto in stockDto.SupplierOfferings)
                {
                    if (!supplierMap.TryGetValue(offeringDto.SupplierName, out var supplier))
                        continue;

                    var existingOffering = await _context.SupplierOfferings
                        .AnyAsync(o => o.SupplierId == supplier.Id && o.StockItemId == stockItem.Id);

                    if (!existingOffering)
                    {
                        _context.SupplierOfferings.Add(new SupplierOffering
                        {
                            SupplierId = supplier.Id,
                            StockItemId = stockItem.Id,
                            PartNumber = offeringDto.PartNumber,
                            SupplierDescription = offeringDto.SupplierDescription,
                            Price = offeringDto.Price,
                            Notes = offeringDto.Notes
                        });
                        offeringsCreated++;
                    }
                }
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Import completed",
            SuppliersCreated = suppliersCreated,
            CuttingToolsCreated = toolsCreated,
            MaterialsCreated = materialsCreated,
            MaterialsSkipped = materialsSkipped,
            StockItemsCreated = stockCreated,
            SupplierOfferingsCreated = offeringsCreated
        });
    }

    private static SeedDimensionsDto? MapDimensionsToDto(MaterialDimensions? dim) => dim switch
    {
        RoundBarDimensions d => new SeedDimensionsDto { Diameter = d.Diameter },
        RoundTubeDimensions d => new SeedDimensionsDto { OuterDiameter = d.OuterDiameter, Wall = d.Wall },
        FlatBarDimensions d => new SeedDimensionsDto { Width = d.Width, Thickness = d.Thickness },
        SquareBarDimensions d => new SeedDimensionsDto { Size = d.Size },
        SquareTubeDimensions d => new SeedDimensionsDto { Size = d.Size, Wall = d.Wall },
        RectangularTubeDimensions d => new SeedDimensionsDto { Width = d.Width, Height = d.Height, Wall = d.Wall },
        AngleDimensions d => new SeedDimensionsDto { Leg1 = d.Leg1, Leg2 = d.Leg2, Thickness = d.Thickness },
        ChannelDimensions d => new SeedDimensionsDto { Height = d.Height, Flange = d.Flange, Web = d.Web },
        IBeamDimensions d => new SeedDimensionsDto { Height = d.Height, WeightPerFoot = d.WeightPerFoot },
        PipeDimensions d => new SeedDimensionsDto { NominalSize = d.NominalSize, Wall = d.Wall, Schedule = d.Schedule },
        _ => null
    };

    private static MaterialDimensions? MapDtoToDimensions(MaterialShape shape, SeedDimensionsDto dto) => shape switch
    {
        MaterialShape.RoundBar => new RoundBarDimensions { Diameter = dto.Diameter ?? 0 },
        MaterialShape.RoundTube => new RoundTubeDimensions { OuterDiameter = dto.OuterDiameter ?? 0, Wall = dto.Wall ?? 0 },
        MaterialShape.FlatBar => new FlatBarDimensions { Width = dto.Width ?? 0, Thickness = dto.Thickness ?? 0 },
        MaterialShape.SquareBar => new SquareBarDimensions { Size = dto.Size ?? 0 },
        MaterialShape.SquareTube => new SquareTubeDimensions { Size = dto.Size ?? 0, Wall = dto.Wall ?? 0 },
        MaterialShape.RectangularTube => new RectangularTubeDimensions { Width = dto.Width ?? 0, Height = dto.Height ?? 0, Wall = dto.Wall ?? 0 },
        MaterialShape.Angle => new AngleDimensions { Leg1 = dto.Leg1 ?? 0, Leg2 = dto.Leg2 ?? 0, Thickness = dto.Thickness ?? 0 },
        MaterialShape.Channel => new ChannelDimensions { Height = dto.Height ?? 0, Flange = dto.Flange ?? 0, Web = dto.Web ?? 0 },
        MaterialShape.IBeam => new IBeamDimensions { Height = dto.Height ?? 0, WeightPerFoot = dto.WeightPerFoot ?? 0 },
        MaterialShape.Pipe => new PipeDimensions { NominalSize = dto.NominalSize ?? 0, Wall = dto.Wall ?? 0, Schedule = dto.Schedule },
        _ => null
    };
}
