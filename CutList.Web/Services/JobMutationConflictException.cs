namespace CutList.Web.Services;

/// <summary>
/// Thrown when a job mutation could not be saved because the job's lock state changed (or the job
/// was removed) after it was read, and the job is not locked when re-read. The change is not retried;
/// the caller must reload the job and decide again.
/// </summary>
public sealed class JobMutationConflictException : Exception
{
    public JobMutationConflictException(int jobId, Exception? innerException = null)
        : base($"Job {jobId} changed before your change could be saved. Reload the job and try again.",
            innerException)
    {
        JobId = jobId;
    }

    public int JobId { get; }
}
