using System.Collections.Concurrent;

namespace Filee.Web.Jobs;

/// <summary>
/// Short-lived 128-bit download tokens bound to one tenant/subject/job triple. A token may also pin a
/// specific output path so a batch's N download links point at N distinct files, not all at the first one.
/// </summary>
public sealed class DownloadTokenService
{
    private sealed record TokenEntry(string Tid, string Sub, string JobId, string? OutputPath, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new(StringComparer.Ordinal);
    private readonly TimeSpan _ttl;

    public DownloadTokenService(TimeSpan? ttl = null) => _ttl = ttl ?? WebRetention.DownloadTokenTtl;

    public string Issue(string tid, string sub, string jobId) => Issue(tid, sub, jobId, null);

    public string Issue(string tid, string sub, string jobId, string? outputPath)
    {
        var token = WebJobId.New();
        _tokens[token] = new TokenEntry(tid, sub, jobId, outputPath, DateTimeOffset.UtcNow + _ttl);
        return token;
    }

    public bool TryResolve(string token, string tid, string sub, out string jobId)
    {
        jobId = string.Empty;
        if (!TryGet(token, tid, sub, out var entry))
            return false;
        jobId = entry.JobId;
        return true;
    }

    public bool TryResolvePath(string token, string tid, string sub, out string outputPath)
    {
        outputPath = string.Empty;
        if (!TryGet(token, tid, sub, out var entry) || string.IsNullOrEmpty(entry.OutputPath))
            return false;
        outputPath = entry.OutputPath!;
        return true;
    }

    private bool TryGet(string token, string tid, string sub, out TokenEntry entry)
    {
        if (_tokens.TryGetValue(token, out entry!)
            && entry.ExpiresAt > DateTimeOffset.UtcNow
            && string.Equals(entry.Tid, tid, StringComparison.Ordinal)
            && string.Equals(entry.Sub, sub, StringComparison.Ordinal))
            return true;
        if (entry is not null && entry.ExpiresAt <= DateTimeOffset.UtcNow)
            _tokens.TryRemove(token, out _);
        return false;
    }
}
