using CutList.Core.Formatting;
using CutList.Web.Data.Entities;
using CutList.Web.DTOs;
using CutList.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CutList.Web.Controllers;

[ApiController]
[Route("api/stock-items")]
public class StockItemsController : ControllerBase
{
    private readonly StockItemService _stockItemService;

    public StockItemsController(StockItemService stockItemService)
    {
        _stockItemService = stockItemService;
    }

    [HttpGet]
    public async Task<ActionResult<List<StockItemDto>>> GetAll(
        [FromQuery] bool includeInactive = false,
        [FromQuery] int? materialId = null)
    {
        List<StockItem> items;
        if (materialId.HasValue)
            items = await _stockItemService.GetByMaterialAsync(materialId.Value, includeInactive);
        else
            items = await _stockItemService.GetAllAsync(includeInactive);

        return Ok(items.Select(MapToDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StockItemDto>> GetById(int id)
    {
        var item = await _stockItemService.GetByIdAsync(id);
        if (item == null)
            return NotFound();

        return Ok(MapToDto(item));
    }

    [HttpPost]
    public async Task<ActionResult<StockItemDto>> Create(CreateStockItemDto dto)
    {
        double lengthInches;
        try
        {
            lengthInches = ArchUnits.ParseToInches(dto.Length);
        }
        catch
        {
            return BadRequest($"Invalid length format: {dto.Length}");
        }

        var exists = await _stockItemService.ExistsAsync(dto.MaterialId, (decimal)lengthInches);
        if (exists)
            return Conflict("A stock item with this material and length already exists");

        var stockItem = new StockItem
        {
            MaterialId = dto.MaterialId,
            LengthInches = (decimal)lengthInches,
            Name = dto.Name,
            QuantityOnHand = dto.QuantityOnHand,
            Notes = dto.Notes
        };

        await _stockItemService.CreateAsync(stockItem);

        // Reload with includes
        var created = await _stockItemService.GetByIdAsync(stockItem.Id);
        return CreatedAtAction(nameof(GetById), new { id = stockItem.Id }, MapToDto(created!));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<StockItemDto>> Update(int id, UpdateStockItemDto dto)
    {
        var item = await _stockItemService.GetByIdAsync(id);
        if (item == null)
            return NotFound();

        if (dto.Length != null)
        {
            try
            {
                item.LengthInches = (decimal)ArchUnits.ParseToInches(dto.Length);
            }
            catch
            {
                return BadRequest($"Invalid length format: {dto.Length}");
            }
        }

        if (dto.Name != null) item.Name = dto.Name;
        if (dto.Notes != null) item.Notes = dto.Notes;

        await _stockItemService.UpdateAsync(item);
        return Ok(MapToDto(item));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _stockItemService.GetByIdAsync(id);
        if (item == null)
            return NotFound();

        await _stockItemService.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("by-material/{materialId}")]
    public async Task<ActionResult<List<StockItemDto>>> GetByMaterial(int materialId)
    {
        var items = await _stockItemService.GetByMaterialAsync(materialId);
        return Ok(items.Select(MapToDto).ToList());
    }

    [HttpGet("{id}/transactions")]
    public async Task<ActionResult<List<StockTransactionDto>>> GetTransactions(int id, [FromQuery] int? limit = null)
    {
        var item = await _stockItemService.GetByIdAsync(id);
        if (item == null)
            return NotFound();

        var transactions = await _stockItemService.GetTransactionHistoryAsync(id, limit);
        return Ok(transactions.Select(MapTransactionToDto).ToList());
    }

    [HttpPost("{id}/receive")]
    public async Task<ActionResult<StockTransactionDto>> ReceiveStock(int id, AddStockDto dto)
    {
        try
        {
            var transaction = await _stockItemService.AddStockAsync(id, dto.Quantity, dto.Notes);
            return Ok(MapTransactionToDto(transaction));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id}/use")]
    public async Task<ActionResult<StockTransactionDto>> UseStock(int id, UseStockDto dto)
    {
        try
        {
            var transaction = await _stockItemService.UseStockAsync(id, dto.Quantity, dto.JobId, dto.Notes);
            return Ok(MapTransactionToDto(transaction));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id}/adjust")]
    public async Task<ActionResult<StockTransactionDto>> AdjustStock(int id, AdjustStockDto dto)
    {
        try
        {
            var transaction = await _stockItemService.AdjustStockAsync(id, dto.NewQuantity, dto.Notes);
            return Ok(MapTransactionToDto(transaction));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id}/scrap")]
    public async Task<ActionResult<StockTransactionDto>> ScrapStock(int id, ScrapStockDto dto)
    {
        try
        {
            var transaction = await _stockItemService.ScrapStockAsync(id, dto.Quantity, dto.Notes);
            return Ok(MapTransactionToDto(transaction));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id}/recalculate")]
    public async Task<ActionResult<object>> RecalculateStock(int id)
    {
        try
        {
            var newQuantity = await _stockItemService.RecalculateQuantityAsync(id);
            return Ok(new { QuantityOnHand = newQuantity });
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    private static StockItemDto MapToDto(StockItem s) => new()
    {
        Id = s.Id,
        MaterialId = s.MaterialId,
        MaterialName = s.Material?.DisplayName ?? string.Empty,
        LengthInches = s.LengthInches,
        LengthFormatted = ArchUnits.FormatFromInches((double)s.LengthInches),
        Name = s.Name,
        QuantityOnHand = s.QuantityOnHand,
        Notes = s.Notes,
        IsActive = s.IsActive
    };

    private static StockTransactionDto MapTransactionToDto(StockTransaction t) => new()
    {
        Id = t.Id,
        StockItemId = t.StockItemId,
        Quantity = t.Quantity,
        Type = t.Type.ToString(),
        JobId = t.JobId,
        JobNumber = t.Job?.JobNumber,
        Notes = t.Notes,
        CreatedAt = t.CreatedAt
    };
}
