using System.Text.Json;
using Filee.Core.Conversion;
using Filee.Web.Conversion;
using Filee.Web.Jobs;
using Filee.Web.Streaming;

namespace Filee.Web.Tests;

public sealed class S5ProgressDownloadTests
{
    [Fact]
    public void ProgressHub_UsesMonotonicIdsAndReplaysFromLastEventId()
    {
        var hub = new ProgressHub(new SseOptions(BufferSize: 10, ThrottleMs: 0));

        var first = hub.Publish("job-1", "running", 0.1, force: true)!;
        var second = hub.Publish("job-1", "running", 0.6, force: true)!;

        Assert.True(second.Id > first.Id);
        Assert.Equal("progress", first.Event);
        Assert.Equal("progress", second.Event);
        Assert.Equal("running", DataStatus(first));
        Assert.Single(hub.GetSince("job-1", first.Id));
        Assert.Equal(second.Id, hub.GetSince("job-1", first.Id)[0].Id);
        Assert.Empty(hub.GetSince("job-1", second.Id));
    }

    [Fact]
    public void ProgressHub_FallsBackToBufferedWindowWhenLastEventIdIsTooOld()
    {
        var hub = new ProgressHub(new SseOptions(BufferSize: 2, ThrottleMs: 0));

        var first = hub.Publish("job-1", "queued", 0, force: true)!;
        hub.Publish("job-1", "running", 0.5, force: true);
        var third = hub.Publish("job-1", "completed", 1, force: true)!;

        Assert.Equal(2, hub.GetSince("job-1", first.Id).Count);
        Assert.Equal(third.Id, hub.GetSince("job-1", first.Id)[1].Id);
    }

    [Fact]
    public void ProgressHub_ProgressOneDoesNotClaimSuccess()
    {
        var hub = new ProgressHub(new SseOptions(ThrottleMs: 0));

        var completedProcessing = hub.Publish("job-1", "running", 1, force: true)!;
        var failed = hub.Publish("job-1", "failed", 1, force: true)!;

        Assert.Equal("running", DataStatus(completedProcessing));
        Assert.Equal("failed", DataStatus(failed));
        Assert.Equal("done", failed.Event);
    }

    [Fact]
    public void JobCancelService_RequestsCancellationAndReturnsStatusAtCallTime()
    {
        var root = Path.Combine(AppContext.BaseDirectory, $"s5-cancel-{Guid.NewGuid():N}");
        var store = new JobStore(Path.Combine(root, "jobs.db"));
        var registry = new WebJobRegistry();
        var webId = WebJobId.New();
        var job = new ConversionJob(["photo.png"], PresetFactory.Create("png", Path.Combine(root, "out")), "png");
        registry.Add(webId, job);
        store.Upsert(new JobRecord(webId, job.Id, "tenant-1", "user-1", "png", "running", 0.2, DateTimeOffset.UtcNow, null));

        var result = new JobCancelService(registry, store).Cancel(webId);

        Assert.Equal(webId, result.JobId);
        Assert.Equal("running", result.Status);
        Assert.True(result.CancellationRequested);
        Assert.True(job.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public void DownloadTokenService_BindsTokenToTenantAndSubject()
    {
        var tokens = new DownloadTokenService();
        var token = tokens.Issue("tenant-1", "user-1", "job-1");

        Assert.Equal(32, token.Length);
        Assert.True(tokens.TryResolve(token, "tenant-1", "user-1", out var jobId));
        Assert.Equal("job-1", jobId);
        Assert.False(tokens.TryResolve(token, "tenant-1", "user-2", out _));
        Assert.False(tokens.TryResolve(token, "tenant-2", "user-1", out _));
    }

    private static string DataStatus(SseEvent sseEvent)
    {
        using var document = JsonDocument.Parse(sseEvent.Data);
        return document.RootElement.GetProperty("status").GetString()!;
    }
}
