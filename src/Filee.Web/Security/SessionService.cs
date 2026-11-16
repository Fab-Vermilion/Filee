using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;

namespace Filee.Web.Security;

/// <summary>
/// Single-process session store. A session is a 128-bit random opaque id; the server keeps only
/// (tid, sub, csrf token, expiry). The browser never sees tenant/subject fields inside the cookie.
/// </summary>
public sealed class SessionService
{
    private sealed record SessionRecord(string Tid, string Sub, string CsrfToken, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new(StringComparer.Ordinal);
    private readonly TimeSpan _lifetime;

    public SessionService(TimeSpan? lifetime = null) => _lifetime = lifetime ?? TimeSpan.FromHours(8);

    public SessionIdentity Issue(HttpContext context, string tid, string sub)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(tid))
            throw new ArgumentException("Tenant id is required.", nameof(tid));
        if (string.IsNullOrWhiteSpace(sub))
            throw new ArgumentException("Subject is required.", nameof(sub));

        // A refresh is a session rotation: the old id and its CSRF token stop working immediately.
        if (context.Request.Cookies.TryGetValue(WebCookieNames.Session, out var previous))
            _sessions.TryRemove(previous, out _);

        var sessionId = NewToken();
        var csrfToken = NewToken();
        _sessions[sessionId] = new SessionRecord(tid, sub, csrfToken, DateTimeOffset.UtcNow + _lifetime);

        AppendCookie(context, WebCookieNames.Session, sessionId, HttpOnly: true);
        AppendCookie(context, WebCookieNames.Csrf, csrfToken, HttpOnly: false);

        return new SessionIdentity(tid, sub, csrfToken);
    }

    public bool TryRead(HttpContext context, out SessionIdentity identity)
    {
        identity = null!;
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Request.Cookies.TryGetValue(WebCookieNames.Session, out var sessionId)
            || string.IsNullOrEmpty(sessionId)
            || !_sessions.TryGetValue(sessionId, out var record))
            return false;

        if (record.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(sessionId, out _);
            return false;
        }

        identity = new SessionIdentity(record.Tid, record.Sub, record.CsrfToken);
        return true;
    }

    public void Invalidate(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Request.Cookies.TryGetValue(WebCookieNames.Session, out var sessionId))
            _sessions.TryRemove(sessionId, out _);
        context.Response.Cookies.Delete(WebCookieNames.Session, SessionCookieOptions(TimeSpan.Zero, HttpOnly: true));
        context.Response.Cookies.Delete(WebCookieNames.Csrf, SessionCookieOptions(TimeSpan.Zero, HttpOnly: false));
    }

    private void AppendCookie(HttpContext context, string name, string value, bool HttpOnly) =>
        context.Response.Cookies.Append(name, value, SessionCookieOptions(_lifetime, HttpOnly));

    private static CookieOptions SessionCookieOptions(TimeSpan lifetime, bool HttpOnly) => new()
    {
        HttpOnly = HttpOnly,
        Secure = true,
        SameSite = SameSiteMode.None,
        Path = "/",
        IsEssential = true,
        Expires = DateTimeOffset.UtcNow.Add(lifetime),
    };

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
