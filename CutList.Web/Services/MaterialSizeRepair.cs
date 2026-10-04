using System.Data;
using System.Globalization;
using System.Text.Json;
using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CutList.Web.Services;

public sealed record RepairTarget(string Server, string Database, Guid DatabaseGuid);
public sealed record RepairDimensions(int Id, int MaterialId, string Kind, SortedDictionary<string, string?> Values);
public sealed record MaterialRepairEntry(int MaterialId, string Shape, string Type, string? Grade,
    RepairDimensions? Dimensions, string OldSize, string NewSize, string Reason, string Status,
    DateTime? BaselineUpdatedAt);
public sealed record MaterialRepairManifest(int Version, string Kind, RepairTarget Target,
    DateTime CreatedAt, List<MaterialRepairEntry> Entries);
public sealed record AppliedMaterialRepair(MaterialRepairEntry Before, DateTime AppliedUpdatedAt);
public sealed record MaterialRepairRollback(int Version, string Kind, RepairTarget Target,
    DateTime CreatedAt, List<AppliedMaterialRepair> Entries);
public sealed class MaterialRepairRejectedException(string message) : Exception(message);

/// <summary>Invoked explicitly by the offline tool; never registered at application startup.</summary>
public sealed class MaterialSizeRepair(IDbContextFactory<ApplicationDbContext> factory)
{
    public async Task<MaterialRepairManifest> DryRunAsync(string expectedServer, string expectedDatabase)
    {
        await using var context = await factory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var target = await ConnectedTargetAsync(context);
        ValidateExpectedTarget(target, expectedServer, expectedDatabase);
        var (materials, dimensions) = await ReadAsync(context);
        var entries = materials.Select(m => Describe(m, dimensions.Where(d => d.MaterialId == m.Id).ToList())).ToList();
        var collisions = await CollisionsAsync(context, entries.Where(e => e.Status == "repair").ToList());
        entries = entries.Select(e => collisions.Contains(e.MaterialId)
            ? e with { Status = "collision", Reason = "Proposed shape/size collides under database collation (including inactive materials and other proposals)." } : e).ToList();
        await transaction.CommitAsync();
        return new(1, "dry-run", target, DateTime.UtcNow, entries);
    }

    public async Task<int> ApplyAsync(MaterialRepairManifest manifest, string expectedServer, string expectedDatabase,
        Func<MaterialRepairRollback, Task> persistBeforeImage)
    {
        ArgumentNullException.ThrowIfNull(persistBeforeImage);
        ValidateHeader(manifest.Version, manifest.Kind, "dry-run", manifest.Target, manifest.Entries);
        await using var context = await factory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var target = await ConnectedTargetAsync(context);
        ValidateExpectedTarget(target, expectedServer, expectedDatabase);
        if (target != manifest.Target) Reject("Manifest connected identity differs from current target.");
        var (materials, dimensions) = await ReadAsync(context);
        foreach (var entry in manifest.Entries)
        {
            var row = materials.SingleOrDefault(m => m.Id == entry.MaterialId);
            if (row == null) Reject("A reviewed material no longer exists.");
            var current = Describe(row!, dimensions.Where(d => d.MaterialId == entry.MaterialId).ToList());
            if (!Same(entry, current)) Reject("Reviewed material label, identity, dimensions, timestamp or repair proof changed.");
        }
        var repairs = manifest.Entries.Where(e => e.Status == "repair").ToList();
        if ((await CollisionsAsync(context, repairs)).Count != 0) Reject("Shape/size collision; no rows changed.");
        var changes = repairs.Select(e => new AppliedMaterialRepair(e, DateTime.UtcNow)).ToList();
        await persistBeforeImage(new(1, "rollback", target, DateTime.UtcNow, changes));
        foreach (var change in changes)
        {
            var row = materials.Single(m => m.Id == change.Before.MaterialId);
            row.Size = change.Before.NewSize;
            row.UpdatedAt = change.AppliedUpdatedAt;
        }
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return changes.Count;
    }

