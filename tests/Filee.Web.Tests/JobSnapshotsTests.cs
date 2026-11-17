using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Web.Jobs;
using Filee.Web.Security;

namespace Filee.Web.Tests;

public sealed class JobSnapshotsTests
{
    private static WebStoragePaths NewStorage()
    {
        var root = Path.Combine(Path.GetTempPath(), "filee-snap-" + Guid.NewGuid().ToString("N")[..8]);
        var storage = new WebStoragePaths(root);
        storage.Ensure();
        return storage;
    }

    [Fact]
    public void Build_UsesRegistryWhenJobStillInMemory()
    {
        var record = new JobRecord("web-1", "internal-1", "t1", "u1", "pdf", "completed", 1,
            DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(-1));
        var job = new ConversionJob(["a.txt"], new Preset { TargetFormat = "pdf" }, "pdf");
        var registry = new WebJobRegistry();
        registry.Add("web-1", job);
        registry.SetOutputs("web-1", ["/out/web-1/a.pdf"]);
        var identity = new SessionIdentity("t1", "u1", "csrf");
        var storage = NewStorage();
        try
        {
            var snapshot = JobSnapshots.Build(record, registry, storage, new DownloadTokenService(), identity);
            Assert.Equal("completed", snapshot.Status);
            Assert.Equal("f1", Assert.Single(snapshot.Files).Id);
            Assert.NotNull(snapshot.ArchiveUrl);
        }
        finally { try { Directory.Delete(storage.Root, recursive: true); } catch { } }
    }

    [Fact]
    public void Build_AfterRestart_RebuildsDownloadableFilesFromPersistedNames()
    {
        // Simulates a restarted process: registry empty, but output file + persisted name survive.
        var storage = NewStorage();
        try
        {
            var dir = storage.OutputDirectory("web-2");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "方案.pdf"), "pdf bytes");
            var record = new JobRecord("web-2", "internal-2", "t1", "u1", "pdf", "completed", 1,
                DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-2),
                ["方案.pdf", "已清理.pdf"]);
            var identity = new SessionIdentity("t1", "u1", "csrf");

            var snapshot = JobSnapshots.Build(record, new WebJobRegistry(), storage, new DownloadTokenService(), identity);

            Assert.Equal(2, snapshot.Files.Count);
            var alive = snapshot.Files[0];
            Assert.Equal("方案.pdf", alive.Name);
            Assert.Equal("done", alive.Status);
            Assert.NotNull(alive.DownloadUrl);
            Assert.StartsWith("/api/download?token=", alive.DownloadUrl!);
            var gone = snapshot.Files[1];
            Assert.Equal("missing", gone.Status);
            Assert.Null(gone.DownloadUrl);
            Assert.NotNull(snapshot.ArchiveUrl); // at least one downloadable file
        }
        finally { try { Directory.Delete(storage.Root, recursive: true); } catch { } }
    }

    [Fact]
    public void JobStore_RoundTripsOutputFiles()
    {
        var db = Path.Combine(Path.GetTempPath(), "filee-db-" + Guid.NewGuid().ToString("N")[..8] + ".db");
        try
        {
            var store = new JobStore(db);
            var record = new JobRecord("web-3", "internal-3", "t1", "u1", "pdf", "queued", 0,
                DateTimeOffset.UtcNow, null);
            store.Upsert(record);
            store.UpdateOutputFiles("web-3", ["a.pdf", "b.pdf"]);

            var loaded = store.Get("web-3");
            Assert.NotNull(loaded);
            Assert.Equal(new[] { "a.pdf", "b.pdf" }, loaded!.OutputFiles);
        }
        finally { try { File.Delete(db); } catch { } }
    }
}
