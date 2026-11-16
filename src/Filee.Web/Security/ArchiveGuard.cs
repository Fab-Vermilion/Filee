using System.IO.Compression;

namespace Filee.Web.Security;

public sealed record ArchiveLimits(
    long MaxUncompressedBytes = 500L * 1024 * 1024,
    int MaxEntries = 10000,
    double MaxCompressionRatio = 200)
{
    public static ArchiveLimits Default { get; } = new();
}

/// <summary>Metadata-only archive budget check: no entry is expanded during validation.</summary>
public static class ArchiveGuard
{
    public static void Validate(Stream archiveStream, ArchiveLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(archiveStream);
        limits ??= ArchiveLimits.Default;

        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
        long uncompressed = 0;
        long compressed = 0;

        for (var index = 0; index < archive.Entries.Count; index++)
        {
            var entry = archive.Entries[index];
            ValidateEntryName(entry.FullName);
            uncompressed += entry.Length;
            compressed += entry.CompressedLength;
            if (uncompressed > limits.MaxUncompressedBytes)
                throw new InvalidOperationException("Archive exceeds uncompressed byte budget.");
            if (index + 1 > limits.MaxEntries)
                throw new InvalidOperationException("Archive exceeds entry count budget.");
        }

        if (compressed > 0 && (double)uncompressed / compressed > limits.MaxCompressionRatio)
            throw new InvalidOperationException("Archive compression ratio is too high.");
    }

    private static void ValidateEntryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || Path.IsPathRooted(name)
            || name.StartsWith('\\')
            || name.Contains(':', StringComparison.Ordinal)
            || name.Split(['/', '\\'], StringSplitOptions.None).Contains(".."))
            throw new InvalidOperationException("Archive contains an unsafe entry path.");
    }
}
