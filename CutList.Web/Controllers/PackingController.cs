using CutList.Core;
using CutList.Core.Formatting;
using CutList.Core.Nesting;
using CutList.Web.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CutList.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PackingController : ControllerBase
{
    private readonly IPackingEngineCatalog _engines;

    public PackingController(IPackingEngineCatalog engines)
    {
        _engines = engines;
    }

    [HttpGet("engines")]
    public ActionResult<List<PackingEngineDto>> GetEngines()
    {
        return Ok(_engines.Engines.Select(e => new PackingEngineDto
        {
            Id = e.Id,
            Name = e.DisplayName,
            Description = e.Description,
            IsDefault = e.Id == _engines.Default.Id
        }).ToList());
    }

    [HttpPost("optimize")]
    public ActionResult<object> Optimize(StandalonePackRequestDto dto)
    {
        if (dto.Parts.Count == 0)
            return BadRequest("At least one part is required");

        if (dto.StockBins.Count == 0)
            return BadRequest("At least one stock bin is required");

        // Parse parts
        var items = new List<BinItem>();
        foreach (var part in dto.Parts)
        {
            double length;
            try
            {
                length = ArchUnits.ParseToInches(part.Length);
            }
            catch
            {
                return BadRequest($"Invalid length format for part '{part.Name}': {part.Length}");
            }

            for (int i = 0; i < part.Quantity; i++)
            {
                items.Add(new BinItem(part.Name, length));
            }
        }

        // Parse stock bins
        var multiBins = new List<MultiBin>();
        foreach (var bin in dto.StockBins)
        {
            double length;
            try
            {
                length = ArchUnits.ParseToInches(bin.Length);
            }
            catch
            {
                return BadRequest($"Invalid length format for stock bin: {bin.Length}");
            }

            multiBins.Add(new MultiBin(length, bin.Quantity, bin.Priority));
        }

        // Select engine (Engine wins over the legacy Strategy field; unknown ids are rejected)
        PackingEngineInfo engineInfo;
        try
        {
            engineInfo = _engines.Resolve(dto.Engine ?? dto.Strategy);
        }
        catch (UnknownPackingEngineException ex)
        {
            return BadRequest(ex.Message);
        }

        // Run packing
        var engine = new MultiBinEngine(_engines.Create(engineInfo.Id))
        {
            Spacing = (double)dto.Kerf
        };

        engine.SetBins(multiBins);
        var result = engine.Pack(items);

        // Map result
        var bins = result.Bins.Select(bin => new PackedBinDto
        {
            LengthInches = bin.Length,
            LengthFormatted = ArchUnits.FormatFromInches(bin.Length),
            UsedInches = bin.UsedLength,
            UsedFormatted = ArchUnits.FormatFromInches(bin.UsedLength),
            WasteInches = bin.RemainingLength,
            WasteFormatted = ArchUnits.FormatFromInches(bin.RemainingLength),
            Efficiency = bin.Length > 0 ? bin.UsedLength / bin.Length * 100 : 0,
            Items = bin.Items.Select(i => new PackedItemDto
            {
                Name = i.Name,
                LengthInches = i.Length,
                LengthFormatted = ArchUnits.FormatFromInches(i.Length)
            }).ToList()
        }).ToList();

        var itemsNotPlaced = result.ItemsNotUsed.Select(i => new PackedItemDto
        {
            Name = i.Name,
            LengthInches = i.Length,
            LengthFormatted = ArchUnits.FormatFromInches(i.Length)
        }).ToList();

        var totalMaterial = result.Bins.Sum(b => b.Length);
        var totalUsed = result.Bins.Sum(b => b.UsedLength);
        var totalWaste = result.Bins.Sum(b => b.RemainingLength);

        return Ok(new
        {
            Engine = engineInfo.Id,
            EngineName = engineInfo.DisplayName,
            Bins = bins,
            ItemsNotPlaced = itemsNotPlaced,
            Summary = new
            {
                TotalBins = result.Bins.Count,
                TotalPieces = result.Bins.Sum(b => b.Items.Count),
                TotalMaterialInches = totalMaterial,
                TotalMaterialFormatted = ArchUnits.FormatFromInches(totalMaterial),
                TotalUsedInches = totalUsed,
                TotalUsedFormatted = ArchUnits.FormatFromInches(totalUsed),
                TotalWasteInches = totalWaste,
                TotalWasteFormatted = ArchUnits.FormatFromInches(totalWaste),
                Efficiency = totalMaterial > 0 ? totalUsed / totalMaterial * 100 : 0,
                ItemsNotPlaced = result.ItemsNotUsed.Count
            }
        });
    }

    [HttpPost("parse-length")]
    public ActionResult<ParseLengthResponseDto> ParseLength(ParseLengthRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Input))
            return BadRequest("Input is required");

        try
        {
            var inches = ArchUnits.ParseToInches(dto.Input);
            return Ok(new ParseLengthResponseDto
            {
                Inches = inches,
                Formatted = ArchUnits.FormatFromInches(inches)
            });
        }
        catch (Exception ex)
        {
            return BadRequest($"Could not parse '{dto.Input}': {ex.Message}");
        }
    }

    [HttpPost("format-length")]
    public ActionResult<FormatLengthResponseDto> FormatLength(FormatLengthRequestDto dto)
    {
        return Ok(new FormatLengthResponseDto
        {
            Inches = dto.Inches,
            Formatted = ArchUnits.FormatFromInches(dto.Inches)
        });
    }
}
