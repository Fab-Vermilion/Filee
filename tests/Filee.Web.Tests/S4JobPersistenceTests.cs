using System.Text;
using System.Text.Json;
using Filee.Core.Platform;
using Filee.Web.Jobs;

namespace Filee.Web.Tests;

public sealed class S4JobPersistenceTests
{
    [Fact]
    public void WebJobId_Is128BitLowerHex()
    {
        var id = WebJobId.New();

        Assert.Equal(32, id.Length);
        Assert.Matches("^[0-9a-f]{32}$", id);
        Assert.NotEqual(id, WebJobId.New());
    }

    [Fact]
    public void WebStoragePaths_KeepsUploadWorkAndOutputSeparate()
    {
        var root = TestPath("s4-storage");
        var paths = new WebStoragePaths(root);

        paths.Ensure();

        Assert.True(Directory.Exists(paths.Uploads));
        Assert.True(Directory.Exists(paths.Work));
        Assert.True(Directory.Exists(paths.Output));
        Assert.Equal(Path.Combine(root, "uploads", "job-1"), paths.UploadDirectory("job-1"));
        Assert.Equal(Path.Combine(root, "output", "job-1"), paths.OutputDirectory("job-1"));
        Assert.Equal(Path.Combine(root, "jobs.db"), paths.Database);
        Assert.Equal("work", Path.GetFileName(paths.Work));
    }

    [Fact]
    public void UploadGuard_AcceptsKnownMagicAndRejectsMismatch()
    {
        using var png = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]);
        UploadGuard.Validate("photo.png", png);

        using var pdfBytes = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7"));
        var exception = Assert.Throws<UploadException>(() => UploadGuard.Validate("photo.png", pdfBytes));
        Assert.Equal("bad_magic", exception.Code);
    }

    [Fact]
    public void UploadGuard_RejectsOversize()
    {
        using var png = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var exception = Assert.Throws<UploadException>(() =>
            UploadGuard.Validate("photo.png", png, new UploadLimits(MaxBytes: 4)));

        Assert.Equal("file_too_large", exception.Code);
    }

    [Fact]
    public void JobStore_PersistsTerminalStateAndFiltersByOwner()
    {
        var store = new JobStore(Path.Combine(TestPath("s4-db"), "jobs.db"));
        var created = DateTimeOffset.UtcNow;
        var record = new JobRecord(
            WebJobId.New(), "internal01", "tenant-1", "user-1", "png",
            "completed", 1.0, created, created.AddSeconds(2));

        store.Upsert(record);

        var loaded = store.Get(record.WebId);
        Assert.NotNull(loaded);
        Assert.Equal("completed", loaded.Status);
        Assert.Equal(1.0, loaded.Progress);
        Assert.Equal("tenant-1", loaded.Tid);
        Assert.Equal("user-1", loaded.Sub);
        Assert.Empty(store.ForOwner("tenant-1", "user-2"));

        store.Upsert(record with { Status = "failed", Progress = 0.5 });
        Assert.Equal("failed", store.Get(record.WebId)!.Status);
    }

    [Fact]
    public void JobSnapshot_DoesNotExposeInternalJobId()
    {
        var created = DateTimeOffset.UtcNow;
        var record = new JobRecord(
            WebJobId.New(), "internal01", "tenant-1", "user-1", "png",
            "completed", 1.0, created, created.AddSeconds(2));

        var snapshot = JobSnapshot.From(record);
        var json = JsonSerializer.Serialize(snapshot);

        Assert.Contains(record.WebId, json);
        Assert.DoesNotContain(record.InternalId, json);
        Assert.Equal("completed", snapshot.Status);
    }

    [Fact]
    public async Task WebConcurrencyGate_LimitsToConfiguredBudget()
    {
        var gate = new WebConcurrencyGate(2);
        var active = 0;
        var peak = 0;

        var workers = Enumerable.Range(0, 5).Select(async _ =>
        {
            using var lease = await gate.AcquireAsync();
            var current = Interlocked.Increment(ref active);
            InterlockedMax(ref peak, current);
            await Task.Delay(25);
            Interlocked.Decrement(ref active);
        });

        await Task.WhenAll(workers);

        Assert.Equal(2, gate.MaxConcurrency);
        Assert.Equal(2, peak);
    }

    [Fact]
    public void WorkFolders_OverrideKeepsWorkInsideRepository()
    {
        var root = TestPath("s4-work-root");

        WorkFolders.SetRootOverride(root);

        Assert.Equal(root, WorkFolders.Root);
        Assert.True(Directory.Exists(root));
    }

    private static string TestPath(string name) =>
        Path.Combine(AppContext.BaseDirectory, $"{name}-{Guid.NewGuid():N}");

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)))
        {
            if (Interlocked.CompareExchange(ref target, value, current) == current)
                return;
        }
    }
}
