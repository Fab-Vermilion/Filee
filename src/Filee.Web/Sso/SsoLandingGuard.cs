namespace Filee.Web.Sso;

/// <summary>Guards the two untrusted landing parameters: iss and target_link_uri.</summary>
public static class SsoLandingGuard
{
    public static bool IsTrustedIssuer(SsoOptions options, string? issuer) =>
        !string.IsNullOrWhiteSpace(options.Issuer)
        && string.Equals(options.Issuer, issuer, StringComparison.Ordinal);

    public static bool IsAllowedTarget(SsoOptions options, string? target, string requestHost)
    {
        if (string.IsNullOrWhiteSpace(target))
            return false;

        // Same-origin relative paths are safe; protocol-relative URLs are not.
        if (target.StartsWith('/') && !target.StartsWith("//"))
            return true;

        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
            return false;

        var origin = uri.GetLeftPart(UriPartial.Authority);
        if (options.AllowedRedirectOrigins.Any(allowed => string.Equals(allowed, origin, StringComparison.OrdinalIgnoreCase)))
            return true;

        return string.Equals(new Uri(requestHost).GetLeftPart(UriPartial.Authority), origin, StringComparison.OrdinalIgnoreCase);
    }
}
