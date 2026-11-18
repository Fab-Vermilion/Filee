using System.IO.Compression;
using Filee.Web.Jobs;
using Filee.Web.Observability;
using Filee.Web.Security;

namespace Filee.Web.Tests;

public sealed class S7SecurityTests
{
    [Fact]
    public void ArchiveGuard_RejectsOversizedAndTraversalEntries()
    {
        using var oversized = Zip(("a.bin", 100));
        var sizeError = Assert.Throws<InvalidOperationException>(() =>
            ArchiveGuard.Validate(oversized, new ArchiveLimits(MaxUncompressedBytes: 10)));
        Assert.Contains("uncompressed", sizeError.Message, StringComparison.OrdinalIgnoreCase);

        using var traversal = Zip(("../outside.txt", 10));
        Assert.Throws<InvalidOperationException>(() => ArchiveGuard.Validate(traversal));

        using var valid = Zip(("inside.txt", 10));
        ArchiveGuard.Validate(valid);
    }

    [Fact]
    public void PathGuard_RejectsTraversalAndSymlinkEscape()
    {
        var root = TestPath("s7-path-root");
        Directory.CreateDirectory(root);
        var inside = Path.Combine(root, "inside.txt");
        File.WriteAllText(inside, "ok");

        Assert.Equal(inside, PathGuard.EnsureInside(root, inside));
        Assert.Throws<InvalidOperationException>(() => PathGuard.EnsureInside(root, Path.Combine(root, "..", "outside.txt")));

        var outside = TestPath("s7-outside.txt");
        File.WriteAllText(outside, "secret");
        var link = Path.Combine(root, "escape.txt");
        File.CreateSymbolicLink(link, outside);
        Assert.Throws<InvalidOperationException>(() => PathGuard.EnsureInside(root, link));
    }

    [Fact]
    public void LogRedactor_RemovesPathsAndCredentials()
    {
        var raw = "Authorization: Basic abc123 path=/Volumes/ZhiTai/workbench/filee_wb_app/Filee/a.txt client_secret=xyz token=12345";

        var redacted = LogRedactor.Redact(raw);

        Assert.DoesNotContain("/Volumes/", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("xyz", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("12345", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void WebLimits_UsesFrozenDefaultBudget()
    {
        var limits = WebLimits.Default;

        Assert.Equal(500L * 1024 * 1024, limits.MaxFileBytes);
        Assert.Equal(4, limits.MaxConcurrency);
        Assert.Equal(150, limits.SseThrottleMs);
        Assert.Equal(200, limits.SseBufferSize);
        Assert.Equal(15, limits.SseHeartbeatSeconds);
    }

    [Fact]
    public async Task OpsHealthService_ReturnsSummaryWithoutPaths()
    {
        var root = TestPath("s7-ops");
        var storage = new WebStoragePaths(root);
        storage.Ensure();
        var service = new OpsHealthService(storage, new JobStore(storage.Database), new WebConcurrencyGate(4));

        var snapshot = await service.GetAsync();

        Assert.Equal("ok", snapshot.Status);
        Assert.Equal(4, snapshot.ConcurrencyBudget);
        Assert.False(snapshot.ToString().Contains(root, StringComparison.Ordinal));
    }

    private static string TestPath(string name) =>
        Path.Combine(AppContext.BaseDirectory, $"{name}-{Guid.NewGuid():N}");

    private static MemoryStream Zip(params (string Name, int Size)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, size) in entries)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using var output = entry.Open();
                output.Write(new byte[size]);
            }
        }
        stream.Position = 0;
        return stream;
    }
}
