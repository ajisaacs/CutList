namespace CutList.Web.DTOs;

public class PackResponseDto
{
    public List<MaterialPackResultDto> Materials { get; set; } = new();
    public PackingSummaryDto Summary { get; set; } = new();
}

public class MaterialPackResultDto
{
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public List<PackedBinDto> InStockBins { get; set; } = new();
    public List<PackedBinDto> ToBePurchasedBins { get; set; } = new();
    public List<PackedItemDto> ItemsNotPlaced { get; set; } = new();
    public MaterialPackingSummaryDto Summary { get; set; } = new();
}

public class PackedBinDto
{
    public double LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public double UsedInches { get; set; }
    public string UsedFormatted { get; set; } = string.Empty;
    public double WasteInches { get; set; }
    public string WasteFormatted { get; set; } = string.Empty;
    public double Efficiency { get; set; }
    public List<PackedItemDto> Items { get; set; } = new();
}

public class PackedItemDto
{
    public string Name { get; set; } = string.Empty;
    public double LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
}

public class PackingSummaryDto
{
    public int TotalInStockBins { get; set; }
    public int TotalToBePurchasedBins { get; set; }
    public int TotalPieces { get; set; }
    public double TotalMaterialInches { get; set; }
    public string TotalMaterialFormatted { get; set; } = string.Empty;
    public double TotalUsedInches { get; set; }
    public string TotalUsedFormatted { get; set; } = string.Empty;
    public double TotalWasteInches { get; set; }
    public string TotalWasteFormatted { get; set; } = string.Empty;
    public double Efficiency { get; set; }
    public int TotalItemsNotPlaced { get; set; }
    public List<MaterialPackingSummaryDto> MaterialSummaries { get; set; } = new();
}

public class MaterialPackingSummaryDto
{
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public int InStockBins { get; set; }
    public int ToBePurchasedBins { get; set; }
    public int TotalPieces { get; set; }
    public double TotalMaterialInches { get; set; }
    public double TotalUsedInches { get; set; }
    public double TotalWasteInches { get; set; }
    public double Efficiency { get; set; }
    public int ItemsNotPlaced { get; set; }
}
