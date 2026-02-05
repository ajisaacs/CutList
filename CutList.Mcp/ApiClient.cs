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

#region API Response DTOs

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
