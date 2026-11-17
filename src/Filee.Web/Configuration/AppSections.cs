using System.Text.Json;

namespace Filee.Web.Configuration;

/// <summary>HTTP listener and self-built-app callback settings (previously hardcoded).</summary>
public sealed record AppServerConfig(string Urls, string RedirectUri)
{
    public const string DefaultUrls = "https://127.0.0.1:18799";
    public const string DefaultRedirectUri = DefaultUrls + "/sso";

    public static AppServerConfig From(JsonElement? root, string configPath)
    {
        var section = AppConfig.Section(root, "Server");
        var urls = AppConfig.Str(section, "urls", "Urls", "ASPNETCORE_URLS")
                   ?? AppConfig.Str(root, "urls", "Urls", "ASPNETCORE_URLS")
                   ?? DefaultUrls;
        var redirect = AppConfig.Str(section, "redirectUri", "RedirectUri", "FILEE_WEB_REDIRECT_URI")
                       ?? DefaultRedirectUri;
        return new AppServerConfig(urls, redirect);
    }
}

/// <summary>OIDC / self-built app SSO settings. Secrets come from the local JSON or env vars.</summary>
public sealed record AppSsoConfig(
    string Issuer, string Audience, string ClientId, string ClientSecret,
    string[] AllowedRedirectOrigins, string WellKnownPath)
{
    public const string DefaultWellKnownPath = "/.well-known/openid-configuration";

    public static AppSsoConfig From(JsonElement? root)
    {
        var section = AppConfig.Section(root, "Sso");
        // issuer/client id/secret historically came from env; env still wins, JSON is the new home.
        var clientId = AppConfig.Str(section, "clientId", "ClientId", "FILEE_WEB_CLIENT_ID") ?? "";
        return new AppSsoConfig(
            Issuer: AppConfig.Str(section, "issuer", "Issuer", "FILEE_WEB_ISSUER") ?? "",
            Audience: AppConfig.Str(section, "audience", "Audience", "FILEE_WEB_AUDIENCE") ?? clientId,
            ClientId: clientId,
            ClientSecret: AppConfig.Str(section, "clientSecret", "ClientSecret", "FILEE_WEB_CLIENT_SECRET") ?? "",
            AllowedRedirectOrigins: AppConfig.List(section, "allowedRedirectOrigins", "AllowedRedirectOrigins", "FILEE_WEB_ALLOWED_ORIGINS"),
            WellKnownPath: AppConfig.Str(section, "wellKnownPath", "WellKnownPath") ?? DefaultWellKnownPath);
    }
}

/// <summary>File, concurrency, timeout and SSE budgets; previously FILEE_WEB_* environment variables.</summary>
public sealed record AppLimitsConfig(
    long MaxFileBytes, int MaxConcurrency, int JobTimeoutMinutes,
    int SseThrottleMs, int SseBufferSize, int SseHeartbeatSeconds, int LoWorkers)
{
    public static AppLimitsConfig From(JsonElement? root)
    {
        var s = AppConfig.Section(root, "Limits");
        return new AppLimitsConfig(
            MaxFileBytes: AppConfig.Long(s, "maxFileBytes", "MaxFileBytes", "FILEE_WEB_MAX_FILE_BYTES", 500L * 1024 * 1024),
            MaxConcurrency: AppConfig.Int(s, "maxConcurrency", "MaxConcurrency", "FILEE_WEB_MAX_CONCURRENCY", 4),
            JobTimeoutMinutes: AppConfig.Int(s, "jobTimeoutMinutes", "JobTimeoutMinutes", "FILEE_WEB_JOB_TIMEOUT_MINUTES", 10),
            SseThrottleMs: AppConfig.Int(s, "sseThrottleMs", "SseThrottleMs", "FILEE_WEB_SSE_THROTTLE_MS", 150),
            SseBufferSize: AppConfig.Int(s, "sseBufferSize", "SseBufferSize", "FILEE_WEB_SSE_BUFFER_SIZE", 200),
            SseHeartbeatSeconds: AppConfig.Int(s, "sseHeartbeatSeconds", "SseHeartbeatSeconds", "FILEE_WEB_SSE_HEARTBEAT_SECONDS", 15),
            LoWorkers: AppConfig.Int(s, "loWorkers", "LoWorkers", "FILEE_LO_WORKERS", 1));
    }
}

/// <summary>Local HTTPS certificate settings (previously env vars + a hardcoded password in dev-up.sh).</summary>
public sealed record AppTlsConfig(string? PfxPath, string Password)
{
    public static AppTlsConfig From(JsonElement? root)
    {
        var s = AppConfig.Section(root, "Tls");
        return new AppTlsConfig(
            PfxPath: AppConfig.Str(s, "pfxPath", "PfxPath", "FILEE_WEB_TLS_PFX"),
            Password: AppConfig.Str(s, "password", "Password", "FILEE_WEB_TLS_PASSWORD") ?? "");
    }
}
