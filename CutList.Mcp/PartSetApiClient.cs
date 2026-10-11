using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CutList.Mcp;

public partial class ApiClient
{
    public async Task<List<ApiPartSetDto>> GetPartSetsAsync(string? search = null, bool includeArchived = false)
    {
        using var response = await _http.GetAsync($"api/part-sets?includeArchived={includeArchived}&search={Uri.EscapeDataString(search ?? string.Empty)}");
        await EnsurePartSetSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ApiPartSetDto>>() ?? [];
    }

    public async Task<ApiPartSetDetailDto> GetPartSetAsync(int id)
    {
        using var response = await _http.GetAsync($"api/part-sets/{id}");
        await EnsurePartSetSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ApiPartSetDetailDto>()
            ?? throw new HttpRequestException("Drawing-set readback returned no detail.");
    }

    public async Task<ApiPartSetDetailDto> SavePartSetAsync(int? id, string drawingNumber, string? revision,
        string displayName, PartSetMemberInput[] members)
    {
        var body = new { DrawingNumber = drawingNumber, Revision = revision, DisplayName = displayName, Members = members };
        using var response = id.HasValue
            ? await _http.PutAsJsonAsync($"api/part-sets/{id.Value}", body)
            : await _http.PostAsJsonAsync("api/part-sets", body);
        await EnsurePartSetSuccessAsync(response);
        var saved = await response.Content.ReadFromJsonAsync<ApiPartSetDetailDto>()
            ?? throw new HttpRequestException("Drawing-set write returned no detail; read back before retrying.");
        return await GetPartSetAsync(saved.Id);
    }

    public async Task<ApiPartSetDetailDto> ArchivePartSetAsync(int id)
    {
        using var response = await _http.PostAsync($"api/part-sets/{id}/archive", null);
        await EnsurePartSetSuccessAsync(response);
        var saved = await GetPartSetAsync(id);
        if (!saved.IsArchived) throw new HttpRequestException("Archive readback did not confirm archived state.");
        return saved;
    }

    public async Task<ApiJobDetailDto> AddJobPartSetAsync(int jobId, int partSetId, int quantity)
    {
        using var response = await _http.PostAsJsonAsync($"api/jobs/{jobId}/part-set-instances", new { PartSetId = partSetId, Quantity = quantity });
        await EnsurePartSetSuccessAsync(response);
        var instance = await response.Content.ReadFromJsonAsync<ApiJobPartSetInstanceDto>()
            ?? throw new HttpRequestException("Instance write returned no detail; read back before retrying.");
        var job = await GetJobAsync(jobId) ?? throw new HttpRequestException("Job readback returned no detail.");
        if (!job.PartSetInstances.Any(i => i.Id == instance.Id))
            throw new HttpRequestException("Job readback did not confirm the added instance; do not retry automatically.");
        return job;
    }

    public async Task<ApiJobDetailDto> RemoveJobPartSetInstanceAsync(int jobId, int instanceId)
    {
        using var response = await _http.DeleteAsync($"api/jobs/{jobId}/part-set-instances/{instanceId}");
        await EnsurePartSetSuccessAsync(response);
        var job = await GetJobAsync(jobId) ?? throw new HttpRequestException("Job readback returned no detail.");
        if (job.PartSetInstances.Any(i => i.Id == instanceId) || job.Parts.Any(p => p.PartSetInstanceId == instanceId))
            throw new HttpRequestException("Job readback still contains the removed instance.");
        return job;
    }

    // Preserve lock details and validation explanations while retaining the HTTP status.
    private static async Task EnsurePartSetSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            await EnsureJobSuccessAsync(response);
            return;
        }
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var text = await response.Content.ReadAsStringAsync();
            try
            {
                using var json = JsonDocument.Parse(text);
                text = json.RootElement.ValueKind == JsonValueKind.String ? json.RootElement.GetString()! : json.RootElement.ToString();
            }
            catch (JsonException) { }
            throw new HttpRequestException($"Invalid drawing-set request: {text}", null, response.StatusCode);
        }
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new HttpRequestException("Drawing set, job or instance not found for the requested owner (404).", null, response.StatusCode);
        response.EnsureSuccessStatusCode();
    }
}
