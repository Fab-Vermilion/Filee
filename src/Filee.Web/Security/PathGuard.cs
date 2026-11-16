namespace Filee.Web.Security;

/// <summary>Ensures a filesystem target stays inside a root and does not traverse a symbolic link.</summary>
public static class PathGuard
{
    public static string EnsureInside(string root, string candidate)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var candidateFull = Path.GetFullPath(candidate);

        if (!candidateFull.Equals(rootFull, comparison)
            && !candidateFull.StartsWith(rootFull + Path.DirectorySeparatorChar, comparison))
            throw new InvalidOperationException("Path escapes the permitted root.");

        var relative = Path.GetRelativePath(rootFull, candidateFull);
        var current = rootFull;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Path traverses a symbolic link.");
        }

        return candidateFull;
    }
}
