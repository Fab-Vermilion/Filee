using Microsoft.Extensions.Hosting;

namespace Filee.Web.Jobs;

/// <summary>
/// Background host service that removes stale per-job upload/output directories. Implements the plan's §A3
/// temporary-file TTL cleanup. File/record/token retention are configured in AppConfig.Retention (default 24h
/// each) so a user can finish a download within the window, then data is reclaimed on an always-on server.
/// </summary>
public sealed class JobDirectoryCleaner(WebStoragePaths storage, JobStore store, TimeSpan ttl, TimeSpan interval, ILogger<JobDirectoryCleaner> logger)
    : BackgroundService
{
    public JobDirectoryCleaner(WebStoragePaths storage, JobStore store, ILogger<JobDirectoryCleaner> logger)
        : this(storage, store, WebRetention.FileAndRecordTtl, WebRetention.CleanupInterval, logger) { }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(interval, stoppingToken); // let startup settle before first sweep
        while (!stoppingToken.IsCancellationRequested)
        {
            try { CleanOnce(DateTimeOffset.UtcNow - ttl); }
            catch (Exception ex) { logger.LogWarning(ex, "job directory cleanup failed"); }
            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Deletes job directories whose last write is older than <paramref name="cutoff"/>. Testable.</summary>
    public int CleanOnce(DateTimeOffset cutoff)
    {
        var removed = 0;
        foreach (var root in new[] { storage.Uploads, storage.Output, storage.Work })
        {
            if (!Directory.Exists(root)) continue;
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                try
                {
                    // A directory's own mtime changes on rename/permission edits, not when a file
                    // inside it is touched. Use the newest file mtime as the activity signal.
                    var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                    var last = files.Length == 0
                        ? Directory.GetLastWriteTimeUtc(dir)
                        : files.Max(File.GetLastWriteTimeUtc);
                    if (last >= cutoff.UtcDateTime) continue;
                    var webId = Path.GetFileName(dir);
                    Directory.Delete(dir, recursive: true);
                    // Files and their summary record share the same lifetime: once the output dir
                    // is reclaimed the history row is no longer usable (no downloadable files).
                    if (string.Equals(Path.GetFileName(root), Path.GetFileName(storage.Output), StringComparison.Ordinal))
                        store.Delete(webId);
                    removed++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "could not remove stale directory {Dir}", dir);
                }
            }
        }
        return removed;
    }
}
