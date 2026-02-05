using CutList.Web.Data.Entities;
using CutList.Web.DTOs;
using CutList.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CutList.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaterialsController : ControllerBase
{
    private readonly MaterialService _materialService;

    public MaterialsController(MaterialService materialService)
    {
        _materialService = materialService;
    }

    [HttpGet]
    public async Task<ActionResult<List<MaterialDto>>> GetMaterials(
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? shape = null)
    {
        List<Material> materials;

        if (!string.IsNullOrWhiteSpace(shape))
        {
            var parsedShape = MaterialShapeExtensions.ParseShape(shape);
            if (!parsedShape.HasValue)
                return BadRequest($"Unknown shape: {shape}");

            materials = await _materialService.GetByShapeAsync(parsedShape.Value, includeInactive);
        }
        else
        {
            materials = await _materialService.GetAllAsync(includeInactive);
        }

        return Ok(materials.Select(MapToDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<MaterialDto>> GetMaterial(int id)
    {
        var material = await _materialService.GetByIdAsync(id);
        if (material == null)
            return NotFound();

        return Ok(MapToDto(material));
    }

    [HttpGet("by-shape/{shape}")]
    public async Task<ActionResult<List<MaterialDto>>> GetByShape(string shape, [FromQuery] bool includeInactive = false)
    {
        var parsedShape = MaterialShapeExtensions.ParseShape(shape);
        if (!parsedShape.HasValue)
            return BadRequest($"Unknown shape: {shape}");

        var materials = await _materialService.GetByShapeAsync(parsedShape.Value, includeInactive);
        return Ok(materials.Select(MapToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<MaterialDto>> CreateMaterial(CreateMaterialDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Shape))
            return BadRequest("Shape is required");

        var parsedShape = MaterialShapeExtensions.ParseShape(dto.Shape);
        if (!parsedShape.HasValue)
            return BadRequest($"Unknown shape: {dto.Shape}");

        // Parse material type
        MaterialType materialType = MaterialType.Steel;
        if (!string.IsNullOrWhiteSpace(dto.Type))
        {
            if (!Enum.TryParse<MaterialType>(dto.Type, true, out materialType))
                return BadRequest($"Unknown material type: {dto.Type}");
        }

        var material = new Material
        {
            Shape = parsedShape.Value,
            Type = materialType,
            Grade = dto.Grade,
            Size = dto.Size ?? string.Empty,
            Description = dto.Description
        };

        if (dto.Dimensions != null && dto.Dimensions.Count > 0)
        {
            var dimensions = MaterialService.CreateDimensionsForShape(parsedShape.Value);
            ApplyDimensionValues(dimensions, dto.Dimensions);

            // Check for duplicates using generated size
            var generatedSize = dimensions.GenerateSizeString();
            var exists = await _materialService.ExistsAsync(parsedShape.Value, generatedSize);
            if (exists)
                return Conflict($"Material '{parsedShape.Value.GetDisplayName()} - {generatedSize}' already exists");

            var created = await _materialService.CreateWithDimensionsAsync(material, dimensions);
            return CreatedAtAction(nameof(GetMaterial), new { id = created.Id }, MapToDto(created));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(material.Size))
                return BadRequest("Size is required when dimensions are not provided");

            var exists = await _materialService.ExistsAsync(parsedShape.Value, material.Size);
            if (exists)
                return Conflict($"Material '{parsedShape.Value.GetDisplayName()} - {material.Size}' already exists");

            var created = await _materialService.CreateAsync(material);
            return CreatedAtAction(nameof(GetMaterial), new { id = created.Id }, MapToDto(created));
        }
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<BulkCreateResult>> CreateMaterialsBulk(List<CreateMaterialDto> materials)
    {
        var created = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var dto in materials)
        {
            if (string.IsNullOrWhiteSpace(dto.Shape))
            {
                errors.Add("Invalid material: Shape is required");
                continue;
            }

            var parsedShape = MaterialShapeExtensions.ParseShape(dto.Shape);
            if (!parsedShape.HasValue)
            {
                errors.Add($"Unknown shape: {dto.Shape}");
                continue;
            }

            var size = dto.Size ?? string.Empty;

            if (dto.Dimensions != null && dto.Dimensions.Count > 0)
            {
                var dimensions = MaterialService.CreateDimensionsForShape(parsedShape.Value);
                ApplyDimensionValues(dimensions, dto.Dimensions);
                size = dimensions.GenerateSizeString();
            }

            if (string.IsNullOrWhiteSpace(size))
            {
                errors.Add($"Size is required for {dto.Shape}");
                continue;
            }

            var exists = await _materialService.ExistsAsync(parsedShape.Value, size);
            if (exists)
            {
                skipped++;
                continue;
            }

            MaterialType materialType = MaterialType.Steel;
            if (!string.IsNullOrWhiteSpace(dto.Type))
            {
                if (!Enum.TryParse<MaterialType>(dto.Type, true, out materialType))
                {
                    errors.Add($"Unknown material type: {dto.Type}");
                    continue;
                }
            }

            var material = new Material
            {
                Shape = parsedShape.Value,
                Type = materialType,
                Grade = dto.Grade,
                Size = size,
                Description = dto.Description
            };

            if (dto.Dimensions != null && dto.Dimensions.Count > 0)
            {
                var dimensions = MaterialService.CreateDimensionsForShape(parsedShape.Value);
                ApplyDimensionValues(dimensions, dto.Dimensions);
                await _materialService.CreateWithDimensionsAsync(material, dimensions);
            }
            else
            {
                await _materialService.CreateAsync(material);
            }

            created++;
        }

        return Ok(new BulkCreateResult
        {
            Created = created,
            Skipped = skipped,
            Errors = errors
        });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<MaterialDto>> UpdateMaterial(int id, UpdateMaterialDto dto)
    {
        var material = await _materialService.GetByIdAsync(id);
        if (material == null)
            return NotFound();

        if (dto.Type != null)
        {
            if (!Enum.TryParse<MaterialType>(dto.Type, true, out var materialType))
                return BadRequest($"Unknown material type: {dto.Type}");
            material.Type = materialType;
        }

        if (dto.Grade != null) material.Grade = dto.Grade;
        if (dto.Size != null) material.Size = dto.Size;
        if (dto.Description != null) material.Description = dto.Description;

        if (dto.Dimensions != null && dto.Dimensions.Count > 0)
        {
            var dimensions = material.Dimensions ?? MaterialService.CreateDimensionsForShape(material.Shape);
            ApplyDimensionValues(dimensions, dto.Dimensions);
            await _materialService.UpdateWithDimensionsAsync(material, dimensions, dto.RegenerateSize ?? false);
        }
        else
        {
            await _materialService.UpdateAsync(material);
        }

        var updated = await _materialService.GetByIdAsync(id);
        return Ok(MapToDto(updated!));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMaterial(int id)
    {
        var material = await _materialService.GetByIdAsync(id);
        if (material == null)
            return NotFound();

        await _materialService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("search")]
    public async Task<ActionResult<List<MaterialDto>>> SearchMaterials(MaterialSearchDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Shape))
            return BadRequest("Shape is required");

        var parsedShape = MaterialShapeExtensions.ParseShape(dto.Shape);
        if (!parsedShape.HasValue)
            return BadRequest($"Unknown shape: {dto.Shape}");

        var results = parsedShape.Value switch
        {
            MaterialShape.RoundBar => await _materialService.SearchRoundBarByDiameterAsync(dto.TargetValue, dto.Tolerance),
            MaterialShape.RoundTube => await _materialService.SearchRoundTubeByODAsync(dto.TargetValue, dto.Tolerance),
            MaterialShape.FlatBar => await _materialService.SearchFlatBarByWidthAsync(dto.TargetValue, dto.Tolerance),
            MaterialShape.SquareBar => await _materialService.SearchSquareBarBySizeAsync(dto.TargetValue, dto.Tolerance),
            MaterialShape.SquareTube => await _materialService.SearchSquareTubeBySizeAsync(dto.TargetValue, dto.Tolerance),
            MaterialShape.RectangularTube => await _materialService.SearchRectangularTubeByWidthAsync(dto.TargetValue, dto.Tolerance),
            MaterialShape.Angle => await _materialService.SearchAngleByLegAsync(dto.TargetValue, dto.Tolerance),
            MaterialShape.Channel => await _materialService.SearchChannelByHeightAsync(dto.TargetValue, dto.Tolerance),
            MaterialShape.IBeam => await _materialService.SearchIBeamByHeightAsync(dto.TargetValue, dto.Tolerance),
            MaterialShape.Pipe => await _materialService.SearchPipeByNominalSizeAsync(dto.TargetValue, dto.Tolerance),
            _ => new List<Material>()
        };

        return Ok(results.Select(MapToDto).ToList());
    }

    private static MaterialDto MapToDto(Material m) => new()
    {
        Id = m.Id,
        Shape = m.Shape.GetDisplayName(),
        Type = m.Type.ToString(),
        Grade = m.Grade,
        Size = m.Size,
        Description = m.Description,
        IsActive = m.IsActive,
        Dimensions = m.Dimensions != null ? MapDimensionsToDto(m.Dimensions) : null
    };

    private static MaterialDimensionsDto MapDimensionsToDto(MaterialDimensions d)
    {
        var dto = new MaterialDimensionsDto
        {
            DimensionType = d.GetType().Name.Replace("Dimensions", "")
        };

        // Extract dimension values based on type
        switch (d)
        {
            case RoundBarDimensions rb:
                dto.Values["Diameter"] = rb.Diameter;
                break;
            case RoundTubeDimensions rt:
                dto.Values["OuterDiameter"] = rt.OuterDiameter;
                dto.Values["Wall"] = rt.Wall;
                break;
            case FlatBarDimensions fb:
                dto.Values["Width"] = fb.Width;
                dto.Values["Thickness"] = fb.Thickness;
                break;
            case SquareBarDimensions sb:
                dto.Values["Size"] = sb.Size;
                break;
            case SquareTubeDimensions st:
                dto.Values["Size"] = st.Size;
                dto.Values["Wall"] = st.Wall;
                break;
            case RectangularTubeDimensions rect:
                dto.Values["Width"] = rect.Width;
                dto.Values["Height"] = rect.Height;
                dto.Values["Wall"] = rect.Wall;
                break;
            case AngleDimensions a:
                dto.Values["Leg1"] = a.Leg1;
                dto.Values["Leg2"] = a.Leg2;
                dto.Values["Thickness"] = a.Thickness;
                break;
            case ChannelDimensions c:
                dto.Values["Height"] = c.Height;
                dto.Values["Flange"] = c.Flange;
                dto.Values["Web"] = c.Web;
                break;
            case IBeamDimensions ib:
                dto.Values["Height"] = ib.Height;
                dto.Values["WeightPerFoot"] = ib.WeightPerFoot;
                break;
            case PipeDimensions p:
                dto.Values["NominalSize"] = p.NominalSize;
                if (p.Wall.HasValue) dto.Values["Wall"] = p.Wall.Value;
                break;
        }

        return dto;
    }

    private static void ApplyDimensionValues(MaterialDimensions dimensions, Dictionary<string, decimal> values)
    {
        switch (dimensions)
        {
            case RoundBarDimensions rb:
                if (values.TryGetValue("Diameter", out var diameter)) rb.Diameter = diameter;
                break;
            case RoundTubeDimensions rt:
                if (values.TryGetValue("OuterDiameter", out var od)) rt.OuterDiameter = od;
                if (values.TryGetValue("Wall", out var rtWall)) rt.Wall = rtWall;
                break;
            case FlatBarDimensions fb:
                if (values.TryGetValue("Width", out var fbWidth)) fb.Width = fbWidth;
                if (values.TryGetValue("Thickness", out var fbThick)) fb.Thickness = fbThick;
                break;
            case SquareBarDimensions sb:
                if (values.TryGetValue("Size", out var sbSize)) sb.Size = sbSize;
                break;
            case SquareTubeDimensions st:
                if (values.TryGetValue("Size", out var stSize)) st.Size = stSize;
                if (values.TryGetValue("Wall", out var stWall)) st.Wall = stWall;
                break;
            case RectangularTubeDimensions rect:
                if (values.TryGetValue("Width", out var rectWidth)) rect.Width = rectWidth;
                if (values.TryGetValue("Height", out var rectHeight)) rect.Height = rectHeight;
                if (values.TryGetValue("Wall", out var rectWall)) rect.Wall = rectWall;
                break;
            case AngleDimensions a:
                if (values.TryGetValue("Leg1", out var leg1)) a.Leg1 = leg1;
                if (values.TryGetValue("Leg2", out var leg2)) a.Leg2 = leg2;
                if (values.TryGetValue("Thickness", out var aThick)) a.Thickness = aThick;
                break;
            case ChannelDimensions c:
                if (values.TryGetValue("Height", out var cHeight)) c.Height = cHeight;
                if (values.TryGetValue("Flange", out var flange)) c.Flange = flange;
                if (values.TryGetValue("Web", out var web)) c.Web = web;
                break;
            case IBeamDimensions ib:
                if (values.TryGetValue("Height", out var ibHeight)) ib.Height = ibHeight;
                if (values.TryGetValue("WeightPerFoot", out var weight)) ib.WeightPerFoot = weight;
                break;
            case PipeDimensions p:
                if (values.TryGetValue("NominalSize", out var nps)) p.NominalSize = nps;
                if (values.TryGetValue("Wall", out var pWall)) p.Wall = pWall;
                if (values.TryGetValue("Schedule", out var schedule)) p.Schedule = schedule.ToString();
                break;
        }
    }
}
