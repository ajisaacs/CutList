using System.ComponentModel;
using ModelContextProtocol.Server;

namespace CutList.Mcp;

/// <summary>
/// MCP tools for job management - creating jobs, managing parts/stock, and running optimization.
/// All calls go through the CutList.Web REST API via ApiClient.
/// </summary>
[McpServerToolType]
public class JobTools
{
    private readonly ApiClient _api;

    public JobTools(ApiClient api)
    {
        _api = api;
    }

    #region Jobs

    [McpServerTool(Name = "list_jobs"), Description("Lists all jobs in the system with summary info (job number, name, customer, part/stock counts).")]
    public async Task<JobListResult> ListJobs()
    {
        var jobs = await _api.GetJobsAsync();

        return new JobListResult
        {
            Success = true,
            Jobs = jobs.Select(j => new JobSummaryDto
            {
                Id = j.Id,
                JobNumber = j.JobNumber,
                Name = j.Name,
                Customer = j.Customer,
                CuttingToolId = j.CuttingToolId,
                CuttingToolName = j.CuttingToolName,
                Notes = j.Notes,
                CreatedAt = j.CreatedAt,
                UpdatedAt = j.UpdatedAt,
                PartCount = j.PartCount,
                StockCount = j.StockCount
            }).ToList()
        };
    }