    public async Task<int> RollbackAsync(MaterialRepairRollback manifest, string expectedServer, string expectedDatabase)
    {
        ValidateHeader(manifest.Version, manifest.Kind, "rollback", manifest.Target, manifest.Entries?.Select(e => e.Before).ToList());
        await using var context = await factory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var target = await ConnectedTargetAsync(context);
        ValidateExpectedTarget(target, expectedServer, expectedDatabase);
        if (target != manifest.Target) Reject("Rollback connected identity differs from current target.");
        var (materials, dimensions) = await ReadAsync(context);
        foreach (var change in manifest.Entries!)
        {
            var entry = change.Before;
            var row = materials.SingleOrDefault(m => m.Id == entry.MaterialId);
            if (row == null || row.Size != entry.NewSize || row.UpdatedAt != change.AppliedUpdatedAt || change.AppliedUpdatedAt == default)
                Reject("Rollback CAS failed: current label or applied timestamp changed.");
            // Recompute proof from original label without touching the tracked database row.
            var original = new Material { Id = row!.Id, Shape = row.Shape, Type = row.Type, Grade = row.Grade,
                Size = entry.OldSize, UpdatedAt = entry.BaselineUpdatedAt };
            var baseline = Describe(original, dimensions.Where(d => d.MaterialId == entry.MaterialId).ToList());
            if (entry.Status != "repair" || !Same(entry, baseline)) Reject("Rollback identity, dimensions or historical proof changed.");
        }
        var restore = manifest.Entries!.Select(e => e.Before with { NewSize = e.Before.OldSize }).ToList();
        if ((await CollisionsAsync(context, restore)).Count != 0) Reject("Rollback shape/size collision; no rows changed.");
        foreach (var change in manifest.Entries!)
        {
            var row = materials.Single(m => m.Id == change.Before.MaterialId);
            row.Size = change.Before.OldSize;
            row.UpdatedAt = change.Before.BaselineUpdatedAt;
        }
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return manifest.Entries.Count;
    }

    private static void Reject(string message) => throw new MaterialRepairRejectedException(message);

