using System.Text.Json;
using Filee.Web.Contracts;
using Filee.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Filee.Web.Tests;

public sealed class S3SessionCsrfTests
{
    [Fact]
    public void SessionService_IssuesSecureSessionAndReadableCsrfCookies()
    {
        var service = new SessionService();
        var context = new DefaultHttpContext();

        var identity = service.Issue(context, "tenant-1", "user-1");

        Assert.Equal("tenant-1", identity.Tid);
        Assert.Equal("user-1", identity.Sub);
        Assert.False(string.IsNullOrWhiteSpace(identity.CsrfToken));

        var session = HeaderFor(context, WebCookieNames.Session);
        ContainsIgnoreCase(session, $"{WebCookieNames.Session}=");
        ContainsIgnoreCase(session, "Secure");
        ContainsIgnoreCase(session, "SameSite=None");
        ContainsIgnoreCase(session, "HttpOnly");
        ContainsIgnoreCase(session, "Path=/");

        var csrf = HeaderFor(context, WebCookieNames.Csrf);
        ContainsIgnoreCase(csrf, $"{WebCookieNames.Csrf}=");
        ContainsIgnoreCase(csrf, "Secure");
        ContainsIgnoreCase(csrf, "SameSite=None");
        ContainsIgnoreCase(csrf, "Path=/");
        Assert.False(csrf.Contains("HttpOnly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SessionService_ReadsIssuedSessionAndRejectsTamperedCookie()
    {
        var service = new SessionService();
        var issued = new DefaultHttpContext();
        service.Issue(issued, "tenant-1", "user-1");
        var sessionId = CookieValue(HeaderFor(issued, WebCookieNames.Session), WebCookieNames.Session);

        var validRequest = new DefaultHttpContext();
        validRequest.Request.Headers.Cookie = $"{WebCookieNames.Session}={sessionId}";
        Assert.True(service.TryRead(validRequest, out var identity));
        Assert.Equal("tenant-1", identity.Tid);
        Assert.Equal("user-1", identity.Sub);

        var tamperedRequest = new DefaultHttpContext();
        tamperedRequest.Request.Headers.Cookie = $"{WebCookieNames.Session}={sessionId}extra";
        Assert.False(service.TryRead(tamperedRequest, out _));
    }

    [Fact]
    public void SessionService_RefreshReplacesOldSessionAndCsrfToken()
    {
        var service = new SessionService();
        var first = new DefaultHttpContext();
        var firstIdentity = service.Issue(first, "tenant-1", "user-1");
        var firstSessionId = CookieValue(HeaderFor(first, WebCookieNames.Session), WebCookieNames.Session);

        var refresh = new DefaultHttpContext();
        refresh.Request.Headers.Cookie = $"{WebCookieNames.Session}={firstSessionId}";
        var secondIdentity = service.Issue(refresh, "tenant-1", "user-1");

        Assert.NotEqual(firstIdentity.CsrfToken, secondIdentity.CsrfToken);

        var oldRequest = new DefaultHttpContext();
        oldRequest.Request.Headers.Cookie = $"{WebCookieNames.Session}={firstSessionId}";
        Assert.False(service.TryRead(oldRequest, out _));
    }

    [Fact]
    public void CsrfService_AcceptsMatchingHeaderAndCookie()
    {
        var csrf = new CsrfService();
        var identity = new SessionIdentity("tenant-1", "user-1", "csrf-token");
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{WebCookieNames.Csrf}=csrf-token";
        context.Request.Headers[WebCookieNames.CsrfHeader] = "csrf-token";

        Assert.True(csrf.IsValid(context, identity));
    }

    [Fact]
    public void CsrfService_RejectsMissingCookie()
    {
        var csrf = new CsrfService();
        var identity = new SessionIdentity("tenant-1", "user-1", "csrf-token");
        var context = new DefaultHttpContext();
        context.Request.Headers[WebCookieNames.CsrfHeader] = "csrf-token";

        Assert.False(csrf.IsValid(context, identity));
    }

    [Fact]
    public void CsrfService_RejectsMissingHeader()
    {
        var csrf = new CsrfService();
        var identity = new SessionIdentity("tenant-1", "user-1", "csrf-token");
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{WebCookieNames.Csrf}=csrf-token";

        Assert.False(csrf.IsValid(context, identity));
    }

    [Fact]
    public void CsrfService_RejectsMismatchedValue()
    {
        var csrf = new CsrfService();
        var identity = new SessionIdentity("tenant-1", "user-1", "csrf-token");
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{WebCookieNames.Csrf}=csrf-token";
        context.Request.Headers[WebCookieNames.CsrfHeader] = "other-token";

        Assert.False(csrf.IsValid(context, identity));
    }

    [Fact]
    public async Task ApiProblem_UsesFixedErrorEnvelope()
    {
        await AssertErrorAsync(ApiProblem.Unauthenticated(), 401, "unauthenticated", "登录已失效，请重新进入应用");
        await AssertErrorAsync(ApiProblem.CsrfFailed(), 400, "csrf_failed", "请求校验失败，请刷新页面重试");
    }

    private static async Task AssertErrorAsync(IResult result, int status, string code, string message)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();

        await result.ExecuteAsync(context);

        Assert.Equal(status, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var error = document.RootElement.GetProperty("error");
        Assert.Equal(code, error.GetProperty("code").GetString());
        Assert.Equal(message, error.GetProperty("message").GetString());
    }

    private static string HeaderFor(HttpContext context, string name) =>
        context.Response.Headers.SetCookie.Single(value => value is not null && value.StartsWith($"{name}=", StringComparison.OrdinalIgnoreCase))!;

    private static string CookieValue(string setCookieHeader, string name) =>
        setCookieHeader.Split(';')[0][(name.Length + 1)..];

    private static void ContainsIgnoreCase(string value, string expected) =>
        Assert.True(value.Contains(expected, StringComparison.OrdinalIgnoreCase), $"Expected '{value}' to contain '{expected}'.");
}
