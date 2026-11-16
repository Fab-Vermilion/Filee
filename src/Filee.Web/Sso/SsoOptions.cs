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
    IReadOnlyCollection<string> AllowedRedirectOrigins)
{
    public static SsoOptions FromEnvironment() => new(
        Environment.GetEnvironmentVariable("FILEE_WEB_ISSUER") ?? "",
        Environment.GetEnvironmentVariable("FILEE_WEB_AUDIENCE") ?? "",
        Environment.GetEnvironmentVariable("FILEE_WEB_CLIENT_ID") ?? "",
        Environment.GetEnvironmentVariable("FILEE_WEB_CLIENT_SECRET") ?? "",
        (Environment.GetEnvironmentVariable("FILEE_WEB_ALLOWED_ORIGINS") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

public sealed record SsoIdentity(string Tid, string Sub);
