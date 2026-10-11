using System.ComponentModel;
using ModelContextProtocol.Server;

namespace CutList.Mcp;

[McpServerToolType]
public class PartSetTools(ApiClient api)
{
    [McpServerTool(Name = "list_part_sets"), Description("Lists reusable drawing sets, optionally searched by drawing/name. Archived sets are hidden by default.")]
    public Task<PartSetResult<List<ApiPartSetDto>>> ListPartSets(string? search = null, bool includeArchived = false) =>
        Run(() => api.GetPartSetsAsync(search, includeArchived));

    [McpServerTool(Name = "get_part_set"), Description("Reads a drawing set and its ordered explicit cuts, including archived sets.")]
    public Task<PartSetResult<ApiPartSetDetailDto>> GetPartSet(int partSetId) => Run(() => api.GetPartSetAsync(partSetId));

    [McpServerTool(Name = "create_part_set"), Description("Creates a reusable drawing set from operator-reviewed explicit members. No spreadsheet parsing, upload or material inference. Requires operatorReviewed=true after the operator reviews material IDs, exact decimal inch lengths, integer per-assembly quantities and source text. Returns persisted readback.")]
    public Task<PartSetResult<ApiPartSetDetailDto>> CreatePartSet(string drawingNumber, string displayName,
        PartSetMemberInput[] members, bool operatorReviewed, string? revision = null) =>
        Save(null, drawingNumber, displayName, members, operatorReviewed, revision);

    [McpServerTool(Name = "update_part_set"), Description("Fully replaces drawing/revision/name and ALL members using operator-reviewed values; not a patch. Requires operatorReviewed=true. Does not change existing job snapshots. No parsing or inference. Returns persisted readback.")]
    public Task<PartSetResult<ApiPartSetDetailDto>> UpdatePartSet(int partSetId, string drawingNumber, string displayName,
        PartSetMemberInput[] members, bool operatorReviewed, string? revision = null) =>
        Save(partSetId, drawingNumber, displayName, members, operatorReviewed, revision);

    [McpServerTool(Name = "archive_part_set"), Description("Archives a drawing set without deleting it or changing job snapshots. Returns archived readback.")]
    public Task<PartSetResult<ApiPartSetDetailDto>> ArchivePartSet(int partSetId) => Run(() => api.ArchivePartSetAsync(partSetId));

    [McpServerTool(Name = "add_job_part_set"), Description("Atomically adds a drawing-set snapshot to an unlocked job with a positive integer assembly quantity. Expands per-assembly quantities into ordinary cut rows; stock must be configured separately. Returns full job readback. Lock conflicts are not retried; unlock explicitly in the web UI.")]
    public Task<JobDetailResult> AddJobPartSet(int jobId, int partSetId, int quantity) => JobRun(() => api.AddJobPartSetAsync(jobId, partSetId, quantity));

    [McpServerTool(Name = "remove_job_part_set_instance"), Description("Atomically removes one whole drawing-set instance from its owning unlocked job, never manual parts or other instances. Use the instance ID from get_job, not the library ID. Returns full job readback. No lock retries or automatic unlock.")]
    public Task<JobDetailResult> RemoveJobPartSetInstance(int jobId, int instanceId) => JobRun(() => api.RemoveJobPartSetInstanceAsync(jobId, instanceId));

    private Task<PartSetResult<ApiPartSetDetailDto>> Save(int? id, string drawingNumber, string displayName,
        PartSetMemberInput[] members, bool reviewed, string? revision)
    {
        if (!reviewed) return Task.FromResult(new PartSetResult<ApiPartSetDetailDto>
            { Success = false, Error = "Operator review is required; no values are inferred or parsed automatically." });
        return Run(() => api.SavePartSetAsync(id, drawingNumber, revision, displayName, members));
    }

    private static async Task<PartSetResult<T>> Run<T>(Func<Task<T>> action)
    {
        try { return new() { Success = true, Data = await action() }; }
        catch (HttpRequestException ex) { return new() { Success = false, Error = ex.Message }; }
    }

    private static async Task<JobDetailResult> JobRun(Func<Task<ApiJobDetailDto>> action)
    {
        try { return new() { Success = true, Job = JobTools.MapJobDetail(await action()) }; }
        catch (HttpRequestException ex) { return new() { Success = false, Error = ex.Message }; }
    }
}
