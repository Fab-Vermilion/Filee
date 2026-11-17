using Filee.Core.Conversion;
using Filee.Web.Conversion;
using Filee.Web.Contracts;
using Filee.Web.Security;

namespace Filee.Web.Jobs;

/// <summary>Builds the wire snapshot for a job, shared by /api/jobs/{id} and the list endpoint.</summary>
public static class JobSnapshots
{
    public static JobSnapshot Build(JobRecord record, WebJobRegistry registry, WebStoragePaths storage,
        DownloadTokenService tokens, SessionIdentity identity)
    {
        var job = registry.Get(record.WebId);
        var files = job is not null
            ? job.Files.Select((file, index) =>
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
            }).ToArray()
            : BuildFromPersistedNames(record, storage, tokens, identity);

        var archiveUrl = files.Any(f => f.DownloadUrl is not null)
            ? $"/api/download/all?token={tokens.Issue(identity.Tid, identity.Sub, record.WebId)}"
            : null;
        return JobSnapshot.From(record) with { Files = files, ArchiveUrl = archiveUrl };
    }

    /// <summary>
    /// After a restart the in-memory registry is empty, but output files and their names are durable.
    /// Rebuild a downloadable file list from the persisted names so history stays useful within TTL.
    /// </summary>
    private static JobFileSnapshot[] BuildFromPersistedNames(JobRecord record, WebStoragePaths storage,
        DownloadTokenService tokens, SessionIdentity identity)
    {
        var directory = storage.OutputDirectory(record.WebId);
        var result = new List<JobFileSnapshot>();
        var index = 0;
        foreach (var name in record.OutputFiles ?? [])
        {
            index++;
            var path = Path.Combine(directory, name);
            var exists = File.Exists(path);
            var downloadUrl = exists
                ? $"/api/download?token={tokens.Issue(identity.Tid, identity.Sub, record.WebId, path)}"
                : null;
            var size = exists ? new FileInfo(path).Length : 0;
            result.Add(new JobFileSnapshot(
                $"f{index}", name, size, exists ? "done" : "missing", exists ? 1 : 0,
                exists ? null : ApiErrorEnvelope.Create("expired", "文件已清理").Error, downloadUrl));
        }
        return result.ToArray();
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
