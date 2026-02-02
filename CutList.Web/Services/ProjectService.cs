using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class ProjectService
{
    private readonly ApplicationDbContext _context;

    public ProjectService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Project>> GetAllAsync()
    {
        return await _context.Projects
            .Include(p => p.CuttingTool)
            .Include(p => p.Parts)
                .ThenInclude(pt => pt.Material)
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Project?> GetByIdAsync(int id)
    {
        return await _context.Projects
            .Include(p => p.CuttingTool)
            .Include(p => p.Parts.OrderBy(pt => pt.SortOrder))
                .ThenInclude(pt => pt.Material)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Project> CreateAsync(Project project)
    {
        project.CreatedAt = DateTime.UtcNow;
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        return project;
    }

    public async Task UpdateAsync(Project project)
    {
        project.UpdatedAt = DateTime.UtcNow;
        _context.Projects.Update(project);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project != null)
        {
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Project> DuplicateAsync(int id)
    {
        var original = await GetByIdAsync(id);
        if (original == null)
        {
            throw new ArgumentException("Project not found", nameof(id));
        }

        var duplicate = new Project
        {
            Name = $"{original.Name} (Copy)",
            Customer = original.Customer,
            CuttingToolId = original.CuttingToolId,
            Notes = original.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _context.Projects.Add(duplicate);
        await _context.SaveChangesAsync();

        // Copy parts
        foreach (var part in original.Parts)
        {
            _context.ProjectParts.Add(new ProjectPart
            {
                ProjectId = duplicate.Id,
                MaterialId = part.MaterialId,
                Name = part.Name,
                LengthInches = part.LengthInches,
                Quantity = part.Quantity,
                SortOrder = part.SortOrder
            });
        }

        await _context.SaveChangesAsync();
        return duplicate;
    }

    // Parts management
    public async Task<ProjectPart> AddPartAsync(ProjectPart part)
    {
        var maxOrder = await _context.ProjectParts
            .Where(p => p.ProjectId == part.ProjectId)
            .MaxAsync(p => (int?)p.SortOrder) ?? -1;
        part.SortOrder = maxOrder + 1;

        _context.ProjectParts.Add(part);
        await _context.SaveChangesAsync();

        // Update project timestamp
        var project = await _context.Projects.FindAsync(part.ProjectId);
        if (project != null)
        {
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return part;
    }

    public async Task UpdatePartAsync(ProjectPart part)
    {
        _context.ProjectParts.Update(part);
        await _context.SaveChangesAsync();

        var project = await _context.Projects.FindAsync(part.ProjectId);
        if (project != null)
        {
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeletePartAsync(int id)
    {
        var part = await _context.ProjectParts.FindAsync(id);
        if (part != null)
        {
            var projectId = part.ProjectId;
            _context.ProjectParts.Remove(part);
            await _context.SaveChangesAsync();

            var project = await _context.Projects.FindAsync(projectId);
            if (project != null)
            {
                project.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }

    // Cutting tools
    public async Task<List<CuttingTool>> GetCuttingToolsAsync(bool includeInactive = false)
    {
        var query = _context.CuttingTools.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }
        return await query.OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<CuttingTool?> GetCuttingToolByIdAsync(int id)
    {
        return await _context.CuttingTools.FindAsync(id);
    }

    public async Task<CuttingTool?> GetDefaultCuttingToolAsync()
    {
        return await _context.CuttingTools.FirstOrDefaultAsync(t => t.IsDefault && t.IsActive);
    }

    public async Task<CuttingTool> CreateCuttingToolAsync(CuttingTool tool)
    {
        if (tool.IsDefault)
        {
            // Clear other defaults
            var others = await _context.CuttingTools.Where(t => t.IsDefault).ToListAsync();
            foreach (var other in others)
            {
                other.IsDefault = false;
            }
        }

        _context.CuttingTools.Add(tool);
        await _context.SaveChangesAsync();
        return tool;
    }

    public async Task UpdateCuttingToolAsync(CuttingTool tool)
    {
        if (tool.IsDefault)
        {
            var others = await _context.CuttingTools.Where(t => t.IsDefault && t.Id != tool.Id).ToListAsync();
            foreach (var other in others)
            {
                other.IsDefault = false;
            }
        }

        _context.CuttingTools.Update(tool);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteCuttingToolAsync(int id)
    {
        var tool = await _context.CuttingTools.FindAsync(id);
        if (tool != null)
        {
            tool.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }
}
