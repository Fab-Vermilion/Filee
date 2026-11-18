using System.IO.Compression;
using System.Text;
using Filee.Web.Streaming;

namespace Filee.Web.Tests;

public sealed class ArchiveBundleTests
{
    [Fact]
    public void Build_ZipsAllFilesWithBareNames()
    {
        var dir = Path.Combine(Path.GetTempPath(), "filee-archive-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(dir);
            var a = Path.Combine(dir, "first.pdf");
            var b = Path.Combine(dir, "second.pdf");
            File.WriteAllText(a, "AAA", Encoding.UTF8);
            File.WriteAllText(b, "BBB", Encoding.UTF8);

            var stream = ArchiveBundle.Build([a, b]);

            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            Assert.Equal(2, zip.Entries.Count);
            Assert.Contains("first.pdf", zip.Entries.Select(e => e.FullName));
            Assert.Contains("second.pdf", zip.Entries.Select(e => e.FullName));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void Build_DeduplicatesSameFileName()
    {
        var dir = Path.Combine(Path.GetTempPath(), "filee-archive-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(dir);
            var sub1 = Directory.CreateDirectory(Path.Combine(dir, "a"));
            var sub2 = Directory.CreateDirectory(Path.Combine(dir, "b"));
            var a = Path.Combine(sub1.FullName, "report.pdf");
            var b = Path.Combine(sub2.FullName, "report.pdf");
            File.WriteAllText(a, "1");
            File.WriteAllText(b, "2");

            var stream = ArchiveBundle.Build([a, b]);

            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            var names = zip.Entries.Select(e => e.FullName).ToArray();
            Assert.Equal(2, names.Length);
            Assert.Contains("report.pdf", names);
            Assert.Contains("report_2.pdf", names);
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void Build_SkipsMissingPaths()
    {
        var stream = ArchiveBundle.Build(["/no/such/file.pdf", ""]);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.Empty(zip.Entries);
    }
}
