using Filee.Core.Conversion;
using Filee.Web.Contracts;

namespace Filee.Web.Jobs;

/// <summary>
/// Resolves the status to persist when a job finishes. Normally the engine's own terminal state is used.
/// As a safety net, a job still marked Running whose every source file has reached a terminal state and
/// whose progress is complete is resolved from its files — otherwise the UI would show "进行中" forever
/// even though all outputs exist and are downloadable.
/// </summary>
public static class JobFinalStatus
{
    public static string Resolve(ConversionJob job)
    {
        var mapped = JobStatusMapper.ToApi(job.State);
        if (IsTerminal(mapped))
            return mapped;

        // File progress is engine-reported and can settle below 1.0 for a successful batch (the job
        // average is what /api/jobs exposes). Use per-file terminal state, not the numeric average.
        var files = job.Files;
        if (files.Count == 0 || files.Any(f => !IsFileTerminal(f.State)))
            return mapped;

        if (files.All(f => f.State is FileState.Done or FileState.Skipped))
            return "completed";
        if (files.Any(f => f.State == FileState.Done))
            return "completed_with_errors";
        return "failed";
    }

    private static bool IsTerminal(string status) => status is
        "completed" or "completed_with_errors" or "failed" or "cancelled" or "interrupted";

    private static bool IsFileTerminal(FileState state) => state is
        FileState.Done or FileState.Skipped or FileState.Failed or FileState.Cancelled;
}
