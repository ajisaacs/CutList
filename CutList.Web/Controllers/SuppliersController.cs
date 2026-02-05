using CutList.Core.Formatting;
using CutList.Web.Data.Entities;
using CutList.Web.DTOs;
using CutList.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CutList.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuppliersController : ControllerBase
{
    private readonly SupplierService _supplierService;

    public SuppliersController(SupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpGet]
    public async Task<ActionResult<List<SupplierDto>>> GetAll([FromQuery] bool includeInactive = false)
    {
        var suppliers = await _supplierService.GetAllAsync(includeInactive);
        return Ok(suppliers.Select(MapToDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SupplierDto>> GetById(int id)
    {
        var supplier = await _supplierService.GetByIdAsync(id);
        if (supplier == null)
            return NotFound();

        return Ok(MapToDto(supplier));
    }

    [HttpPost]
    public async Task<ActionResult<SupplierDto>> Create(CreateSupplierDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Name is required");

        var supplier = new Supplier
        {
            Name = dto.Name,
            ContactInfo = dto.ContactInfo,
            Notes = dto.Notes
        };

        await _supplierService.CreateAsync(supplier);
        return CreatedAtAction(nameof(GetById), new { id = supplier.Id }, MapToDto(supplier));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SupplierDto>> Update(int id, UpdateSupplierDto dto)
    {
        var supplier = await _supplierService.GetByIdAsync(id);
        if (supplier == null)
            return NotFound();

        if (dto.Name != null) supplier.Name = dto.Name;
        if (dto.ContactInfo != null) supplier.ContactInfo = dto.ContactInfo;
        if (dto.Notes != null) supplier.Notes = dto.Notes;

        await _supplierService.UpdateAsync(supplier);
        return Ok(MapToDto(supplier));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var supplier = await _supplierService.GetByIdAsync(id);
        if (supplier == null)
            return NotFound();

        await _supplierService.DeleteAsync(id);
        return NoContent();
    }

    // --- Offerings ---

    [HttpGet("{id}/offerings")]
    public async Task<ActionResult<List<OfferingDto>>> GetOfferings(int id)
    {
        var supplier = await _supplierService.GetByIdAsync(id);
        if (supplier == null)
            return NotFound();

        var offerings = await _supplierService.GetOfferingsForSupplierAsync(id);
        return Ok(offerings.Select(MapOfferingToDto).ToList());
    }

    [HttpPost("{id}/offerings")]
    public async Task<ActionResult<OfferingDto>> CreateOffering(int id, CreateOfferingDto dto)
    {
        var supplier = await _supplierService.GetByIdAsync(id);
        if (supplier == null)
            return NotFound();

        var exists = await _supplierService.OfferingExistsAsync(id, dto.StockItemId);
        if (exists)
            return Conflict("An offering for this supplier and stock item already exists");

        var offering = new SupplierOffering
        {
            SupplierId = id,
            StockItemId = dto.StockItemId,
            PartNumber = dto.PartNumber,
            SupplierDescription = dto.SupplierDescription,
            Price = dto.Price,
            Notes = dto.Notes
        };

        await _supplierService.AddOfferingAsync(offering);

        // Reload with includes
        var created = await _supplierService.GetOfferingByIdAsync(offering.Id);
        return CreatedAtAction(nameof(GetOfferings), new { id }, MapOfferingToDto(created!));
    }

    [HttpPut("{supplierId}/offerings/{offeringId}")]
    public async Task<ActionResult<OfferingDto>> UpdateOffering(int supplierId, int offeringId, UpdateOfferingDto dto)
    {
        var offering = await _supplierService.GetOfferingByIdAsync(offeringId);
        if (offering == null || offering.SupplierId != supplierId)
            return NotFound();

        if (dto.PartNumber != null) offering.PartNumber = dto.PartNumber;
        if (dto.SupplierDescription != null) offering.SupplierDescription = dto.SupplierDescription;
        if (dto.Price.HasValue) offering.Price = dto.Price;
        if (dto.Notes != null) offering.Notes = dto.Notes;

        await _supplierService.UpdateOfferingAsync(offering);
        return Ok(MapOfferingToDto(offering));
    }

    [HttpDelete("{supplierId}/offerings/{offeringId}")]
    public async Task<IActionResult> DeleteOffering(int supplierId, int offeringId)
    {
        var offering = await _supplierService.GetOfferingByIdAsync(offeringId);
        if (offering == null || offering.SupplierId != supplierId)
            return NotFound();

        await _supplierService.DeleteOfferingAsync(offeringId);
        return NoContent();
    }

    private static SupplierDto MapToDto(Supplier s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        ContactInfo = s.ContactInfo,
        Notes = s.Notes,
        IsActive = s.IsActive
    };

    private static OfferingDto MapOfferingToDto(SupplierOffering o) => new()
    {
        Id = o.Id,
        SupplierId = o.SupplierId,
        SupplierName = o.Supplier?.Name,
        StockItemId = o.StockItemId,
        MaterialName = o.StockItem?.Material?.DisplayName,
        LengthInches = o.StockItem?.LengthInches,
        LengthFormatted = o.StockItem != null ? ArchUnits.FormatFromInches((double)o.StockItem.LengthInches) : null,
        PartNumber = o.PartNumber,
        SupplierDescription = o.SupplierDescription,
        Price = o.Price,
        Notes = o.Notes,
        IsActive = o.IsActive
    };
}
