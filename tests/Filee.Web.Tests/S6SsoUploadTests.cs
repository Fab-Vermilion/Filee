using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Filee.Web.Jobs;
using Filee.Web.Security;
using Filee.Web.Sso;
using Filee.Web.Streaming;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Filee.Web.Tests;

public sealed class S6SsoUploadTests
{
    private const string Issuer = "https://sso.example.test";
    private const string Audience = "filee-web-audience";

    [Fact]
    public void SsoLandingGuard_AcceptsConfiguredIssuerAndRelativeTargetOnly()
    {
        var options = new SsoOptions(Issuer, Audience, "client", "secret", ["https://client.example.test"]);

        Assert.True(SsoLandingGuard.IsTrustedIssuer(options, Issuer));
        Assert.False(SsoLandingGuard.IsTrustedIssuer(options, "https://evil.example.test"));
        Assert.True(SsoLandingGuard.IsAllowedTarget(options, "/index.html", Issuer));
        Assert.True(SsoLandingGuard.IsAllowedTarget(options, "https://client.example.test/app", Issuer));
        Assert.False(SsoLandingGuard.IsAllowedTarget(options, "https://evil.example.test/app", Issuer));
        Assert.False(SsoLandingGuard.IsAllowedTarget(options, "//evil.example.test/app", Issuer));
        Assert.Equal("https://127.0.0.1:18799/sso", options.EffectiveRedirectUri);
    }

    [Fact]
    public async Task SsoTokenClient_UsesRegisteredCallbackAsRedirectUri()
    {
        var handler = new TokenCaptureHandler();
        var options = new SsoOptions(Issuer, Audience, "client", "secret", ["https://client.example.test"]);
        var client = new SsoTokenClient(new HttpClient(handler), options);

        await client.ExchangeAsync("code-1");

        Assert.Equal("https://127.0.0.1:18799/sso", handler.RedirectUri);
    }

    [Fact]
    public async Task OidcTokenValidator_AcceptsSignedTokenWithConfiguredIssuerAndAudience()
    {
        using var rsa = RSA.Create(2048);
        var handler = new StubHttpHandler(() => Jwks(rsa, "key-1"));
        var validator = NewValidator(handler);
        var token = CreateToken(rsa, "key-1", Issuer, Audience, "user-1", "tenant-1");

        var identity = await validator.ValidateAsync(token);

        Assert.Equal("tenant-1", identity.Tid);
        Assert.Equal("user-1", identity.Sub);
        Assert.Equal(1, handler.JwksCalls);
    }

    [Fact]
    public async Task OidcTokenValidator_RejectsWrongIssuerOrAudience()
    {
        using var rsa = RSA.Create(2048);
        var validator = NewValidator(new StubHttpHandler(() => Jwks(rsa, "key-1")));
        var wrongIssuer = CreateToken(rsa, "key-1", "https://evil.example.test", Audience, "user-1", "tenant-1");
        var wrongAudience = CreateToken(rsa, "key-1", Issuer, "other-audience", "user-1", "tenant-1");

        await Assert.ThrowsAnyAsync<SecurityTokenException>(() => validator.ValidateAsync(wrongIssuer));
        await Assert.ThrowsAnyAsync<SecurityTokenException>(() => validator.ValidateAsync(wrongAudience));
    }

    [Fact]
    public async Task OidcTokenValidator_RefreshesJwksOnceForUnknownKid()
    {
        using var first = RSA.Create(2048);
        using var second = RSA.Create(2048);
        var calls = 0;
        var handler = new StubHttpHandler(() => ++calls == 1 ? Jwks(first, "key-1") : Jwks(second, "key-2"));
        var validator = NewValidator(handler);
        var token = CreateToken(second, "key-2", Issuer, Audience, "user-2", "tenant-2");

        var identity = await validator.ValidateAsync(token);

        Assert.Equal("tenant-2", identity.Tid);
        Assert.Equal(2, handler.JwksCalls);
    }

    [Fact]
    public void ReloadMessageParser_ValidatesBChannelOnly()
    {
        var good = """{"type":"wb:reload:response","requestId":"req-1","code":0,"message":"ok","origin":"https://client.example.test"}""";
        Assert.True(ReloadMessageParser.IsValidBMessage(good, "req-1", "https://client.example.test", sourceMatches: true));
        Assert.False(ReloadMessageParser.IsValidBMessage(good, "req-2", "https://client.example.test", sourceMatches: true));
        Assert.False(ReloadMessageParser.IsValidBMessage(good, "req-1", "https://evil.example.test", sourceMatches: true));
        Assert.False(ReloadMessageParser.IsValidBMessage(good, "req-1", "https://client.example.test", sourceMatches: false));
        Assert.False(ReloadMessageParser.IsValidBMessage("""{"type":"wb:reload:response","requestId":"req-1","code":1}""", "req-1", "https://client.example.test", true));
    }

