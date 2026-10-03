using System.ComponentModel;
using CutList.Core;
using CutList.Core.Formatting;
using CutList.Core.Nesting;
using ModelContextProtocol.Server;

namespace CutList.Mcp;

/// <summary>
/// MCP tools for cut list optimization.
/// </summary>
[McpServerToolType]
public static class CutListTools
{
    /// <summary>
    /// Creates an optimized cut list by packing parts into stock bins using the selected packing engine.
    /// Returns the optimal arrangement of cuts to minimize waste.
    /// </summary>
    /// <param name="parts">List of parts to cut. Each part has a name, length (in inches or architectural format like 12' 6"), and quantity.</param>
    /// <param name="stockBins">List of available stock material. Each has a length, quantity (-1 for unlimited), and priority (lower = used first).</param>
    /// <param name="kerf">Blade/cutting width in inches. Default is 0.125 (1/8"). This accounts for material lost to the cut itself.</param>
    /// <returns>Optimized cut list showing which parts go in which bins, utilization percentages, and any parts that couldn't fit.</returns>
    [McpServerTool(Name = "create_cutlist"), Description("Creates an optimized cut list by packing parts into stock bins to minimize waste.")]
    public static CutListResult CreateCutList(
        [Description("Parts to cut. Each object needs: name (string), length (string like \"12'\" or \"36\\\"\" or \"12.5\"), quantity (int)")]
        PartInput[] parts,
        [Description("Stock bins available. Each needs: length (string), quantity (int, use -1 for unlimited), priority (int, lower = used first, default 25)")]
        StockBinInput[] stockBins,
        [Description("Blade kerf/width in inches (default 0.125)")]
        double kerf = 0.125,
        [Description("Packing engine id (see list_packing_engines). Omit for the built-in default engine.")]
        string? strategy = null)
    {
        try
        {
            var (binItems, partsError) = ConvertParts(parts);
            if (partsError != null)
                return new CutListResult { Success = false, Error = partsError };

            var (multiBins, binsError) = ConvertStockBins(stockBins);
            if (binsError != null)
                return new CutListResult { Success = false, Error = binsError };

            var engineInfo = Engines.Resolve(strategy);
            var packResult = RunPackingAlgorithm(binItems!, multiBins!, kerf, engineInfo.Id);

            // Convert results
            var resultBins = new List<ResultBin>();
            foreach (var bin in packResult.Bins)
            {
                var resultBin = new ResultBin
                {
                    Length = FormatLength(bin.Length),
                    LengthInches = bin.Length,
                    UsedLength = FormatLength(bin.UsedLength),
                    UsedLengthInches = bin.UsedLength,
                    RemainingLength = FormatLength(bin.RemainingLength),
                    RemainingLengthInches = bin.RemainingLength,
                    Utilization = Math.Round(bin.Utilization * 100, 2),
                    Items = bin.Items.Select(item => new ResultItem
                    {
                        Name = item.Name,
                        Length = FormatLength(item.Length),
                        LengthInches = item.Length
                    }).ToList()
                };
                resultBins.Add(resultBin);
            }

            var unusedItems = packResult.ItemsNotUsed.Select(item => new ResultItem
            {
                Name = item.Name,
                Length = FormatLength(item.Length),
                LengthInches = item.Length
            }).ToList();

            // Calculate summary statistics
            double totalStockUsed = packResult.Bins.Sum(b => b.Length);
            double totalMaterialUsed = packResult.Bins.Sum(b => b.UsedLength);
            double totalWaste = packResult.Bins.Sum(b => b.RemainingLength);
            double overallUtilization = totalStockUsed > 0 ? (totalMaterialUsed / totalStockUsed) * 100 : 0;

            return new CutListResult
            {
                Success = true,
                EngineId = engineInfo.Id,
                Bins = resultBins,
                UnusedItems = unusedItems,
                Summary = new CutListSummary
                {
                    TotalBinsUsed = packResult.Bins.Count,
                    TotalPartsPlaced = binItems!.Count - packResult.ItemsNotUsed.Count,
                    TotalPartsNotPlaced = packResult.ItemsNotUsed.Count,
                    TotalStockLength = FormatLength(totalStockUsed),
                    TotalStockLengthInches = totalStockUsed,
                    TotalWaste = FormatLength(totalWaste),
                    TotalWasteInches = totalWaste,
                    OverallUtilization = Math.Round(overallUtilization, 2)
                }
            };
        }
        catch (Exception ex)
        {
            return new CutListResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Parses a length string in architectural format (feet/inches/fractions) to inches.
    /// </summary>
    /// <param name="input">Length string like "12'", "6\"", "12' 6\"", "12.5", "6 1/2\"", etc.</param>
    /// <returns>The length in inches, or an error message if parsing fails.</returns>
    [McpServerTool(Name = "parse_length"), Description("Parses an architectural length string (feet/inches/fractions) to decimal inches.")]
    public static ParseLengthResult ParseLengthString(
        [Description("Length string like \"12'\", \"6\\\"\", \"12' 6\\\"\", \"12.5\", \"6 1/2\\\"\"")]
        string input)
    {
        try
        {
            double inches = ParseLength(input);
            return new ParseLengthResult
            {
                Success = true,
                Inches = inches,
                Formatted = FormatLength(inches)
            };
        }
        catch (Exception ex)
        {
            return new ParseLengthResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Formats a length in inches to a human-readable string with feet and fractional inches.
    /// </summary>
    /// <param name="inches">Length in inches.</param>
    /// <returns>Formatted string like "12' 6 1/2\"".</returns>
    [McpServerTool(Name = "format_length"), Description("Formats a length in inches to feet and fractional inches.")]
    public static FormatLengthResult FormatLengthString(
        [Description("Length in inches")]
        double inches)
    {
        try
        {
            return new FormatLengthResult
            {
                Success = true,
                Formatted = FormatLength(inches),
                Inches = inches
            };
        }
        catch (Exception ex)
        {
            return new FormatLengthResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Creates an optimized cut list and saves a formatted text report to a file.
    /// </summary>
    [McpServerTool(Name = "create_cutlist_report"), Description("Creates an optimized cut list and saves a formatted printable text report to a file. Returns the file path.")]
    public static CutListReportResult CreateCutListReport(
        [Description("Parts to cut. Each object needs: name (string), length (string like \"12'\" or \"36\\\"\" or \"12.5\"), quantity (int)")]
        PartInput[] parts,
        [Description("Stock bins available. Each needs: length (string), quantity (int, use -1 for unlimited), priority (int, lower = used first, default 25)")]
        StockBinInput[] stockBins,
        [Description("Blade kerf/width in inches (default 0.125)")]
        double kerf = 0.125,
        [Description("File path to save the report. If not provided, saves to a temp file.")]
        string? filePath = null,
        [Description("Packing engine id (see list_packing_engines). Omit for the built-in default engine.")]
        string? strategy = null)
    {
        try
        {
            var (binItems, partsError) = ConvertParts(parts);
            if (partsError != null)
                return new CutListReportResult { Success = false, Error = partsError };

            var (multiBins, binsError) = ConvertStockBins(stockBins);
            if (binsError != null)
                return new CutListReportResult { Success = false, Error = binsError };

            var engineInfo = Engines.Resolve(strategy);
            var packResult = RunPackingAlgorithm(binItems!, multiBins!, kerf, engineInfo.Id);

            // Determine file path
            var outputPath = string.IsNullOrWhiteSpace(filePath)
                ? Path.Combine(Path.GetTempPath(), $"cutlist_{DateTime.Now:yyyyMMdd_HHmmss}.txt")
                : filePath;

            // Save using BinFileSaver
            var saver = new BinFileSaver(packResult.Bins);
            saver.SaveBinsToFile(outputPath);

            return new CutListReportResult
            {
                Success = true,
                EngineId = engineInfo.Id,
                FilePath = Path.GetFullPath(outputPath),
                TotalBins = packResult.Bins.Count,
                TotalParts = binItems!.Count - packResult.ItemsNotUsed.Count,
                PartsNotPlaced = packResult.ItemsNotUsed.Count
            };
        }
        catch (Exception ex)
        {
            return new CutListReportResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static (List<BinItem>? Items, string? Error) ConvertParts(PartInput[] parts)
    {
        var binItems = new List<BinItem>();
        foreach (var part in parts)
        {
            double length = ParseLength(part.Length);
            if (length <= 0)
                return (null, $"Invalid part length: {part.Length} for part '{part.Name}'");

            for (int i = 0; i < part.Quantity; i++)
                binItems.Add(new BinItem(part.Name, length));
        }
        return (binItems, null);
    }

    private static (List<MultiBin>? Bins, string? Error) ConvertStockBins(StockBinInput[] stockBins)
    {
        var multiBins = new List<MultiBin>();
        foreach (var bin in stockBins)
        {
            double length = ParseLength(bin.Length);
            if (length <= 0)
                return (null, $"Invalid bin length: {bin.Length}");

            multiBins.Add(new MultiBin(length, bin.Quantity, bin.Priority));
        }
        return (multiBins, null);
    }

    // Built-in engines resolved in-process; these tools do not call CutList.Web.
    private static readonly PackingEngineCatalog Engines = PackingEngineCatalog.CreateDefault();

    private static PackResult RunPackingAlgorithm(List<BinItem> items, List<MultiBin> bins, double kerf, string? engineId)
    {
        var packer = new MultiBinPacker(Engines.Create(engineId));
        packer.SetBins(bins);
        packer.Spacing = kerf;
        return packer.Pack(items);
    }

    private static double ParseLength(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return 0;

        // Try parsing as a plain number first
        if (double.TryParse(input.Trim(), out double plainNumber))
            return plainNumber;

        // Try architectural format
        return ArchUnits.ParseToInches(input);
    }

    private static string FormatLength(double inches)
    {
        return ArchUnits.FormatFromInches(inches);
    }
}
