using System.Net.Http.Headers;
using System.Text.Json;

namespace Filee.Web.Sso;

/// <summary>Exchanges the one-time authorization code over the configured issuer's token endpoint.</summary>
public sealed class SsoTokenClient(HttpClient http, SsoOptions options)
{
    public async Task<string> ExchangeAsync(string code)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Issuer.TrimEnd('/')}/token");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = options.EffectiveRedirectUri,
            ["client_id"] = options.ClientId,
        });

        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id_token").GetString()
            ?? throw new InvalidOperationException("Token endpoint returned no id_token.");
    }
}