    [Fact]
    public async Task JobSubmissionService_RejectsOptionsAndInvalidTarget()
    {
        var service = NewSubmissionService();
        var files = PngFiles();
        var identity = new SessionIdentity("tenant-1", "user-1", "csrf");

        var optionsError = await Assert.ThrowsAsync<UploadException>(() =>
            service.SubmitAsync(identity, "png", files, hasOptions: true));
        Assert.Equal("unsupported_option", optionsError.Code);

        var targetError = await Assert.ThrowsAsync<UploadException>(() =>
            service.SubmitAsync(identity, "not-a-format", files, hasOptions: false));
        Assert.Equal("invalid_target", targetError.Code);
    }

    [Fact]
    public async Task JobSubmissionService_AcceptsValidPngAndCreatesWebJobId()
    {
        var service = NewSubmissionService();
        var identity = new SessionIdentity("tenant-1", "user-1", "csrf");

        var record = await service.SubmitAsync(identity, "png", PngFiles(), hasOptions: false);

        Assert.Equal(32, record.WebId.Length);
        Assert.Matches("^[0-9a-f]{32}$", record.WebId);
        Assert.Equal("queued", record.Status);
        Assert.True(File.Exists(Path.Combine(service.Storage.UploadDirectory(record.WebId), "photo.png")));
    }

    private static JobSubmissionService NewSubmissionService()
    {
        var root = Path.Combine(AppContext.BaseDirectory, $"s6-submit-{Guid.NewGuid():N}");
        var storage = new WebStoragePaths(root);
        storage.Ensure();
        var store = new JobStore(storage.Database);
        var registry = new WebJobRegistry();
        var hub = new ProgressHub();
        var gate = new WebConcurrencyGate(2);
        var service = new JobSubmissionService(storage, store, registry, hub, gate, _ => Task.CompletedTask);
        return service;
    }

    private static FormFileCollection PngFiles()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var files = new FormFileCollection();
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "files", "photo.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png",
        };
        files.Add(file);
        return files;
    }

    private static OidcTokenValidator NewValidator(StubHttpHandler handler) =>
        new(new JwksProvider(new HttpClient(handler), Issuer), Issuer, Audience);

    private static string CreateToken(RSA rsa, string kid, string issuer, string audience, string sub, string tid)
    {
        var identity = new ClaimsIdentity([new Claim("sub", sub), new Claim("tid", tid)]);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddMinutes(5),
            Subject = identity,
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = kid }, SecurityAlgorithms.RsaSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static string Jwks(RSA rsa, string kid)
    {
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        return JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid,
                    n = Base64UrlEncoder.Encode(parameters.Modulus!),
                    e = Base64UrlEncoder.Encode(parameters.Exponent!),
                },
            },
        });
    }

    private sealed class TokenCaptureHandler : HttpMessageHandler
    {
        public string? RedirectUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var form = await request.Content!.ReadAsStringAsync(cancellationToken);
            foreach (var part in form.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                if (pair.Length == 2 && pair[0] == "redirect_uri")
                    RedirectUri = Uri.UnescapeDataString(pair[1]);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id_token":"test-token"}""", Encoding.UTF8, "application/json"),
            };
        }
    }

    [Fact]
    public async Task JwksProvider_UsesConfiguredWellKnownPath()
    {
        var seen = new List<string>();
        var handler = new CaptureUrlHandler(seen, Issuer);
        var provider = new JwksProvider(new HttpClient(handler), Issuer, "/custom/.well-known/openid-configuration");

        await provider.GetKeysAsync();

        Assert.Contains($"{Issuer}/custom/.well-known/openid-configuration", seen);
    }

    private sealed class CaptureUrlHandler(List<string> seen, string issuer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            seen.Add(request.RequestUri!.ToString());
            if (request.RequestUri!.AbsolutePath.EndsWith("openid-configuration", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { issuer, jwks_uri = issuer + "/jwks" }), Encoding.UTF8, "application/json")
                });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"keys\":[]}", Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StubHttpHandler(Func<string> jwks) : HttpMessageHandler
    {
        private int _jwksCalls;

        public int JwksCalls => Volatile.Read(ref _jwksCalls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            if (url == $"{Issuer}/.well-known/openid-configuration")
                return JsonAsync(JsonSerializer.Serialize(new { issuer = Issuer, jwks_uri = $"{Issuer}/jwks" }));
            if (url == $"{Issuer}/jwks")
            {
                Interlocked.Increment(ref _jwksCalls);
                return JsonAsync(jwks());
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static Task<HttpResponseMessage> JsonAsync(string json) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
    }
}
