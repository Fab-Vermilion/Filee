using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Filee.Web.Sso;

/// <summary>
/// Discovers jwks_uri from the configured issuer only, caches signing keys and allows one explicit refresh
/// when an unknown kid arrives.
/// </summary>
public sealed class JwksProvider
{
    private readonly HttpClient _http;
    private readonly string _issuer;
    private readonly string _wellKnownPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<SecurityKey>? _keys;

    public JwksProvider(HttpClient http, string issuer, string? wellKnownPath = null)
    {
        _http = http;
        _issuer = issuer.TrimEnd('/');
        _wellKnownPath = string.IsNullOrWhiteSpace(wellKnownPath)
            ? "/.well-known/openid-configuration"
            : wellKnownPath!;
    }

    public async Task<IReadOnlyList<SecurityKey>> GetKeysAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _keys is not null)
            return _keys;

        await _gate.WaitAsync();
        try
        {
            if (!forceRefresh && _keys is not null)
                return _keys;

            using var discoveryResponse = await _http.GetAsync($"{_issuer}{_wellKnownPath}");
            discoveryResponse.EnsureSuccessStatusCode();
            using var discovery = JsonDocument.Parse(await discoveryResponse.Content.ReadAsStringAsync());
            var jwksUri = discovery.RootElement.GetProperty("jwks_uri").GetString();
            if (!Uri.TryCreate(jwksUri, UriKind.Absolute, out var jwks)
                || jwks.Scheme != Uri.UriSchemeHttps
                || !string.Equals(jwks.GetLeftPart(UriPartial.Authority), new Uri(_issuer).GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
                throw new SecurityTokenException("JWKS endpoint is outside the configured issuer.");

            using var jwksResponse = await _http.GetAsync(jwks);
            jwksResponse.EnsureSuccessStatusCode();
            _keys = new JsonWebKeySet(await jwksResponse.Content.ReadAsStringAsync()).GetSigningKeys().ToArray();
            return _keys;
        }
        finally
        {
            _gate.Release();
        }
    }
}
