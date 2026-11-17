using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Filee.Web.Sso;

/// <summary>
/// Minimal RS256 id_token validation: fixed issuer, configured audience, JWKS by kid, one refresh on unknown kid,
/// fail-closed otherwise.
/// </summary>
public sealed class OidcTokenValidator(JwksProvider jwks, string issuer, string audience, TimeSpan? clockSkew = null)
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly TimeSpan _clockSkew = clockSkew ?? TimeSpan.FromMinutes(5);

    public async Task<SsoIdentity> ValidateAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new SecurityTokenException("id_token is missing.");

        var keys = await jwks.GetKeysAsync();
        var result = await ValidateCoreAsync(token, keys);
        if (!result.IsValid)
        {
            var kid = _handler.ReadJsonWebToken(token).Kid;
            if (!string.IsNullOrEmpty(kid) && keys.All(key => key.KeyId != kid))
            {
                keys = await jwks.GetKeysAsync(forceRefresh: true);
                result = await ValidateCoreAsync(token, keys);
            }
        }

        if (!result.IsValid)
            throw result.Exception ?? new SecurityTokenException("id_token validation failed.");

        var tid = result.ClaimsIdentity?.FindFirst("tid")?.Value;
        var sub = result.ClaimsIdentity?.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(tid) || string.IsNullOrEmpty(sub))
            throw new SecurityTokenException("id_token has no tenant or subject claim.");

        return new SsoIdentity(tid, sub);
    }

    private Task<TokenValidationResult> ValidateCoreAsync(string token, IReadOnlyList<SecurityKey> keys) =>
        _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = keys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = _clockSkew,
        });
}
