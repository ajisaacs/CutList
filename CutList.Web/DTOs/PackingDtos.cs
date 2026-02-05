namespace CutList.Web.DTOs;

public class StandalonePackRequestDto
{
    public List<PartInputDto> Parts { get; set; } = new();
    public List<StockBinInputDto> StockBins { get; set; } = new();
    public decimal Kerf { get; set; } = 0.125m;
    public string Strategy { get; set; } = "advanced";
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
