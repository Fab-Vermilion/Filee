using System.IO.Compression;

namespace Filee.Web.Streaming;

/// <summary>Builds an in-memory zip of a job's converted outputs for one-click "download all".</summary>
public static class ArchiveBundle
{
    /// <summary>
    /// Zips the given files. Entry names are bare file names, de-duplicated so two outputs with the
    /// same name never collide inside the archive.
    /// </summary>
    public static byte[] Build(IEnumerable<string> paths)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                var name = UniqueName(Path.GetFileName(path), used);
                var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
                using var output = entry.Open();
                using var input = File.OpenRead(path);
                input.CopyTo(output);
            }
        }
        return stream.ToArray();
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
