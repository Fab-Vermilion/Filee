using Filee.Core.Conversion;

namespace Filee.Web.Contracts;

/// <summary>Maps Filee job states to the frozen API status strings.</summary>
public static class JobStatusMapper
{
    /// <summary>Web recovery state; Filee's own <see cref="JobState"/> has no equivalent.</summary>
    public const string Interrupted = "interrupted";

    /// <summary>Returns the public status string for an engine job state.</summary>
    public static string ToApi(JobState state) => state switch
    {
        JobState.Queued => "queued",
        JobState.Running => "running",
        JobState.Completed => "completed",
        JobState.CompletedWithErrors => "completed_with_errors",
        JobState.Failed => "failed",
        JobState.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
}
