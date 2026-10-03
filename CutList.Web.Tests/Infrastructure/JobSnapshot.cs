using System.Text.Json;
using CutList.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CutList.Web.Tests.Infrastructure;

public sealed record JobRow(
    int Id, string JobNumber, string? Name, string? Customer, int? CuttingToolId, string? Notes,
    DateTime CreatedAt, DateTime? UpdatedAt, DateTime? LockedAt, string? OptimizationResultJson, DateTime? OptimizedAt);

public sealed record PartRow(int Id, int JobId, int MaterialId, string Name, decimal LengthInches, int Quantity, int SortOrder);

public sealed record StockRow(
    int Id, int JobId, int MaterialId, int? StockItemId, decimal LengthInches, int Quantity,
    bool IsCustomLength, int Priority, int SortOrder);

public sealed record MaterialRow(
    int Id, string Shape, string Type, string? Grade, string Size, string? Description, bool IsActive,
    int SortOrder, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record StockItemRow(
    int Id, int MaterialId, decimal LengthInches, string? Name, string? Notes, bool IsActive,
    DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record CuttingToolRow(int Id, string Name, decimal KerfInches, bool IsDefault, bool IsActive);

/// <summary>
/// Canonical fresh-context snapshot of every job, job child, and referenced catalog row in the
/// disposable test database. Comparing two snapshots proves a rejected operation changed nothing,
/// including other jobs and catalog data.
/// </summary>
public sealed record JobSnapshot(
    IReadOnlyList<JobRow> Jobs,
    IReadOnlyList<PartRow> Parts,
    IReadOnlyList<StockRow> Stock,
    IReadOnlyList<MaterialRow> Materials,
    IReadOnlyList<StockItemRow> StockItems,
    IReadOnlyList<CuttingToolRow> CuttingTools)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task<JobSnapshot> CaptureAsync(IServiceProvider services)
    {
        var factory = services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var context = await factory.CreateDbContextAsync();

        var jobs = await context.Jobs.AsNoTracking().OrderBy(j => j.Id)
            .Select(j => new JobRow(j.Id, j.JobNumber, j.Name, j.Customer, j.CuttingToolId, j.Notes,
                j.CreatedAt, j.UpdatedAt, j.LockedAt, j.OptimizationResultJson, j.OptimizedAt))
            .ToListAsync();
        var parts = await context.JobParts.AsNoTracking().OrderBy(p => p.Id)
            .Select(p => new PartRow(p.Id, p.JobId, p.MaterialId, p.Name, p.LengthInches, p.Quantity, p.SortOrder))
            .ToListAsync();
        var stock = await context.JobStocks.AsNoTracking().OrderBy(s => s.Id)
            .Select(s => new StockRow(s.Id, s.JobId, s.MaterialId, s.StockItemId, s.LengthInches, s.Quantity,
                s.IsCustomLength, s.Priority, s.SortOrder))
            .ToListAsync();
        var materials = (await context.Materials.AsNoTracking().OrderBy(m => m.Id).ToListAsync())
            .Select(m => new MaterialRow(m.Id, m.Shape.ToString(), m.Type.ToString(), m.Grade, m.Size, m.Description,
                m.IsActive, m.SortOrder, m.CreatedAt, m.UpdatedAt))
            .ToList();
        var stockItems = await context.StockItems.AsNoTracking().OrderBy(s => s.Id)
            .Select(s => new StockItemRow(s.Id, s.MaterialId, s.LengthInches, s.Name, s.Notes, s.IsActive,
                s.CreatedAt, s.UpdatedAt))
            .ToListAsync();
        var tools = await context.CuttingTools.AsNoTracking().OrderBy(t => t.Id)
            .Select(t => new CuttingToolRow(t.Id, t.Name, t.KerfInches, t.IsDefault, t.IsActive))
            .ToListAsync();

        return new JobSnapshot(jobs, parts, stock, materials, stockItems, tools);
    }

    public JobRow Job(int id) => Jobs.Single(j => j.Id == id);

    public IReadOnlyList<PartRow> PartsOf(int jobId) => Parts.Where(p => p.JobId == jobId).ToList();

    public IReadOnlyList<StockRow> StockOf(int jobId) => Stock.Where(s => s.JobId == jobId).ToList();

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Asserts that two snapshots are identical, reporting the full JSON on mismatch.</summary>
    public static void AssertUnchanged(JobSnapshot before, JobSnapshot after) =>
        Assert.Equal(before.ToJson(), after.ToJson());

    public static Task<JobSnapshot> CaptureAsync(SqlServerFixture fixture) => CaptureAsync(fixture.Services);
}
