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
    /// <summary>Test/diagnostic defaults; the runtime source of truth is AppConfig.Limits.</summary>
    public static WebLimits Default { get; } = new(
        500L * 1024 * 1024, 4, TimeSpan.FromMinutes(10), 150, 200, 15);

    /// <summary>Builds limits from the unified config file (env overrides already applied).</summary>
    public static WebLimits FromConfig(Filee.Web.Configuration.AppConfig config) => new(
        config.Limits.MaxFileBytes, config.Limits.MaxConcurrency,
        TimeSpan.FromMinutes(config.Limits.JobTimeoutMinutes),
        config.Limits.SseThrottleMs, config.Limits.SseBufferSize, config.Limits.SseHeartbeatSeconds);
}
