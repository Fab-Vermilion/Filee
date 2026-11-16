namespace Filee.Web.Security;

/// <summary>Cookie and header names used by the Web session layer.</summary>
public static class WebCookieNames
{
    public const string Session = "filee_session";
    public const string Csrf = "filee_csrf";
    public const string CsrfHeader = "X-CSRF-Token";
}
