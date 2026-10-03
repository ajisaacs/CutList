using CutList.Web.Data.Entities;
using CutList.Web.Services;

namespace CutList.Web.Tests.Infrastructure;

public enum ChildOp
{
    AddPart,
    UpdatePart,
    DeletePart,
    AddStock,
    UpdateStock,
    DeleteStock
}

/// <summary>
/// The six job-child mutations, each with distinct input values so a missed guard or a dropped
/// field is visible in a database snapshot, plus the expected snapshot after a successful run.
/// </summary>
public static class ChildMutations
{
    public static TheoryData<ChildOp> All => new()
    {
        ChildOp.AddPart, ChildOp.UpdatePart, ChildOp.DeletePart,
        ChildOp.AddStock, ChildOp.UpdateStock, ChildOp.DeleteStock
    };

    /// <summary>Runs the operation through the service; returns the generated child Id for adds.</summary>
    public static async Task<int?> RunAsync(JobService jobs, TestSeed seed, ChildOp op, int jobId, int partId, int stockId)
    {
        switch (op)
        {
            case ChildOp.AddPart:
                var part = new JobPart
                {
                    JobId = jobId, MaterialId = seed.RoundTubeMaterialId, Name = "Added part",
                    LengthInches = 55.5m, Quantity = 7
                };
                await jobs.AddPartAsync(part);
                return part.Id;
            case ChildOp.UpdatePart:
                await jobs.UpdatePartAsync(new JobPart
                {
                    Id = partId, JobId = jobId, MaterialId = seed.RoundTubeMaterialId, Name = "Edited part",
                    LengthInches = 77.125m, Quantity = 9, SortOrder = 42
                });
                return null;
            case ChildOp.DeletePart:
                await jobs.DeletePartAsync(partId);
                return null;
            case ChildOp.AddStock:
                var stock = new JobStock
                {
                    JobId = jobId, MaterialId = seed.RoundTubeMaterialId, StockItemId = seed.RoundTubeStockItemId,
                    LengthInches = 288m, Quantity = 3, IsCustomLength = false, Priority = 4
                };
                await jobs.AddStockAsync(stock);
                return stock.Id;
            case ChildOp.UpdateStock:
                await jobs.UpdateStockAsync(new JobStock
                {
                    Id = stockId, JobId = jobId, MaterialId = seed.RoundTubeMaterialId, StockItemId = null,
                    LengthInches = 120m, Quantity = -1, IsCustomLength = true, Priority = 2, SortOrder = 42
                });
                return null;
            case ChildOp.DeleteStock:
                await jobs.DeleteStockAsync(stockId);
                return null;
            default:
                throw new ArgumentOutOfRangeException(nameof(op), op, null);
        }
    }

    /// <summary>
    /// Expected database state after the service call succeeds: only the targeted child changes and
    /// the parent's saved result is cleared. The parent's new UpdatedAt is taken from
    /// <paramref name="actualUpdatedAt"/> (the caller asserts it advanced).
    /// </summary>
    public static JobSnapshot ExpectedAfterServiceRun(
        JobSnapshot before, TestSeed seed, ChildOp op, int jobId, int partId, int stockId, int? newId,
        DateTime? actualUpdatedAt)
    {
        var jobs = before.Jobs
            .Select(j => j.Id == jobId
                ? j with { UpdatedAt = actualUpdatedAt, OptimizationResultJson = null, OptimizedAt = null }
                : j)
            .ToList();
        var parts = before.Parts.ToList();
        var stock = before.Stock.ToList();

        switch (op)
        {
            case ChildOp.AddPart:
                parts.Add(new PartRow(newId!.Value, jobId, seed.RoundTubeMaterialId, "Added part", 55.5m, 7,
                    NextSortOrder(before.PartsOf(jobId).Select(p => p.SortOrder))));
                break;
            case ChildOp.UpdatePart:
                parts = parts.Select(p => p.Id == partId
                    ? p with { MaterialId = seed.RoundTubeMaterialId, Name = "Edited part", LengthInches = 77.125m, Quantity = 9 }
                    : p).ToList();
                break;
            case ChildOp.DeletePart:
                parts.RemoveAll(p => p.Id == partId);
                break;
            case ChildOp.AddStock:
                stock.Add(new StockRow(newId!.Value, jobId, seed.RoundTubeMaterialId, seed.RoundTubeStockItemId, 288m, 3,
                    false, 4, NextSortOrder(before.StockOf(jobId).Select(s => s.SortOrder))));
                break;
            case ChildOp.UpdateStock:
                stock = stock.Select(s => s.Id == stockId
                    ? s with
                    {
                        MaterialId = seed.RoundTubeMaterialId, StockItemId = null, LengthInches = 120m, Quantity = -1,
                        IsCustomLength = true, Priority = 2
                    }
                    : s).ToList();
                break;
            case ChildOp.DeleteStock:
                stock.RemoveAll(s => s.Id == stockId);
                break;
        }

        return before with
        {
            Jobs = jobs,
            Parts = parts.OrderBy(p => p.Id).ToList(),
            Stock = stock.OrderBy(s => s.Id).ToList()
        };
    }

    private static int NextSortOrder(IEnumerable<int> existing) => existing.DefaultIfEmpty(-1).Max() + 1;
}
