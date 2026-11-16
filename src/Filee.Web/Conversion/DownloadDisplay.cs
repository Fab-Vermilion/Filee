namespace Filee.Web.Conversion;

/// <summary>
/// Produces the download label shown to the user. The real file served by /api/download is the job's
/// output (already in the target format), so the label must never advertise the source format.
/// </summary>
public static class DownloadDisplay
{
    public static string Name(string? sourceName, string? target, string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            var outputName = Path.GetFileName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputName))
                return outputName;
        }

        var source = string.IsNullOrWhiteSpace(sourceName) ? "converted" : Path.GetFileName(sourceName);
        if (string.IsNullOrWhiteSpace(target))
            return source;

        var extension = target.TrimStart('.');
        return Path.ChangeExtension(source, string.IsNullOrWhiteSpace(extension) ? source : "." + extension);
    }
}
