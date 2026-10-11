using CutList.Web.Data.Entities;
using CutList.Web.DTOs;
using CutList.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CutList.Web.Controllers;

[ApiController]
[Route("api/part-sets")]
public class PartSetsController(PartSetService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PartSetDto>>> GetAll(string? search = null, bool includeArchived = false) =>
        Ok((await service.GetAllAsync(search, includeArchived)).Select(MapSummary).ToList());

    [HttpGet("{id}")]
    public async Task<ActionResult<PartSetDetailDto>> GetById(int id)
    {
        var set = await service.GetByIdAsync(id);
        return set == null ? NotFound() : Ok(MapDetail(set));
    }

    [HttpPost]
    public async Task<ActionResult<PartSetDetailDto>> Create(SavePartSetDto dto)
    {
        if (dto.Members.Any(m => m is null)) return BadRequest("Members cannot contain null rows.");
        try
        {
            var set = await service.CreateAsync(ToEntity(dto));
            return CreatedAtAction(nameof(GetById), new { id = set.Id }, MapDetail((await service.GetByIdAsync(set.Id))!));
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PartSetDetailDto>> Update(int id, SavePartSetDto dto)
    {
        if (dto.Members.Any(m => m is null)) return BadRequest("Members cannot contain null rows.");
        try
        {
            await service.UpdateAsync(ToEntity(dto, id));
            return Ok(MapDetail((await service.GetByIdAsync(id))!));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id}/archive")]
    public async Task<IActionResult> Archive(int id)
    {
        try { await service.ArchiveAsync(id); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    private static PartSet ToEntity(SavePartSetDto dto, int id = 0) => new()
    {
        Id = id, DrawingNumber = dto.DrawingNumber, Revision = dto.Revision, DisplayName = dto.DisplayName,
        Members = dto.Members.Select(m => new PartSetMember
        {
            Name = m.Name, SourceItem = m.SourceItem, SourceFile = m.SourceFile,
            MaterialId = m.MaterialId, LengthInches = m.LengthInches, Quantity = m.Quantity, SortOrder = m.SortOrder
        }).ToList()
    };

    private static PartSetDto MapSummary(PartSet set) => new()
    {
        Id = set.Id, DrawingNumber = set.DrawingNumber, Revision = set.Revision,
        DisplayName = set.DisplayName, IsArchived = set.IsArchived
    };

    private static PartSetDetailDto MapDetail(PartSet set) => new()
    {
        Id = set.Id, DrawingNumber = set.DrawingNumber, Revision = set.Revision,
        DisplayName = set.DisplayName, IsArchived = set.IsArchived,
        Members = set.Members.OrderBy(m => m.SortOrder).ThenBy(m => m.Id).Select(m => new PartSetMemberDto
        {
            Id = m.Id, Name = m.Name, SourceItem = m.SourceItem, SourceFile = m.SourceFile,
            MaterialId = m.MaterialId, MaterialName = m.Material?.DisplayName ?? string.Empty,
            LengthInches = m.LengthInches, Quantity = m.Quantity, SortOrder = m.SortOrder
        }).ToList()
    };
}
