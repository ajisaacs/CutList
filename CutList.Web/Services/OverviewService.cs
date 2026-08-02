using CutList.Web.Data;
using CutList.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CutList.Web.Services;

public class OverviewService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public OverviewService(IDbContextFactory<ApplicationDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<OverviewSnapshot> GetSnapshotAsync()
    {
        await using var context = _factory.CreateDbContext();

        var recentJobs = await context.Jobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Take(8)
            .Select(j => new RecentJobOverview(
                j.Id,
                j.JobNumber,
                j.Name,
                j.Customer,
                j.CreatedAt,
                j.LockedAt != null,
                j.Parts.Sum(p => (int?)p.Quantity) ?? 0))
            .ToListAsync();

        var frequentStock = await context.JobStocks
            .AsNoTracking()
            .GroupBy(s => new { s.MaterialId, s.LengthInches, s.Material.Shape, s.Material.Size })
            .Select(g => new
            {
                g.Key.MaterialId,
                g.Key.LengthInches,
                g.Key.Shape,
                g.Key.Size,
                JobCount = g.Select(s => s.JobId).Distinct().Count()
            })
            .OrderByDescending(s => s.JobCount)
            .ThenBy(s => s.Shape)
            .ThenBy(s => s.Size)
            .ThenBy(s => s.LengthInches)
            .Take(5)
            .ToListAsync();

        var totalJobs = await context.Jobs.CountAsync();
        var openJobs = await context.Jobs.CountAsync(j => j.LockedAt == null);
        var totalParts = await context.JobParts.SumAsync(p => (int?)p.Quantity) ?? 0;
        var activeStockItems = await context.StockItems.CountAsync(s => s.IsActive);

        return new OverviewSnapshot(
            totalJobs,
            openJobs,
            totalParts,
            activeStockItems,
            recentJobs,
            frequentStock.Select(s => new FrequentStockOverview(
                s.MaterialId,
                $"{s.Shape.GetDisplayName()} - {s.Size}",
                s.LengthInches,
                s.JobCount)).ToList());
    }
}

public record OverviewSnapshot(
    int TotalJobs,
    int OpenJobs,
    int TotalParts,
    int ActiveStockItems,
    IReadOnlyList<RecentJobOverview> RecentJobs,
    IReadOnlyList<FrequentStockOverview> FrequentStock);

public record RecentJobOverview(
    int Id,
    string JobNumber,
    string? Name,
    string? Customer,
    DateTime CreatedAt,
    bool IsLocked,
    int PartCount);

public record FrequentStockOverview(
    int MaterialId,
    string MaterialName,
    decimal LengthInches,
    int JobCount);