    [McpServerTool(Name = "get_job"), Description("Gets full job details including all parts and stock assignments.")]
    public async Task<JobDetailResult> GetJob(
        [Description("Job ID")]
        int jobId)
    {
        try
        {
            var job = await _api.GetJobAsync(jobId);
            if (job == null)
                return new JobDetailResult { Success = false, Error = $"Job {jobId} not found" };

            return new JobDetailResult
            {
                Success = true,
                Job = MapJobDetail(job)
            };
        }
        catch (HttpRequestException ex)
        {
            return new JobDetailResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "create_job"), Description("Creates a new job. Returns the created job with its auto-generated job number.")]
    public async Task<JobDetailResult> CreateJob(
        [Description("Job name/description")]
        string? name = null,
        [Description("Customer name")]
        string? customer = null,
        [Description("Cutting tool ID (use list_cutting_tools to find IDs). If not set, uses the default tool.")]
        int? cuttingToolId = null,
        [Description("Notes about the job")]
        string? notes = null)
    {
        try
        {
            var job = await _api.CreateJobAsync(name, customer, cuttingToolId, notes);
            if (job == null)
                return new JobDetailResult { Success = false, Error = "Failed to create job" };

            return new JobDetailResult
            {
                Success = true,
                Job = MapJobDetail(job)
            };
        }
        catch (HttpRequestException ex)
        {
            return new JobDetailResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "update_job"), Description("Updates job details (name, customer, cutting tool, notes). Only provided fields are updated.")]
    public async Task<JobDetailResult> UpdateJob(
        [Description("Job ID")]
        int jobId,
        [Description("New job name")]
        string? name = null,
        [Description("New customer name")]
        string? customer = null,
        [Description("New cutting tool ID")]
        int? cuttingToolId = null,
        [Description("New notes")]
        string? notes = null)
    {
        try
        {
            var job = await _api.UpdateJobAsync(jobId, name, customer, cuttingToolId, notes);
            if (job == null)
                return new JobDetailResult { Success = false, Error = $"Job {jobId} not found" };

            return new JobDetailResult
            {
                Success = true,
                Job = MapJobDetail(job)
            };
        }
        catch (HttpRequestException ex)
        {
            return new JobDetailResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "delete_job"), Description("Deletes a job and all its parts and stock assignments.")]
    public async Task<SimpleResult> DeleteJob(
        [Description("Job ID")]
        int jobId)
    {
        try
        {
            await _api.DeleteJobAsync(jobId);
            return new SimpleResult { Success = true };
        }
        catch (HttpRequestException ex)
        {
            return new SimpleResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Parts

    [McpServerTool(Name = "add_job_part"), Description("Adds a single part to a job.")]
    public async Task<JobPartResult> AddJobPart(
        [Description("Job ID")]
        int jobId,
        [Description("Material ID (use list_materials to find IDs)")]
        int materialId,
        [Description("Part name/label (e.g., 'Top Rail', 'Picket')")]
        string name,
        [Description("Part length (e.g., '36\"', '4\\' 6\"', '54.5')")]
        string length,
        [Description("Quantity needed (default 1)")]
        int quantity = 1)
    {
        try
        {
            var part = await _api.AddJobPartAsync(jobId, materialId, name, length, quantity);
            if (part == null)
                return new JobPartResult { Success = false, Error = "Failed to add part" };

            return new JobPartResult
            {
                Success = true,
                Part = MapPart(part)
            };
        }
        catch (HttpRequestException ex)
        {
            return new JobPartResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "add_job_parts"), Description("Batch adds multiple parts to a job. Ideal for entering a full BOM (bill of materials). Returns the complete job state after all parts are added.")]
    public async Task<JobDetailResult> AddJobParts(
        [Description("Job ID")]
        int jobId,
        [Description("Array of parts to add. Each needs: materialId (int), name (string), length (string like \"36\\\"\" or \"4' 6\\\"\"), quantity (int)")]
        PartEntry[] parts)
    {
        var errors = new List<string>();
        int added = 0;

        foreach (var part in parts)
        {
            try
            {
                await _api.AddJobPartAsync(jobId, part.MaterialId, part.Name, part.Length, part.Quantity);
                added++;
            }
            catch (HttpRequestException ex)
            {
                errors.Add($"Failed to add '{part.Name}': {ex.Message}");
            }
        }

        // Reload the full job state
        try
        {
            var job = await _api.GetJobAsync(jobId);
            if (job == null)
                return new JobDetailResult { Success = false, Error = $"Job {jobId} not found after adding parts" };

            var result = new JobDetailResult
            {
                Success = errors.Count == 0,
                Job = MapJobDetail(job)
            };

            if (errors.Count > 0)
                result.Error = $"Added {added}/{parts.Length} parts. Errors: {string.Join("; ", errors)}";

            return result;
        }
        catch (HttpRequestException ex)
        {
            return new JobDetailResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "delete_job_part"), Description("Removes a part from a job.")]
    public async Task<SimpleResult> DeleteJobPart(
        [Description("Job ID")]
        int jobId,
        [Description("Part ID (from get_job results)")]
        int partId)
    {
        try
        {
            await _api.DeleteJobPartAsync(jobId, partId);
            return new SimpleResult { Success = true };
        }
        catch (HttpRequestException ex)
        {
            return new SimpleResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Stock

    [McpServerTool(Name = "add_job_stock"), Description("Adds a stock material assignment to a job. Stock defines what material lengths are available for cutting.")]
    public async Task<JobStockResult> AddJobStock(
        [Description("Job ID")]
        int jobId,
        [Description("Material ID (must match the material used by parts)")]
        int materialId,
        [Description("Stock length (e.g., '20'', '240', '20 ft')")]
        string length,
        [Description("Quantity available (-1 for unlimited, default -1)")]
        int quantity = -1,
        [Description("Stock item ID from the stock catalog (optional - links to a specific catalog stock item)")]
        int? stockItemId = null,
        [Description("True if this is a custom length not sourced from the stock catalog (default false)")]
        bool isCustomLength = false,
        [Description("Priority - lower number = used first (default 10)")]
        int priority = 10)
    {
        try
        {
            var stock = await _api.AddJobStockAsync(jobId, materialId, stockItemId, length, quantity, isCustomLength, priority);
            if (stock == null)
                return new JobStockResult { Success = false, Error = "Failed to add stock" };

            return new JobStockResult
            {
                Success = true,
                Stock = MapStock(stock)
            };
        }
        catch (HttpRequestException ex)
        {
            return new JobStockResult { Success = false, Error = ex.Message };
        }
    }

    [McpServerTool(Name = "delete_job_stock"), Description("Removes a stock assignment from a job.")]
    public async Task<SimpleResult> DeleteJobStock(
        [Description("Job ID")]
        int jobId,
        [Description("Stock ID (from get_job results)")]
        int stockId)
    {
        try
        {
            await _api.DeleteJobStockAsync(jobId, stockId);
            return new SimpleResult { Success = true };
        }
        catch (HttpRequestException ex)
        {
            return new SimpleResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Optimization

    [McpServerTool(Name = "optimize_job"), Description("Runs bin packing optimization on a job. The job must have parts defined, and stock must be explicitly configured on the job (via add_job_stock) for each material used by its parts - there is no fallback to inventory; parts with no matching stock configured come back as items not placed. Returns optimized cut layouts per material with efficiency stats.")]
    public async Task<OptimizeJobResult> OptimizeJob(
        [Description("Job ID")]
        int jobId,
        [Description("Optional kerf override in inches (e.g., 0.125). If not set, uses the job's cutting tool kerf.")]
        double? kerfOverride = null)
    {
        try
        {
            var result = await _api.PackJobAsync(jobId, kerfOverride.HasValue ? (decimal)kerfOverride.Value : null);
            if (result == null)
                return new OptimizeJobResult { Success = false, Error = "Optimization returned no results" };

            return new OptimizeJobResult
            {
                Success = true,
                Materials = result.Materials.Select(m => new OptMaterialResultDto
                {
                    MaterialId = m.MaterialId,
                    MaterialName = m.MaterialName,
                    InStockBins = m.InStockBins.Select(MapBin).ToList(),
                    ToBePurchasedBins = m.ToBePurchasedBins.Select(MapBin).ToList(),
                    ItemsNotPlaced = m.ItemsNotPlaced.Select(i => new OptItemDto { Name = i.Name, LengthInches = i.LengthInches, LengthFormatted = i.LengthFormatted }).ToList(),
                    Summary = MapMaterialSummary(m.Summary)
                }).ToList(),
                Summary = new OptSummaryDto
                {
                    TotalInStockBins = result.Summary.TotalInStockBins,
                    TotalToBePurchasedBins = result.Summary.TotalToBePurchasedBins,
                    TotalPieces = result.Summary.TotalPieces,
                    TotalMaterialFormatted = result.Summary.TotalMaterialFormatted,
                    TotalUsedFormatted = result.Summary.TotalUsedFormatted,
                    TotalWasteFormatted = result.Summary.TotalWasteFormatted,
                    Efficiency = result.Summary.Efficiency,
                    TotalItemsNotPlaced = result.Summary.TotalItemsNotPlaced
                }
            };
        }
        catch (HttpRequestException ex)
        {
            return new OptimizeJobResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Cutting Tools

    [McpServerTool(Name = "list_cutting_tools"), Description("Lists all available cutting tools with their kerf (blade width) values.")]
    public async Task<CuttingToolListResult> ListCuttingTools(
        [Description("Include inactive tools (default false)")]
        bool includeInactive = false)
    {
        var tools = await _api.GetCuttingToolsAsync(includeInactive);

        return new CuttingToolListResult
        {
            Success = true,
            Tools = tools.Select(t => new CuttingToolSummaryDto
            {
                Id = t.Id,
                Name = t.Name,
                KerfInches = t.KerfInches,
                IsDefault = t.IsDefault,
                IsActive = t.IsActive
            }).ToList()
        };
    }

    #endregion

    #region Mapping Helpers

    private static JobDetailDto MapJobDetail(ApiJobDetailDto j) => new()
    {
        Id = j.Id,
        JobNumber = j.JobNumber,
        Name = j.Name,
        Customer = j.Customer,
        CuttingToolId = j.CuttingToolId,
        CuttingToolName = j.CuttingToolName,
        Notes = j.Notes,
        CreatedAt = j.CreatedAt,
        UpdatedAt = j.UpdatedAt,
        PartCount = j.PartCount,
        StockCount = j.StockCount,
        Parts = j.Parts.Select(MapPart).ToList(),
        Stock = j.Stock.Select(MapStock).ToList()
    };

    private static JobPartSummaryDto MapPart(ApiJobPartDto p) => new()
    {
        Id = p.Id,
        MaterialId = p.MaterialId,
        MaterialName = p.MaterialName,
        Name = p.Name,
        LengthInches = p.LengthInches,
        LengthFormatted = p.LengthFormatted,
        Quantity = p.Quantity
    };

    private static JobStockSummaryDto MapStock(ApiJobStockDto s) => new()
    {
        Id = s.Id,
        MaterialId = s.MaterialId,
        MaterialName = s.MaterialName,
        StockItemId = s.StockItemId,
        LengthInches = s.LengthInches,
        LengthFormatted = s.LengthFormatted,
        Quantity = s.Quantity,
        IsCustomLength = s.IsCustomLength,
        Priority = s.Priority
    };

    private static OptBinDto MapBin(ApiPackedBinDto b) => new()
    {
        LengthFormatted = b.LengthFormatted,
        UsedFormatted = b.UsedFormatted,
        WasteFormatted = b.WasteFormatted,
        Efficiency = Math.Round(b.Efficiency, 1),
        Items = b.Items.Select(i => new OptItemDto
        {
            Name = i.Name,
            LengthInches = i.LengthInches,
            LengthFormatted = i.LengthFormatted
        }).ToList()
    };

    private static OptMaterialSummaryDto MapMaterialSummary(ApiMaterialPackingSummaryDto s) => new()
    {
        InStockBins = s.InStockBins,
        ToBePurchasedBins = s.ToBePurchasedBins,
        TotalPieces = s.TotalPieces,
        Efficiency = Math.Round(s.Efficiency, 1),
        ItemsNotPlaced = s.ItemsNotPlaced
    };

    #endregion
}

#region Job Tool DTOs

public class PartEntry
{
    public int MaterialId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Length { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

public class SimpleResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class JobSummaryDto
{
    public int Id { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Customer { get; set; }
    public int? CuttingToolId { get; set; }
    public string? CuttingToolName { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int PartCount { get; set; }
    public int StockCount { get; set; }
}

public class JobDetailDto
{
    public int Id { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Customer { get; set; }
    public int? CuttingToolId { get; set; }
    public string? CuttingToolName { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int PartCount { get; set; }
    public int StockCount { get; set; }
    public List<JobPartSummaryDto> Parts { get; set; } = new();
    public List<JobStockSummaryDto> Stock { get; set; } = new();
}

public class JobPartSummaryDto
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class JobStockSummaryDto
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public int? StockItemId { get; set; }
    public decimal LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool IsCustomLength { get; set; }
    public int Priority { get; set; }
}

public class JobListResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<JobSummaryDto> Jobs { get; set; } = new();
}

public class JobDetailResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public JobDetailDto? Job { get; set; }
}

public class JobPartResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public JobPartSummaryDto? Part { get; set; }
}

public class JobStockResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public JobStockSummaryDto? Stock { get; set; }
}

public class CuttingToolSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal KerfInches { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

public class CuttingToolListResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<CuttingToolSummaryDto> Tools { get; set; } = new();
}

// Optimization result DTOs — streamlined for LLM consumption
public class OptimizeJobResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<OptMaterialResultDto> Materials { get; set; } = new();
    public OptSummaryDto? Summary { get; set; }
}

public class OptMaterialResultDto
{
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public List<OptBinDto> InStockBins { get; set; } = new();
    public List<OptBinDto> ToBePurchasedBins { get; set; } = new();
    public List<OptItemDto> ItemsNotPlaced { get; set; } = new();
    public OptMaterialSummaryDto Summary { get; set; } = new();
}

public class OptBinDto
{
    public string LengthFormatted { get; set; } = string.Empty;
    public string UsedFormatted { get; set; } = string.Empty;
    public string WasteFormatted { get; set; } = string.Empty;
    public double Efficiency { get; set; }
    public List<OptItemDto> Items { get; set; } = new();
}

public class OptItemDto
{
    public string Name { get; set; } = string.Empty;
    public double LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
}

public class OptSummaryDto
{
    public int TotalInStockBins { get; set; }
    public int TotalToBePurchasedBins { get; set; }
    public int TotalPieces { get; set; }
    public string TotalMaterialFormatted { get; set; } = string.Empty;
    public string TotalUsedFormatted { get; set; } = string.Empty;
    public string TotalWasteFormatted { get; set; } = string.Empty;
    public double Efficiency { get; set; }
    public int TotalItemsNotPlaced { get; set; }
}

public class OptMaterialSummaryDto
{
    public int InStockBins { get; set; }
    public int ToBePurchasedBins { get; set; }
    public int TotalPieces { get; set; }
    public double Efficiency { get; set; }
    public int ItemsNotPlaced { get; set; }
}

#endregion
