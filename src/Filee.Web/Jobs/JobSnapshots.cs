using Filee.Core.Conversion;
using Filee.Web.Conversion;
using Filee.Web.Contracts;
using Filee.Web.Security;

namespace Filee.Web.Jobs;

/// <summary>Builds the wire snapshot for a job, shared by /api/jobs/{id} and the list endpoint.</summary>
public static class JobSnapshots
{
    public static JobSnapshot Build(JobRecord record, WebJobRegistry registry,
        DownloadTokenService tokens, SessionIdentity identity)
    {
        var job = registry.Get(record.WebId);
        var files = job is null
            ? []
            : job.Files.Select((file, index) =>
            {
                var hasOutput = registry.TryGetOutput(record.WebId, index, out var outputPath);
                var downloadUrl = hasOutput
                    ? $"/api/download?token={tokens.Issue(identity.Tid, identity.Sub, record.WebId, outputPath)}"
                    : null;
                return new JobFileSnapshot(
                    $"f{index + 1}",
                    DownloadDisplay.Name(Path.GetFileName(file.SourcePath), record.Target, hasOutput ? outputPath : null),
                    file.Transfer?.Bytes ?? 0,
                    ToFileStatus(file.State),
                    file.Progress,
                    file.ErrorKey is null ? null : ApiErrorEnvelope.Create("conversion_failed", "转换失败，请重试").Error,
                    downloadUrl);
            }).ToArray();

        var archiveUrl = files.Any(f => f.DownloadUrl is not null)
            ? $"/api/download/all?token={tokens.Issue(identity.Tid, identity.Sub, record.WebId)}"
            : null;
        return JobSnapshot.From(record) with { Files = files, ArchiveUrl = archiveUrl };
    }

    private static string ToFileStatus(FileState state) => state switch
    {
        FileState.Pending => "pending",
        FileState.Running => "running",
        FileState.Done => "done",
        FileState.Skipped => "skipped",
        FileState.Failed => "failed",
        FileState.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
}
