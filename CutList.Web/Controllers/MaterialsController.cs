using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaterialsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public MaterialsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<MaterialDto>>> GetMaterials()
    {
        var materials = await _context.Materials
            .Where(m => m.IsActive)
            .OrderBy(m => m.Shape)
            .ThenBy(m => m.Size)
            .Select(m => new MaterialDto
            {
                Id = m.Id,
                Shape = m.Shape.GetDisplayName(),
                Size = m.Size,
                Description = m.Description
            })
            .ToListAsync();

        return Ok(materials);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MaterialDto>> GetMaterial(int id)
    {
        var material = await _context.Materials.FindAsync(id);

        if (material == null || !material.IsActive)
            return NotFound();

        return Ok(new MaterialDto
        {
            Id = material.Id,
            Shape = material.Shape.GetDisplayName(),
            Size = material.Size,
            Description = material.Description
        });
    }

    [HttpPost]
    public async Task<ActionResult<MaterialDto>> CreateMaterial(CreateMaterialDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Shape))
            return BadRequest("Shape is required");

        if (string.IsNullOrWhiteSpace(dto.Size))
            return BadRequest("Size is required");

        var parsedShape = MaterialShapeExtensions.ParseShape(dto.Shape);
        if (!parsedShape.HasValue)
            return BadRequest($"Unknown shape: {dto.Shape}");

        // Check for duplicates
        var exists = await _context.Materials
            .AnyAsync(m => m.Shape == parsedShape.Value && m.Size == dto.Size && m.IsActive);

        if (exists)
            return Conflict($"Material '{dto.Shape} - {dto.Size}' already exists");

        var material = new Material
        {
            Shape = parsedShape.Value,
            Size = dto.Size,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow
        };

        _context.Materials.Add(material);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMaterial), new { id = material.Id }, new MaterialDto
        {
            Id = material.Id,
            Shape = material.Shape.GetDisplayName(),
            Size = material.Size,
            Description = material.Description
        });
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<BulkCreateResult>> CreateMaterialsBulk(List<CreateMaterialDto> materials)
    {
        var created = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var dto in materials)
        {
            if (string.IsNullOrWhiteSpace(dto.Shape) || string.IsNullOrWhiteSpace(dto.Size))
            {
                errors.Add($"Invalid material: Shape and Size are required");
                continue;
            }

            var parsedShape = MaterialShapeExtensions.ParseShape(dto.Shape);
            if (!parsedShape.HasValue)
            {
                errors.Add($"Unknown shape: {dto.Shape}");
                continue;
            }

            var exists = await _context.Materials
                .AnyAsync(m => m.Shape == parsedShape.Value && m.Size == dto.Size && m.IsActive);

            if (exists)
            {
                skipped++;
                continue;
            }

            _context.Materials.Add(new Material
            {
                Shape = parsedShape.Value,
                Size = dto.Size,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow
            });
            created++;
        }

        await _context.SaveChangesAsync();

        return Ok(new BulkCreateResult
        {
            Created = created,
            Skipped = skipped,
            Errors = errors
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMaterial(int id)
    {
        var material = await _context.Materials.FindAsync(id);

        if (material == null)
            return NotFound();

        material.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class MaterialDto
{
    public int Id { get; set; }
    public string Shape { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CreateMaterialDto
{
    public string Shape { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class BulkCreateResult
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; set; } = new();
}
