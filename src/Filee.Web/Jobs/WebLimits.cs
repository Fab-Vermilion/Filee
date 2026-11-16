namespace Filee.Web.Jobs;

/// <summary>One configuration source for file, concurrency, timeout and SSE budgets.</summary>
public sealed record WebLimits(
    long MaxFileBytes,
    int MaxConcurrency,
    TimeSpan JobTimeout,
    int SseThrottleMs,
    int SseBufferSize,
    int SseHeartbeatSeconds)
{
    public static WebLimits Default { get; } = new(
        500L * 1024 * 1024, 4, TimeSpan.FromMinutes(10), 150, 200, 15);

    public static WebLimits FromEnvironment() => new(
        ReadLong("FILEE_WEB_MAX_FILE_BYTES", Default.MaxFileBytes),
        ReadInt("FILEE_WEB_MAX_CONCURRENCY", Default.MaxConcurrency),
        TimeSpan.FromMinutes(ReadInt("FILEE_WEB_JOB_TIMEOUT_MINUTES", 10)),
        ReadInt("FILEE_WEB_SSE_THROTTLE_MS", Default.SseThrottleMs),
        ReadInt("FILEE_WEB_SSE_BUFFER_SIZE", Default.SseBufferSize),
        ReadInt("FILEE_WEB_SSE_HEARTBEAT_SECONDS", Default.SseHeartbeatSeconds));

    private static long ReadLong(string name, long fallback) =>
        long.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0 ? value : fallback;

    private static int ReadInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0 ? value : fallback;
}
