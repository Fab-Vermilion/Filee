namespace Filee.Web.Security;

/// <summary>
/// The only identity fields the Web application stores and returns. No profile data is fetched or exposed.
/// </summary>
public sealed record SessionIdentity(string Tid, string Sub, string CsrfToken);
