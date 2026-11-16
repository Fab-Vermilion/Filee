using System.Text;
using Filee.Core.Formats;

namespace Filee.Web.Jobs;

public sealed record UploadLimits(long MaxBytes = 500L * 1024 * 1024);

public sealed class UploadException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

/// <summary>
/// First upload gate: a known extension and, where a stable signature exists, a matching magic number.
/// </summary>
public static class UploadGuard
{
    private static readonly Dictionary<string, byte[]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["png"] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
        ["jpg"] = [0xFF, 0xD8, 0xFF],
        ["jpeg"] = [0xFF, 0xD8, 0xFF],
        ["gif"] = Encoding.ASCII.GetBytes("GIF8"),
        ["pdf"] = Encoding.ASCII.GetBytes("%PDF"),
        ["zip"] = Encoding.ASCII.GetBytes("PK\x03\x04"),
        ["7z"] = [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C],
    };

    public static void Validate(string fileName, Stream stream, UploadLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        limits ??= new UploadLimits();

        if (stream.CanSeek && stream.Length > limits.MaxBytes)
            throw new UploadException("file_too_large");

        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (extension.Length == 0)
            throw new UploadException("bad_magic");

        var position = stream.CanSeek ? stream.Position : 0;
        var header = new byte[8];
        var read = stream.Read(header, 0, header.Length);
        if (stream.CanSeek)
            stream.Position = position;

        if (Signatures.TryGetValue(extension, out var signature))
        {
            if (read < signature.Length || !header.AsSpan(0, signature.Length).SequenceEqual(signature))
                throw new UploadException("bad_magic");
            return;
        }

        if (FormatRegistry.FindByExtension(extension) is null)
            throw new UploadException("bad_magic");
    }
}
