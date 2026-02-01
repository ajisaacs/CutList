namespace CutList.Mcp;

// Input models
public class PartInput
{
    public string Name { get; set; } = string.Empty;
    public string Length { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

public class StockBinInput
{
    public string Length { get; set; } = string.Empty;
    public int Quantity { get; set; } = -1;  // -1 = unlimited
    public int Priority { get; set; } = 25;   // Lower = used first
}

// Output models
public class CutListResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ResultBin> Bins { get; set; } = new();
    public List<ResultItem> UnusedItems { get; set; } = new();
    public CutListSummary? Summary { get; set; }
}

public class ResultBin
{
    public string Length { get; set; } = string.Empty;
    public double LengthInches { get; set; }
    public string UsedLength { get; set; } = string.Empty;
    public double UsedLengthInches { get; set; }
    public string RemainingLength { get; set; } = string.Empty;
    public double RemainingLengthInches { get; set; }
    public double Utilization { get; set; }
    public List<ResultItem> Items { get; set; } = new();
}

public class ResultItem
{
    public string Name { get; set; } = string.Empty;
    public string Length { get; set; } = string.Empty;
    public double LengthInches { get; set; }
}

public class CutListSummary
{
    public int TotalBinsUsed { get; set; }
    public int TotalPartsPlaced { get; set; }
    public int TotalPartsNotPlaced { get; set; }
    public string TotalStockLength { get; set; } = string.Empty;
    public double TotalStockLengthInches { get; set; }
    public string TotalWaste { get; set; } = string.Empty;
    public double TotalWasteInches { get; set; }
    public double OverallUtilization { get; set; }
}

public class ParseLengthResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public double Inches { get; set; }
    public string? Formatted { get; set; }
}

public class FormatLengthResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Formatted { get; set; }
    public double Inches { get; set; }
}

public class CutListReportResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? FilePath { get; set; }
    public int TotalBins { get; set; }
    public int TotalParts { get; set; }
    public int PartsNotPlaced { get; set; }
}
