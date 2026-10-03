using CutList.Core.Formatting;
using CutList.Core.Nesting;
using CutList.Web.Data.Entities;
using CutList.Web.DTOs;
using CutList.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CutList.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly JobService _jobService;
    private readonly CutListPackingService _packingService;

    public JobsController(JobService jobService, CutListPackingService packingService)
    {
        _jobService = jobService;
        _packingService = packingService;
    }

    [HttpGet]
    public async Task<ActionResult<List<JobDto>>> GetAll()
    {
        var jobs = await _jobService.GetAllAsync();
        return Ok(jobs.Select(MapToDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<JobDetailDto>> GetById(int id)
    {
        var job = await _jobService.GetByIdAsync(id);
        if (job == null)
            return NotFound();

        return Ok(MapToDetailDto(job));
    }

    [HttpPost]
    public async Task<ActionResult<JobDetailDto>> Create(CreateJobDto dto)
    {
        var job = new Job
        {
            Name = dto.Name,
            Customer = dto.Customer,
            CuttingToolId = dto.CuttingToolId,
            Notes = dto.Notes
        };

        await _jobService.CreateAsync(job);
        var created = await _jobService.GetByIdAsync(job.Id);
        return CreatedAtAction(nameof(GetById), new { id = job.Id }, MapToDetailDto(created!));
    }

    [HttpPost("quick-create")]
    public async Task<ActionResult<JobDetailDto>> QuickCreate(QuickCreateJobDto dto)
    {
        var job = await _jobService.QuickCreateAsync(dto.Customer);
        var created = await _jobService.GetByIdAsync(job.Id);
        return CreatedAtAction(nameof(GetById), new { id = job.Id }, MapToDetailDto(created!));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<JobDetailDto>> Update(int id, UpdateJobDto dto)
    {
        var job = await _jobService.GetByIdAsync(id);
        if (job == null)
            return NotFound();

        if (dto.Name != null) job.Name = dto.Name;
        if (dto.Customer != null) job.Customer = dto.Customer;
        if (dto.CuttingToolId.HasValue) job.CuttingToolId = dto.CuttingToolId;
        if (dto.Notes != null) job.Notes = dto.Notes;

        await _jobService.UpdateAsync(job);

        var updated = await _jobService.GetByIdAsync(id);
        return Ok(MapToDetailDto(updated!));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var job = await _jobService.GetByIdAsync(id);
        if (job == null)
            return NotFound();

        await _jobService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/duplicate")]
    public async Task<ActionResult<JobDetailDto>> Duplicate(int id)
    {
        try
        {
            var duplicate = await _jobService.DuplicateAsync(id);
            var loaded = await _jobService.GetByIdAsync(duplicate.Id);
            return CreatedAtAction(nameof(GetById), new { id = duplicate.Id }, MapToDetailDto(loaded!));
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    // --- Parts ---

    [HttpGet("{id}/parts")]
    public async Task<ActionResult<List<JobPartDto>>> GetParts(int id)
    {
        var job = await _jobService.GetByIdAsync(id);
        if (job == null)
            return NotFound();

        return Ok(job.Parts.Select(MapPartToDto).ToList());
    }

    [HttpPost("{id}/parts")]
    public async Task<ActionResult<JobPartDto>> AddPart(int id, CreateJobPartDto dto)
    {
        var job = await _jobService.GetByIdAsync(id);
        if (job == null)
            return NotFound();

        decimal lengthInches;
        try
        {
            lengthInches = (decimal)ArchUnits.ParseToInches(dto.Length);
        }
        catch
        {
            return BadRequest($"Invalid length format: {dto.Length}");
        }

        var part = new JobPart
        {
            JobId = id,
            MaterialId = dto.MaterialId,
            Name = dto.Name,
            LengthInches = lengthInches,
            Quantity = dto.Quantity
        };

        await _jobService.AddPartAsync(part);

        // Reload to get material name
        var reloadedJob = await _jobService.GetByIdAsync(id);
        var addedPart = reloadedJob!.Parts.FirstOrDefault(p => p.Id == part.Id);
        return CreatedAtAction(nameof(GetParts), new { id }, MapPartToDto(addedPart ?? part));
    }

    [HttpPut("{id}/parts/{partId}")]
    public async Task<ActionResult<JobPartDto>> UpdatePart(int id, int partId, UpdateJobPartDto dto)
    {
        var job = await _jobService.GetByIdAsync(id);
        if (job == null)
            return NotFound();

        var part = job.Parts.FirstOrDefault(p => p.Id == partId);
        if (part == null)
            return NotFound();

        if (dto.MaterialId.HasValue) part.MaterialId = dto.MaterialId.Value;
        if (dto.Name != null) part.Name = dto.Name;
        if (dto.Length != null)
        {
            try
            {
                part.LengthInches = (decimal)ArchUnits.ParseToInches(dto.Length);
            }
            catch
            {
                return BadRequest($"Invalid length format: {dto.Length}");
            }
        }
        if (dto.Quantity.HasValue) part.Quantity = dto.Quantity.Value;

        await _jobService.UpdatePartAsync(part);

        var reloadedJob = await _jobService.GetByIdAsync(id);
        var updatedPart = reloadedJob!.Parts.FirstOrDefault(p => p.Id == partId);
        return Ok(MapPartToDto(updatedPart ?? part));
    }

    [HttpDelete("{id}/parts/{partId}")]
    public async Task<IActionResult> DeletePart(int id, int partId)
    {
        var job = await _jobService.GetByIdAsync(id);
        if (job == null)
            return NotFound();

        var part = job.Parts.FirstOrDefault(p => p.Id == partId);
        if (part == null)
            return NotFound();

        await _jobService.DeletePartAsync(partId);
        return NoContent();
    }

    // --- Stock ---

    [HttpGet("{id}/stock")]
    public async Task<ActionResult<List<JobStockDto>>> GetStock(int id)
    {
        var job = await _jobService.GetByIdAsync(id);
        if (job == null)
            return NotFound();

        return Ok(job.Stock.Select(MapStockToDto).ToList());
    }

    [HttpPost("{id}/stock")]
    public async Task<ActionResult<JobStockDto>> AddStock(int id, CreateJobStockDto dto)
    {
        var job = await _jobService.GetByIdAsync(id);
        if (job == null)
            return NotFound();

        decimal lengthInches;
        try
        {
            lengthInches = (decimal)ArchUnits.ParseToInches(dto.Length);
        }
        catch
        {
            return BadRequest($"Invalid length format: {dto.Length}");
        }

        var stock = new JobStock
        {
            JobId = id,
            MaterialId = dto.MaterialId,
            StockItemId = dto.StockItemId,
            LengthInches = lengthInches,
            Quantity = dto.Quantity,
            IsCustomLength = dto.IsCustomLength,
            Priority = dto.Priority
        };

        await _jobService.AddStockAsync(stock);

        var reloadedJob = await _jobService.GetByIdAsync(id);
        var addedStock = reloadedJob!.Stock.FirstOrDefault(s => s.Id == stock.Id);
        return CreatedAtAction(nameof(GetStock), new { id }, MapStockToDto(addedStock ?? stock));
    }

    [HttpPut("{id}/stock/{stockId}")]
    public async Task<ActionResult<JobStockDto>> UpdateStock(int id, int stockId, UpdateJobStockDto dto)
    {
        var job = await _jobService.GetByIdAsync(id);
        if (job == null)
            return NotFound();

        var stock = job.Stock.FirstOrDefault(s => s.Id == stockId);
        if (stock == null)
            return NotFound();

        if (dto.StockItemId.HasValue) stock.StockItemId = dto.StockItemId;
        if (dto.Length != null)
        {
            try
            {
                stock.LengthInches = (decimal)ArchUnits.ParseToInches(dto.Length);
            }
            catch
            {
                return BadRequest($"Invalid length format: {dto.Length}");
            }
        }
        if (dto.Quantity.HasValue) stock.Quantity = dto.Quantity.Value;
        if (dto.IsCustomLength.HasValue) stock.IsCustomLength = dto.IsCustomLength.Value;
        if (dto.Priority.HasValue) stock.Priority = dto.Priority.Value;

        await _jobService.UpdateStockAsync(stock);

        var reloadedJob = await _jobService.GetByIdAsync(id);
        var updatedStock = reloadedJob!.Stock.FirstOrDefault(s => s.Id == stockId);
        return Ok(MapStockToDto(updatedStock ?? stock));
    }

    [HttpDelete("{id}/stock/{stockId}")]
    public async Task<IActionResult> DeleteStock(int id, int stockId)
    {
        var job = await _jobService.GetByIdAsync(id);
        if (job == null)
            return NotFound();

        var stock = job.Stock.FirstOrDefault(s => s.Id == stockId);
        if (stock == null)
            return NotFound();

        await _jobService.DeleteStockAsync(stockId);
        return NoContent();
    }

    [HttpGet("{id}/available-stock/{materialId}")]
    public async Task<ActionResult<List<StockItemDto>>> GetAvailableStock(int id, int materialId)
    {
        var job = await _jobService.GetByIdAsync(id);
        if (job == null)
            return NotFound();

        var items = await _jobService.GetAvailableStockForMaterialAsync(materialId);
        return Ok(items.Select(s => new StockItemDto
        {
            Id = s.Id,
            MaterialId = s.MaterialId,
            MaterialName = s.Material?.DisplayName ?? string.Empty,
            LengthInches = s.LengthInches,
            LengthFormatted = ArchUnits.FormatFromInches((double)s.LengthInches),
            Name = s.Name,
            IsActive = s.IsActive
        }).ToList());
    }

    // --- Packing ---

    [HttpPost("{id}/pack")]
    public async Task<ActionResult<PackResponseDto>> Pack(int id, PackJobRequestDto? dto = null)
    {
        var job = await _jobService.GetByIdAsync(id);
        if (job == null)
            return NotFound();

        if (job.Parts.Count == 0)
            return BadRequest("Job has no parts to pack");

        // Determine kerf
        decimal kerf = dto?.KerfOverride
            ?? job.CuttingTool?.KerfInches
            ?? (await _jobService.GetDefaultCuttingToolAsync())?.KerfInches
            ?? 0.125m;

        MultiMaterialPackResult result;
        try
        {
            result = await _packingService.PackAsync(job.Parts, kerf, job.Stock.Any() ? job.Stock : null, dto?.Engine);
        }
        catch (UnknownPackingEngineException ex)
        {
            return BadRequest(ex.Message);
        }
        var summary = _packingService.GetSummary(result);

        return Ok(MapPackResult(result, summary));
    }

    // --- Mapping helpers ---

    // Lock timestamps are stored as UTC; label them so clients do not read them as local time.
    private static DateTime? AsUtc(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

    private static JobDto MapToDto(Job j) => new()
    {
        Id = j.Id,
        JobNumber = j.JobNumber,
        Name = j.Name,
        Customer = j.Customer,
        CuttingToolId = j.CuttingToolId,
        CuttingToolName = j.CuttingTool?.Name,
        Notes = j.Notes,
        CreatedAt = j.CreatedAt,
        UpdatedAt = j.UpdatedAt,
        PartCount = j.Parts?.Count ?? 0,
        StockCount = j.Stock?.Count ?? 0,
        IsLocked = j.IsLocked,
        LockedAt = AsUtc(j.LockedAt)
    };

    private static JobDetailDto MapToDetailDto(Job j) => new()
    {
        Id = j.Id,
        JobNumber = j.JobNumber,
        Name = j.Name,
        Customer = j.Customer,
        CuttingToolId = j.CuttingToolId,
        CuttingToolName = j.CuttingTool?.Name,
        Notes = j.Notes,
        CreatedAt = j.CreatedAt,
        UpdatedAt = j.UpdatedAt,
        PartCount = j.Parts?.Count ?? 0,
        StockCount = j.Stock?.Count ?? 0,
        IsLocked = j.IsLocked,
        LockedAt = AsUtc(j.LockedAt),
        Parts = j.Parts?.Select(MapPartToDto).ToList() ?? new(),
        Stock = j.Stock?.Select(MapStockToDto).ToList() ?? new()
    };

    private static JobPartDto MapPartToDto(JobPart p) => new()
    {
        Id = p.Id,
        JobId = p.JobId,
        MaterialId = p.MaterialId,
        MaterialName = p.Material?.DisplayName ?? string.Empty,
        Name = p.Name,
        LengthInches = p.LengthInches,
        LengthFormatted = ArchUnits.FormatFromInches((double)p.LengthInches),
        Quantity = p.Quantity,
        SortOrder = p.SortOrder
    };

    private static JobStockDto MapStockToDto(JobStock s) => new()
    {
        Id = s.Id,
        JobId = s.JobId,
        MaterialId = s.MaterialId,
        MaterialName = s.Material?.DisplayName ?? string.Empty,
        StockItemId = s.StockItemId,
        LengthInches = s.LengthInches,
        LengthFormatted = ArchUnits.FormatFromInches((double)s.LengthInches),
        Quantity = s.Quantity,
        IsCustomLength = s.IsCustomLength,
        Priority = s.Priority,
        SortOrder = s.SortOrder
    };

    private static PackResponseDto MapPackResult(MultiMaterialPackResult result, MultiMaterialPackingSummary summary)
    {
        var response = new PackResponseDto
        {
            EngineId = result.EngineId,
            EngineName = result.EngineName
        };

        foreach (var mr in result.MaterialResults)
        {
            var matResult = new MaterialPackResultDto
            {
                MaterialId = mr.Material.Id,
                MaterialName = mr.Material.DisplayName,
                InStockBins = mr.InStockBins.Select(MapBinToDto).ToList(),
                ToBePurchasedBins = mr.ToBePurchasedBins.Select(MapBinToDto).ToList(),
                ItemsNotPlaced = mr.PackResult.ItemsNotUsed.Select(i => new PackedItemDto
                {
                    Name = i.Name,
                    LengthInches = i.Length,
                    LengthFormatted = ArchUnits.FormatFromInches(i.Length)
                }).ToList()
            };

            var ms = summary.MaterialSummaries.FirstOrDefault(s => s.Material.Id == mr.Material.Id);
            if (ms != null)
            {
                matResult.Summary = new MaterialPackingSummaryDto
                {
                    MaterialId = ms.Material.Id,
                    MaterialName = ms.Material.DisplayName,
                    InStockBins = ms.InStockBins,
                    ToBePurchasedBins = ms.ToBePurchasedBins,
                    TotalPieces = ms.TotalPieces,
                    TotalMaterialInches = ms.TotalMaterial,
                    TotalUsedInches = ms.TotalUsed,
                    TotalWasteInches = ms.TotalWaste,
                    Efficiency = ms.Efficiency,
                    ItemsNotPlaced = ms.ItemsNotPlaced
                };
            }

            response.Materials.Add(matResult);
        }

        response.Summary = new PackingSummaryDto
        {
            TotalInStockBins = summary.TotalInStockBins,
            TotalToBePurchasedBins = summary.TotalToBePurchasedBins,
            TotalPieces = summary.TotalPieces,
            TotalMaterialInches = summary.TotalMaterial,
            TotalMaterialFormatted = ArchUnits.FormatFromInches(summary.TotalMaterial),
            TotalUsedInches = summary.TotalUsed,
            TotalUsedFormatted = ArchUnits.FormatFromInches(summary.TotalUsed),
            TotalWasteInches = summary.TotalWaste,
            TotalWasteFormatted = ArchUnits.FormatFromInches(summary.TotalWaste),
            Efficiency = summary.Efficiency,
            TotalItemsNotPlaced = summary.TotalItemsNotPlaced,
            MaterialSummaries = summary.MaterialSummaries.Select(ms => new MaterialPackingSummaryDto
            {
                MaterialId = ms.Material.Id,
                MaterialName = ms.Material.DisplayName,
                InStockBins = ms.InStockBins,
                ToBePurchasedBins = ms.ToBePurchasedBins,
                TotalPieces = ms.TotalPieces,
                TotalMaterialInches = ms.TotalMaterial,
                TotalUsedInches = ms.TotalUsed,
                TotalWasteInches = ms.TotalWaste,
                Efficiency = ms.Efficiency,
                ItemsNotPlaced = ms.ItemsNotPlaced
            }).ToList()
        };

        return response;
    }

    private static PackedBinDto MapBinToDto(CutList.Core.Bin bin) => new()
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
    };
}
