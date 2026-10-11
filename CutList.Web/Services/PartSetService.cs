using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class PartSetService(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<PartSet?> GetByIdAsync(int id)
    {
        await using var db = factory.CreateDbContext();
        return await db.PartSets.AsNoTracking().Include(s => s.Members.OrderBy(m => m.SortOrder))
            .ThenInclude(m => m.Material).FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<List<PartSet>> GetAllAsync(string? search = null, bool includeArchived = false)
    {
        await using var db = factory.CreateDbContext();
        var query = db.PartSets.AsNoTracking().AsQueryable();
        if (!includeArchived) query = query.Where(s => !s.IsArchived);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var key = search.Trim().ToUpperInvariant();
            query = query.Where(s => s.DrawingKey.Contains(key) || s.DisplayName.Contains(search.Trim()));
        }
        return await query.OrderBy(s => s.DrawingKey).ThenBy(s => s.RevisionKey).ToListAsync();
    }

    public async Task<PartSet> CreateAsync(PartSet input)
    {
        await using var db = factory.CreateDbContext();
        var set = await ValidatedCopyAsync(db, input);
        await RequireUniqueAsync(db, set, 0);
        db.PartSets.Add(set);
        await SaveAsync(db);
        return set;
    }

    public async Task UpdateAsync(PartSet input)
    {
        await using var db = factory.CreateDbContext();
        var stored = await db.PartSets.Include(s => s.Members).FirstOrDefaultAsync(s => s.Id == input.Id)
            ?? throw new KeyNotFoundException($"Part set {input.Id} was not found.");
        var copy = await ValidatedCopyAsync(db, input);
        await RequireUniqueAsync(db, copy, stored.Id);
        stored.DrawingNumber = copy.DrawingNumber; stored.Revision = copy.Revision;
        stored.DrawingKey = copy.DrawingKey; stored.RevisionKey = copy.RevisionKey;
        stored.DisplayName = copy.DisplayName;
        db.PartSetMembers.RemoveRange(stored.Members);
        stored.Members = copy.Members;
        await SaveAsync(db);
    }

    public async Task ArchiveAsync(int id)
    {
        await using var db = factory.CreateDbContext();
        var set = await db.PartSets.FindAsync(id)
            ?? throw new KeyNotFoundException($"Part set {id} was not found.");
        set.IsArchived = true;
        await db.SaveChangesAsync();
    }

    private static async Task RequireUniqueAsync(ApplicationDbContext db, PartSet set, int exceptId)
    {
        if (await db.PartSets.AnyAsync(s => s.Id != exceptId && s.DrawingKey == set.DrawingKey && s.RevisionKey == set.RevisionKey))
            throw new ArgumentException("A part set with this drawing number and revision already exists.");
    }

    private static async Task SaveAsync(ApplicationDbContext db)
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new ArgumentException("A part set with this drawing number and revision already exists.", ex); }
    }

    private static async Task<PartSet> ValidatedCopyAsync(ApplicationDbContext db, PartSet input)
    {
        var drawing = Required(input.DrawingNumber, 100, "Drawing number");
        var revision = input.Revision?.Trim();
        if (string.IsNullOrEmpty(revision)) revision = null;
        if (revision?.Length > 50) throw new ArgumentException("Revision exceeds 50 characters.");
        var result = new PartSet { DrawingNumber = drawing, Revision = revision,
            DrawingKey = drawing.ToUpperInvariant(), RevisionKey = revision?.ToUpperInvariant() ?? "",
            DisplayName = Required(input.DisplayName, 100, "Display name") };
        foreach (var row in input.Members.OrderBy(m => m.SortOrder))
        {
            ValidateRow(row);
            result.Members.Add(new PartSetMember { Name = Required(row.Name, 100, "Cut name"),
                SourceItem = Optional(row.SourceItem, 100), SourceFile = Optional(row.SourceFile, 255),
                MaterialId = row.MaterialId, LengthInches = row.LengthInches, Quantity = row.Quantity,
                SortOrder = result.Members.Count });
        }
        await ValidateMaterialsAsync(db, result.Members);
        return result;
    }

    internal static void ValidateRow(PartSetMember row)
    {
        Required(row.Name, 100, "Cut name");
        // JobPart uses decimal(10,4); reject values that SQL would round or overflow.
        if (row.LengthInches <= 0 || row.LengthInches > 999999.9999m || decimal.Round(row.LengthInches, 4) != row.LengthInches)
            throw new ArgumentException("Cut length must be positive and fit decimal(10,4).");
        if (row.Quantity <= 0) throw new ArgumentException("Per-set quantity must be positive.");
    }

    internal static async Task ValidateMaterialsAsync(ApplicationDbContext db, IEnumerable<PartSetMember> rows)
    {
        var ids = rows.Select(m => m.MaterialId).Distinct().ToArray();
        if (await db.Materials.CountAsync(m => ids.Contains(m.Id) && m.IsActive) != ids.Length)
            throw new ArgumentException("Every cut requires an active catalog material.");
    }

    private static string Required(string value, int max, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max)
            throw new ArgumentException($"{label} is required and must not exceed {max} characters.");
        return value.Trim();
    }
    private static string? Optional(string? value, int max)
    {
        if (value?.Length > max) throw new ArgumentException($"Source text exceeds {max} characters.");
        return value;
    }
}
