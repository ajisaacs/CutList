namespace CutList.Web.DTOs;

public class JobDto
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

public class JobDetailDto : JobDto
{
    public List<JobPartDto> Parts { get; set; } = new();
    public List<JobStockDto> Stock { get; set; } = new();
}

public class CreateJobDto
{
    public string? Name { get; set; }
    public string? Customer { get; set; }
    public int? CuttingToolId { get; set; }
    public string? Notes { get; set; }
}

public class UpdateJobDto
{
    public string? Name { get; set; }
    public string? Customer { get; set; }
    public int? CuttingToolId { get; set; }
    public string? Notes { get; set; }
}

public class JobPartDto
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int SortOrder { get; set; }
}

public class CreateJobPartDto
{
    public int MaterialId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Length { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

public class UpdateJobPartDto
{
    public int? MaterialId { get; set; }
    public string? Name { get; set; }
    public string? Length { get; set; }
    public int? Quantity { get; set; }
}

public class JobStockDto
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public int? StockItemId { get; set; }
    public decimal LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool IsCustomLength { get; set; }
    public int Priority { get; set; }
    public int SortOrder { get; set; }
}

public class CreateJobStockDto
{
    public int MaterialId { get; set; }
    public int? StockItemId { get; set; }
    public string Length { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public bool IsCustomLength { get; set; }
    public int Priority { get; set; } = 10;
}

public class UpdateJobStockDto
{
    public int? StockItemId { get; set; }
    public string? Length { get; set; }
    public int? Quantity { get; set; }
    public bool? IsCustomLength { get; set; }
    public int? Priority { get; set; }
}

public class QuickCreateJobDto
{
    public string? Customer { get; set; }
}

public class PackJobRequestDto
{
    public decimal? KerfOverride { get; set; }
}