    private static bool Same(MaterialRepairEntry a, MaterialRepairEntry b) =>
        JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);

    private static void ValidateHeader(int version, string kind, string expectedKind, RepairTarget target, List<MaterialRepairEntry>? entries)
    {
        if (version != 1 || kind != expectedKind || target == null || target.DatabaseGuid == Guid.Empty || entries == null ||
            entries.Any(e => e == null || e.MaterialId <= 0) || entries.Select(e => e.MaterialId).Distinct().Count() != entries.Count)
            Reject("Malformed or unsupported repair manifest.");
    }

    private static void ValidateExpectedTarget(RepairTarget target, string server, string database)
    {
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database) ||
            !string.Equals(target.Server, server, StringComparison.Ordinal) || !string.Equals(target.Database, database, StringComparison.Ordinal))
            Reject("Connected server/database does not exactly match the explicit expected target.");
    }

    // Read *all* rows under SERIALIZABLE, including inactive materials. SQL Server range locks
    // cover collision inserts and catalog/dimension edits until commit (no retry on deadlock).
    private static async Task<HashSet<int>> CollisionsAsync(ApplicationDbContext context, List<MaterialRepairEntry> entries)
    {
        var collisions = new HashSet<int>();
        foreach (var entry in entries)
        {
            if (!Enum.TryParse<MaterialShape>(entry.Shape, out var shape)) Reject("Invalid material shape.");
            if (await context.Materials.AnyAsync(m => m.Id != entry.MaterialId && m.Shape == shape && m.Size == entry.NewSize))
                collisions.Add(entry.MaterialId);
        }
        for (var i = 0; i < entries.Count; i++)
        for (var j = i + 1; j < entries.Count; j++)
        {
            if (entries[i].Shape != entries[j].Shape) continue;
            var a = entries[i].NewSize;
            var b = entries[j].NewSize;
            var equal = await context.Database.SqlQuery<int>($"SELECT CASE WHEN {a} COLLATE DATABASE_DEFAULT = {b} COLLATE DATABASE_DEFAULT THEN 1 ELSE 0 END AS [Value]").SingleAsync();
            if (equal != 0) { collisions.Add(entries[i].MaterialId); collisions.Add(entries[j].MaterialId); }
        }
        return collisions;
    }

    private static async Task<(List<Material>, List<MaterialDimensions>)> ReadAsync(ApplicationDbContext context) =>
        (await context.Materials.OrderBy(m => m.Id).ToListAsync(),
         await context.MaterialDimensions.AsNoTracking().OrderBy(d => d.Id).ToListAsync());

    private static async Task<RepairTarget> ConnectedTargetAsync(ApplicationDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = context.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')), DB_NAME(), service_broker_guid FROM sys.databases WHERE name = DB_NAME()";
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new MaterialRepairRejectedException("Connected target identity is unavailable.");
        return new(reader.GetString(0), reader.GetString(1), reader.GetGuid(2));
    }

    private static MaterialRepairEntry Describe(Material material, List<MaterialDimensions> dimensions)
    {
        var dimension = dimensions.Count == 1 ? dimensions[0] : null;
        var snapshot = dimension == null ? null : Capture(dimension);
        var status = "unchanged";
        var reason = "Unsupported shape; not inspected for repair.";
        var newSize = material.Size;
        if (Supported(material.Shape))
        {
            var oldGenerated = dimension == null ? null : HistoricalLabel(material.Shape, dimension);
            if (oldGenerated == null)
            {
                status = "missingdimensions";
                reason = "Missing, ambiguous, mismatched or nonpositive dimensions; skipped.";
            }
            else
            {
                var corrected = dimension!.GenerateSizeString();
                if (material.Size == corrected) reason = "Already matches current generator.";
                else if (material.Size == oldGenerated)
                {
                    status = "repair";
                    newSize = corrected;
                    reason = "Exact match to 613064b truncating generator with these dimensions.";
                }
                else
                {
                    status = "customname";
                    reason = "Not an exact historical-generated label; preserve authored/ambiguous name.";
                }
            }
        }
        return new(material.Id, material.Shape.ToString(), material.Type.ToString(), material.Grade,
            snapshot, material.Size, newSize, reason, status, material.UpdatedAt);
    }

    private static bool Supported(MaterialShape shape) => shape is MaterialShape.RoundTube or MaterialShape.SquareTube
        or MaterialShape.RectangularTube or MaterialShape.Channel;

    private static RepairDimensions Capture(MaterialDimensions dimensions)
    {
        var values = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var property in dimensions.GetType().GetProperties().Where(p => p.DeclaringType == dimensions.GetType()))
        {
            var value = property.GetValue(dimensions);
            values[property.Name] = value is decimal number ? number.ToString("G29", CultureInfo.InvariantCulture) : value?.ToString();
        }
        return new(dimensions.Id, dimensions.MaterialId, dimensions.GetType().Name, values);
    }

    private static string? HistoricalLabel(MaterialShape shape, MaterialDimensions dimensions) => (shape, dimensions) switch
    {
        (MaterialShape.RoundTube, RoundTubeDimensions d) when d.OuterDiameter > 0 && d.Wall > 0 && d.Wall * 2 < d.OuterDiameter =>
            $"{OldInches(d.OuterDiameter)} OD x {OldInches(d.Wall)} wall",
        (MaterialShape.SquareTube, SquareTubeDimensions d) when d.Size > 0 && d.Wall > 0 && d.Wall * 2 < d.Size =>
            $"{OldInches(d.Size)} x {OldInches(d.Wall)} wall",
        (MaterialShape.RectangularTube, RectangularTubeDimensions d) when d.Width > 0 && d.Height > 0 && d.Wall > 0 && d.Wall * 2 < Math.Min(d.Width, d.Height) =>
            $"{OldInches(d.Width)} x {OldInches(d.Height)} x {OldInches(d.Wall)} wall",
        (MaterialShape.Channel, ChannelDimensions d) when d.Height > 0 && d.Flange > 0 && d.Web > 0 && d.Web < Math.Min(d.Height, d.Flange) =>
            $"{OldInches(d.Height)} x {OldInches(d.Flange)} x {OldInches(d.Web)}",
        _ => null
    };

    // Faithful to 613064b: decimal -> double -> subtract feet -> decimal; truncate sixteenths.
    private static string OldInches(decimal value)
    {
        var total = (double)value;
        var feet = Math.Floor(total / 12.0);
        var inches = (decimal)(total - feet * 12.0);
        var whole = (int)inches;
        var numerator = (int)(Math.Abs(inches - whole) * 16);
        var denominator = 16;
        var a = numerator;
        var b = denominator;
        while (b != 0) (a, b) = (b, a % b);
        numerator /= a;
        denominator /= a;
        var fraction = numerator == 0 ? whole.ToString(CultureInfo.InvariantCulture)
            : whole == 0 ? $"{numerator}/{denominator}" : $"{whole}-{numerator}/{denominator}";
        return feet > 0 ? $"{feet.ToString(CultureInfo.InvariantCulture)}'  {fraction}\"" : $"{fraction}\"";
    }
}
