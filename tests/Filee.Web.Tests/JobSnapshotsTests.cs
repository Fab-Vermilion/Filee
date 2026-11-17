using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Web.Jobs;
using Filee.Web.Security;

namespace Filee.Web.Tests;

public sealed class JobSnapshotsTests
{
    [Fact]
    public void Build_UsesRecordStatusAndMarksArchiveWhenOutputsExist()
    {
        var record = new JobRecord("web-1", "internal-1", "t1", "u1", "pdf", "completed", 1,
            DateTimeOffset.UtcNow.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(-1));
        var job = new ConversionJob(["a.txt"], new Preset { TargetFormat = "pdf" }, "pdf");
        var registry = new WebJobRegistry();
        registry.Add("web-1", job);
        registry.SetOutputs("web-1", ["/out/web-1/a.pdf"]);
        var identity = new SessionIdentity("t1", "u1", "csrf");

        var snapshot = JobSnapshots.Build(record, registry, new DownloadTokenService(), identity);

        Assert.Equal("completed", snapshot.Status);
        Assert.Equal("f1", Assert.Single(snapshot.Files).Id);
        Assert.NotNull(snapshot.ArchiveUrl);
        Assert.StartsWith("/api/download/all?token=", snapshot.ArchiveUrl!);
    }

    [Fact]
    public void Build_WithoutRegistryStillReturnsRecordHistory()
    {
        var record = new JobRecord("web-2", "internal-2", "t1", "u1", "pdf", "completed", 1,
            DateTimeOffset.UtcNow.AddHours(-2), DateTimeOffset.UtcNow.AddHours(-2));
        var registry = new WebJobRegistry(); // process restarted: no in-memory job

        var snapshot = JobSnapshots.Build(record, registry, new DownloadTokenService(),
            new SessionIdentity("t1", "u1", "csrf"));

        Assert.Equal("web-2", snapshot.JobId);
        Assert.Equal("completed", snapshot.Status);
        Assert.Empty(snapshot.Files);
        Assert.Null(snapshot.ArchiveUrl);
    }
}
