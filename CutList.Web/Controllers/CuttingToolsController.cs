using CutList.Web.Data.Entities;
using CutList.Web.DTOs;
using CutList.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CutList.Web.Controllers;

[ApiController]
[Route("api/cutting-tools")]
public class CuttingToolsController : ControllerBase
{
    private readonly JobService _jobService;

    public CuttingToolsController(JobService jobService)
    {
        _jobService = jobService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CuttingToolDto>>> GetAll([FromQuery] bool includeInactive = false)
    {
        var tools = await _jobService.GetCuttingToolsAsync(includeInactive);
        return Ok(tools.Select(MapToDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CuttingToolDto>> GetById(int id)
    {
        var tool = await _jobService.GetCuttingToolByIdAsync(id);
        if (tool == null)
            return NotFound();

        return Ok(MapToDto(tool));
    }

    [HttpGet("default")]
    public async Task<ActionResult<CuttingToolDto>> GetDefault()
    {
        var tool = await _jobService.GetDefaultCuttingToolAsync();
        if (tool == null)
            return NotFound();

        return Ok(MapToDto(tool));
    }

    [HttpPost]
    public async Task<ActionResult<CuttingToolDto>> Create(CreateCuttingToolDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Name is required");

        var tool = new CuttingTool
        {
            Name = dto.Name,
            KerfInches = dto.KerfInches,
            IsDefault = dto.IsDefault
        };

        await _jobService.CreateCuttingToolAsync(tool);
        return CreatedAtAction(nameof(GetById), new { id = tool.Id }, MapToDto(tool));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CuttingToolDto>> Update(int id, UpdateCuttingToolDto dto)
    {
        var tool = await _jobService.GetCuttingToolByIdAsync(id);
        if (tool == null)
            return NotFound();

        if (dto.Name != null) tool.Name = dto.Name;
        if (dto.KerfInches.HasValue) tool.KerfInches = dto.KerfInches.Value;
        if (dto.IsDefault.HasValue) tool.IsDefault = dto.IsDefault.Value;

        await _jobService.UpdateCuttingToolAsync(tool);
        return Ok(MapToDto(tool));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var tool = await _jobService.GetCuttingToolByIdAsync(id);
        if (tool == null)
            return NotFound();

        await _jobService.DeleteCuttingToolAsync(id);
        return NoContent();
    }

    private static CuttingToolDto MapToDto(CuttingTool tool) => new()
    {
        Id = tool.Id,
        Name = tool.Name,
        KerfInches = tool.KerfInches,
        IsDefault = tool.IsDefault,
        IsActive = tool.IsActive
    };
}
