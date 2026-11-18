namespace Filee.Web.Jobs;

/// <summary>One configuration source for file, concurrency and SSE budgets.</summary>
/// <remarks>
/// There is no global job timeout: each engine has its own stall timeout (e.g. FFmpeg's and
/// calibre's). A previously configured jobTimeoutMinutes was never wired up and has been removed
/// so the config does not promise behaviour that does not exist.
/// </remarks>
public sealed record WebLimits(
    long MaxFileBytes,
    int MaxConcurrency,
    int SseThrottleMs,
    int SseBufferSize,
    int SseHeartbeatSeconds)
{
    /// <summary>Test/diagnostic defaults; the runtime source of truth is AppConfig.Limits.</summary>
    public static WebLimits Default { get; } = new(500L * 1024 * 1024, 4, 150, 200, 15);

    /// <summary>Builds limits from the unified config file (env overrides already applied).</summary>
    public static WebLimits FromConfig(Filee.Web.Configuration.AppConfig config) => new(
        config.Limits.MaxFileBytes, config.Limits.MaxConcurrency,
        config.Limits.SseThrottleMs, config.Limits.SseBufferSize, config.Limits.SseHeartbeatSeconds);
}
