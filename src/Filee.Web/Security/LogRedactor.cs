using System.Text.RegularExpressions;

namespace Filee.Web.Security;

/// <summary>Last line of defence for diagnostic text: no paths, headers or secrets leave the process.</summary>
public static partial class LogRedactor
{
    public static string Redact(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        var redacted = AuthorizationPattern().Replace(message, "$1[REDACTED]");
        redacted = SecretPattern().Replace(redacted, "$1[REDACTED]");
        return AbsolutePathPattern().Replace(redacted, "[REDACTED_PATH]");
    }

    [GeneratedRegex(@"(?i)(Authorization\s*:\s*Basic\s+)\S+", RegexOptions.Compiled)]
    private static partial Regex AuthorizationPattern();

    [GeneratedRegex(@"(?i)\b(client_secret|token|password|secret)\s*=\s*[^\s&,""']+", RegexOptions.Compiled)]
    private static partial Regex SecretPattern();

    [GeneratedRegex(@"(/Volumes/|/Users/|/home/|C:\\)[^\s""']*", RegexOptions.Compiled)]
    private static partial Regex AbsolutePathPattern();
}
