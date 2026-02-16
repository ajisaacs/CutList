using System.Net.Http.Json;

namespace CutList.Mcp;

/// <summary>
/// Typed HTTP client for calling the CutList.Web REST API.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    #region Suppliers

    public async Task<List<ApiSupplierDto>> GetSuppliersAsync(bool includeInactive = false)
    {
        var url = $"api/suppliers?includeInactive={includeInactive}";
        return await _http.GetFromJsonAsync<List<ApiSupplierDto>>(url) ?? [];
    }

    public async Task<ApiSupplierDto?> CreateSupplierAsync(string name, string? contactInfo, string? notes)
    {
        var response = await _http.PostAsJsonAsync("api/suppliers", new { Name = name, ContactInfo = contactInfo, Notes = notes });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiSupplierDto>();
    }

    #endregion

    #region Materials

    public async Task<List<ApiMaterialDto>> GetMaterialsAsync(string? shape = null, bool includeInactive = false)
    {
        var url = $"api/materials?includeInactive={includeInactive}";
        if (!string.IsNullOrEmpty(shape))
            url += $"&shape={Uri.EscapeDataString(shape)}";
        return await _http.GetFromJsonAsync<List<ApiMaterialDto>>(url) ?? [];
    }

    public async Task<ApiMaterialDto?> CreateMaterialAsync(string shape, string? size, string? description,
        string? type, string? grade, Dictionary<string, decimal>? dimensions)
    {
        var body = new
        {
            Shape = shape,
            Size = size,
            Description = description,
            Type = type,
            Grade = grade,
            Dimensions = dimensions
        };
        var response = await _http.PostAsJsonAsync("api/materials", body);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new ApiConflictException(error);
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiMaterialDto>();
    }

    public async Task<List<ApiMaterialDto>> SearchMaterialsAsync(string shape, decimal targetValue, decimal tolerance)
    {
        var response = await _http.PostAsJsonAsync("api/materials/search", new
        {
            Shape = shape,
            TargetValue = targetValue,
            Tolerance = tolerance
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ApiMaterialDto>>() ?? [];
    }

    #endregion

    #region Stock Items

    public async Task<List<ApiStockItemDto>> GetStockItemsAsync(int? materialId = null, bool includeInactive = false)
    {
        var url = $"api/stock-items?includeInactive={includeInactive}";
        if (materialId.HasValue)
            url += $"&materialId={materialId.Value}";
        return await _http.GetFromJsonAsync<List<ApiStockItemDto>>(url) ?? [];
    }

    public async Task<ApiStockItemDto?> CreateStockItemAsync(int materialId, string length, string? name, int quantityOnHand, string? notes)
    {
        var body = new
        {
            MaterialId = materialId,
            Length = length,
            Name = name,
            QuantityOnHand = quantityOnHand,
            Notes = notes
        };
        var response = await _http.PostAsJsonAsync("api/stock-items", body);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new ApiConflictException(error);
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiStockItemDto>();
    }

    #endregion

    #region Jobs

    public async Task<List<ApiJobDto>> GetJobsAsync()
    {
        return await _http.GetFromJsonAsync<List<ApiJobDto>>("api/jobs") ?? [];
    }

    public async Task<ApiJobDetailDto?> GetJobAsync(int id)
    {
        return await _http.GetFromJsonAsync<ApiJobDetailDto>($"api/jobs/{id}");
    }

    public async Task<ApiJobDetailDto?> CreateJobAsync(string? name, string? customer, int? cuttingToolId, string? notes)
    {
        var response = await _http.PostAsJsonAsync("api/jobs", new { Name = name, Customer = customer, CuttingToolId = cuttingToolId, Notes = notes });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiJobDetailDto>();
    }

    public async Task<ApiJobDetailDto?> UpdateJobAsync(int id, string? name, string? customer, int? cuttingToolId, string? notes)
    {
        var response = await _http.PutAsJsonAsync($"api/jobs/{id}", new { Name = name, Customer = customer, CuttingToolId = cuttingToolId, Notes = notes });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiJobDetailDto>();
    }

    public async Task DeleteJobAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/jobs/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<ApiJobPartDto?> AddJobPartAsync(int jobId, int materialId, string name, string length, int quantity)
    {
        var response = await _http.PostAsJsonAsync($"api/jobs/{jobId}/parts", new { MaterialId = materialId, Name = name, Length = length, Quantity = quantity });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiJobPartDto>();
    }

    public async Task<ApiJobPartDto?> UpdateJobPartAsync(int jobId, int partId, int? materialId, string? name, string? length, int? quantity)
    {
        var response = await _http.PutAsJsonAsync($"api/jobs/{jobId}/parts/{partId}", new { MaterialId = materialId, Name = name, Length = length, Quantity = quantity });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiJobPartDto>();
    }

    public async Task DeleteJobPartAsync(int jobId, int partId)
    {
        var response = await _http.DeleteAsync($"api/jobs/{jobId}/parts/{partId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<ApiJobStockDto?> AddJobStockAsync(int jobId, int materialId, int? stockItemId, string length, int quantity, bool isCustomLength, int priority)
    {
        var response = await _http.PostAsJsonAsync($"api/jobs/{jobId}/stock", new
        {
            MaterialId = materialId,
            StockItemId = stockItemId,
            Length = length,
            Quantity = quantity,
            IsCustomLength = isCustomLength,
            Priority = priority
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiJobStockDto>();
    }

    public async Task DeleteJobStockAsync(int jobId, int stockId)
    {
        var response = await _http.DeleteAsync($"api/jobs/{jobId}/stock/{stockId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<ApiPackResponseDto?> PackJobAsync(int jobId, decimal? kerfOverride = null)
    {
        var response = await _http.PostAsJsonAsync($"api/jobs/{jobId}/pack", new { KerfOverride = kerfOverride });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiPackResponseDto>();
    }

    #endregion

    #region Cutting Tools

    public async Task<List<ApiCuttingToolDto>> GetCuttingToolsAsync(bool includeInactive = false)
    {
        return await _http.GetFromJsonAsync<List<ApiCuttingToolDto>>($"api/cutting-tools?includeInactive={includeInactive}") ?? [];
    }

    public async Task<ApiCuttingToolDto?> GetDefaultCuttingToolAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ApiCuttingToolDto>("api/cutting-tools/default");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    #endregion

    #region Offerings

    public async Task<List<ApiOfferingDto>> GetOfferingsForSupplierAsync(int supplierId)
    {
        return await _http.GetFromJsonAsync<List<ApiOfferingDto>>($"api/suppliers/{supplierId}/offerings") ?? [];
    }

    public async Task<List<ApiOfferingDto>> GetOfferingsForStockItemAsync(int stockItemId)
    {
        return await _http.GetFromJsonAsync<List<ApiOfferingDto>>($"api/stock-items/{stockItemId}/offerings") ?? [];
    }

    public async Task<ApiOfferingDto?> CreateOfferingAsync(int supplierId, int stockItemId,
        string? partNumber, string? supplierDescription, decimal? price, string? notes)
    {
        var body = new
        {
            StockItemId = stockItemId,
            PartNumber = partNumber,
            SupplierDescription = supplierDescription,
            Price = price,
            Notes = notes
        };
        var response = await _http.PostAsJsonAsync($"api/suppliers/{supplierId}/offerings", body);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new ApiConflictException(error);
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiOfferingDto>();
    }

    #endregion
}

/// <summary>
/// Thrown when the API returns 409 Conflict (duplicate resource).
/// </summary>
public class ApiConflictException : Exception
{
    public ApiConflictException(string message) : base(message) { }
}

#region API Response DTOs — Jobs & Cutting Tools

public class ApiJobDto
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

public class ApiJobDetailDto : ApiJobDto
{
    public List<ApiJobPartDto> Parts { get; set; } = new();
    public List<ApiJobStockDto> Stock { get; set; } = new();
}

public class ApiJobPartDto
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

public class ApiJobStockDto
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

public class ApiCuttingToolDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal KerfInches { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

public class ApiPackResponseDto
{
    public List<ApiMaterialPackResultDto> Materials { get; set; } = new();
    public ApiPackingSummaryDto Summary { get; set; } = new();
}

public class ApiMaterialPackResultDto
{
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public List<ApiPackedBinDto> InStockBins { get; set; } = new();
    public List<ApiPackedBinDto> ToBePurchasedBins { get; set; } = new();
    public List<ApiPackedItemDto> ItemsNotPlaced { get; set; } = new();
    public ApiMaterialPackingSummaryDto Summary { get; set; } = new();
}

public class ApiPackedBinDto
{
    public double LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public double UsedInches { get; set; }
    public string UsedFormatted { get; set; } = string.Empty;
    public double WasteInches { get; set; }
    public string WasteFormatted { get; set; } = string.Empty;
    public double Efficiency { get; set; }
    public List<ApiPackedItemDto> Items { get; set; } = new();
}

public class ApiPackedItemDto
{
    public string Name { get; set; } = string.Empty;
    public double LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
}

public class ApiPackingSummaryDto
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
    public List<ApiMaterialPackingSummaryDto> MaterialSummaries { get; set; } = new();
}

public class ApiMaterialPackingSummaryDto
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

#endregion

#region API Response DTOs — Inventory

public class ApiSupplierDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactInfo { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public class ApiMaterialDto
{
    public int Id { get; set; }
    public string Shape { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Grade { get; set; }
    public string Size { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public ApiMaterialDimensionsDto? Dimensions { get; set; }
}

public class ApiMaterialDimensionsDto
{
    public string DimensionType { get; set; } = string.Empty;
    public Dictionary<string, decimal> Values { get; set; } = new();
}

public class ApiStockItemDto
{
    public int Id { get; set; }
    public int MaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal LengthInches { get; set; }
    public string LengthFormatted { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int QuantityOnHand { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public class ApiOfferingDto
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public int StockItemId { get; set; }
    public string? MaterialName { get; set; }
    public decimal? LengthInches { get; set; }
    public string? LengthFormatted { get; set; }
    public string? PartNumber { get; set; }
    public string? SupplierDescription { get; set; }
    public decimal? Price { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

#endregion
