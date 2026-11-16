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
    /// <summary>Local development callback registered in the self-built app console.</summary>
    public const string DefaultRedirectUri = "https://127.0.0.1:18799/sso";

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
}

public sealed record SsoIdentity(string Tid, string Sub);
