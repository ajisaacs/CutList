namespace CutList.Web.DTOs;

public class StandalonePackRequestDto
{
    public List<PartInputDto> Parts { get; set; } = new();
    public List<StockBinInputDto> StockBins { get; set; } = new();
    public decimal Kerf { get; set; } = 0.125m;

    /// <summary>Packing engine id (see GET /api/packing/engines); null uses the configured default.</summary>
    public string? Engine { get; set; }

    /// <summary>Legacy name for <see cref="Engine"/>; used only when Engine is not set.</summary>
    public string? Strategy { get; set; }
}

public class PackingEngineDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class PartInputDto
{
    public string Name { get; set; } = string.Empty;
    public string Length { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

public class StockBinInputDto
{
    public string Length { get; set; } = string.Empty;
    public int Quantity { get; set; } = -1;
    public int Priority { get; set; } = 25;
}

public class ParseLengthRequestDto
{
    public string Input { get; set; } = string.Empty;
}

public class ParseLengthResponseDto
{
    public double Inches { get; set; }
    public string Formatted { get; set; } = string.Empty;
}

public class FormatLengthRequestDto
{
    public double Inches { get; set; }
}

public class FormatLengthResponseDto
{
    public string Formatted { get; set; } = string.Empty;
    public double Inches { get; set; }
}
