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

    [HttpPost("alro-1018-round")]
    public async Task<ActionResult> SeedAlro1018Round()
    {
        // Add Alro supplier if not exists
        var alro = await _context.Suppliers.FirstOrDefaultAsync(s => s.Name == "Alro");
        if (alro == null)
        {
            alro = new Supplier
            {
                Name = "Alro",
                ContactInfo = "https://www.alro.com",
                CreatedAt = DateTime.UtcNow
            };
            _context.Suppliers.Add(alro);
            await _context.SaveChangesAsync();
        }

        // 1018 CF Round bar sizes from the screenshot
        var sizes = new[]
        {
            "1/8\"",
            "5/32\"",
            "3/16\"",
            "7/32\"",
            ".236\"",
            "1/4\"",
            "9/32\"",
            "5/16\"",
            "11/32\"",
            "3/8\"",
            ".394\"",
            "13/32\"",
            "7/16\"",
            "15/32\"",
            ".472\"",
            "1/2\"",
            "17/32\"",
            "9/16\"",
            ".593\""
        };

        var created = 0;
        var skipped = 0;

        foreach (var size in sizes)
        {
            var exists = await _context.Materials
                .AnyAsync(m => m.Shape == "Round Bar" && m.Size == size && m.IsActive);

            if (exists)
            {
                skipped++;
                continue;
            }

            _context.Materials.Add(new Material
            {
                Shape = "Round Bar",
                Size = size,
                Description = "1018 Cold Finished",
                CreatedAt = DateTime.UtcNow
            });
            created++;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            Message = "Alro 1018 CF Round materials seeded",
            SupplierId = alro.Id,
            MaterialsCreated = created,
            MaterialsSkipped = skipped
        });
    }
}
