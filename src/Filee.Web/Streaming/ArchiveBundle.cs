using System.IO.Compression;

namespace Filee.Web.Streaming;

/// <summary>Builds an in-memory zip of a job's converted outputs for one-click "download all".</summary>
public static class ArchiveBundle
{
    // Guard against a batch of large outputs exhausting server memory. Caller disposes the returned stream.
    public const long MaxTotalBytes = 1024L * 1024 * 1024; // 1 GiB

    /// <summary>
    /// Zips the given files. Entry names are bare file names, de-duplicated so two outputs with the
    /// same name never collide inside the archive. Returns a positioned stream (no ToArray copy).
    /// Throws InvalidOperationException if the total output exceeds <see cref="MaxTotalBytes"/>.
    /// </summary>
    public static MemoryStream Build(IEnumerable<string> paths)
    {
        var stream = new MemoryStream();
        try
        {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                long total = 0;
                foreach (var path in paths)
                {
                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                    total += new FileInfo(path).Length;
                    if (total > MaxTotalBytes)
                        throw new InvalidOperationException("Archive total exceeds the size budget.");
                    var name = UniqueName(Path.GetFileName(path), used);
                    var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
                    using var output = entry.Open();
                    using var input = File.OpenRead(path);
                    input.CopyTo(output);
                }
            }
            stream.Position = 0;
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }

    private static string UniqueName(string name, HashSet<string> used)
    {
        if (used.Add(name)) return name;
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (var i = 2; ; i++)
        {
            var candidate = $"{stem}_{i}{ext}";
            if (used.Add(candidate)) return candidate;
        }
    }
}
