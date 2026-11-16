using System.Collections.Concurrent;

namespace Filee.Web.Jobs;

/// <summary>Short-lived 128-bit download tokens bound to one tenant/subject/job triple.</summary>
public sealed class DownloadTokenService
{
    private sealed record TokenEntry(string Tid, string Sub, string JobId, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new(StringComparer.Ordinal);
    private readonly TimeSpan _ttl;

    public DownloadTokenService(TimeSpan? ttl = null) => _ttl = ttl ?? TimeSpan.FromHours(1);

    public string Issue(string tid, string sub, string jobId)
    {
        var token = WebJobId.New();
        _tokens[token] = new TokenEntry(tid, sub, jobId, DateTimeOffset.UtcNow + _ttl);
        return token;
    }

    public bool TryResolve(string token, string tid, string sub, out string jobId)
    {
        jobId = string.Empty;
        if (!_tokens.TryGetValue(token, out var entry))
            return false;
        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _tokens.TryRemove(token, out _);
            return false;
        }
        if (!string.Equals(entry.Tid, tid, StringComparison.Ordinal)
            || !string.Equals(entry.Sub, sub, StringComparison.Ordinal))
            return false;

        jobId = entry.JobId;
        return true;
    }
}
