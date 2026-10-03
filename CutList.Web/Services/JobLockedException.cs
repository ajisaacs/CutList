namespace CutList.Web.Services;

/// <summary>
/// Thrown when a mutation targets a job whose persisted <c>LockedAt</c> is set (materials ordered).
/// The job must be unlocked explicitly before it can be changed.
/// </summary>
public sealed class JobLockedException : Exception
{
    public JobLockedException(int jobId, DateTime lockedAt, Exception? innerException = null)
        : base($"Job {jobId} is locked because materials have been ordered. Unlock it explicitly before making changes.",
            innerException)
    {
        JobId = jobId;
        LockedAt = DateTime.SpecifyKind(lockedAt, DateTimeKind.Utc);
    }

    public int JobId { get; }

    /// <summary>The persisted lock timestamp (UTC).</summary>
    public DateTime LockedAt { get; }
}
