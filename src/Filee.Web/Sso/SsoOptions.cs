namespace Filee.Web.Sso;

/// <summary>
/// SSO configuration. Values come from environment variables in the hosting process; secrets are never
/// read from the repository or echoed to logs/responses.
/// </summary>
public sealed record SsoOptions(
    string Issuer,
    string Audience,
    string ClientId,
    string ClientSecret,
    IReadOnlyCollection<string> AllowedRedirectOrigins,
    string? RedirectUri = null)
{
    /// <summary>Development callback default; the authoritative value lives in AppConfig/JSON.</summary>
    public const string DefaultRedirectUri = Filee.Web.Configuration.AppServerConfig.DefaultRedirectUri;

    public string EffectiveRedirectUri =>
        string.IsNullOrWhiteSpace(RedirectUri) ? DefaultRedirectUri : RedirectUri!;

    public static SsoOptions FromEnvironment() => new(
        Environment.GetEnvironmentVariable("FILEE_WEB_ISSUER") ?? "",
        Environment.GetEnvironmentVariable("FILEE_WEB_AUDIENCE") ?? "",
        Environment.GetEnvironmentVariable("FILEE_WEB_CLIENT_ID") ?? "",
        Environment.GetEnvironmentVariable("FILEE_WEB_CLIENT_SECRET") ?? "",
        (Environment.GetEnvironmentVariable("FILEE_WEB_ALLOWED_ORIGINS") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        Environment.GetEnvironmentVariable("FILEE_WEB_REDIRECT_URI"));

    /// <summary>Builds SSO options from the unified config file (env overrides already applied).</summary>
    public static SsoOptions FromConfig(Filee.Web.Configuration.AppConfig config) => new(
        config.Sso.Issuer, config.Sso.Audience, config.Sso.ClientId, config.Sso.ClientSecret,
        config.Sso.AllowedRedirectOrigins, config.Server.RedirectUri);
}

public sealed record SsoIdentity(string Tid, string Sub);
