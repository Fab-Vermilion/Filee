using System.Text.Json.Serialization;
using Filee.Core.Conversion;
using Filee.Core.Platform;
using Filee.Engines.Infrastructure;
using Filee.Web.Contracts;
using Filee.Web.Conversion;
using Filee.Web.Jobs;
using Filee.Web.Security;
using Filee.Web.Streaming;

var builder = WebApplication.CreateBuilder(args);

var repositoryRoot = WebStoragePaths.FindRepositoryRoot(AppContext.BaseDirectory);
var storage = new WebStoragePaths(Path.Combine(repositoryRoot, ".filee-web"));
storage.Ensure();
WorkFolders.SetRootOverride(storage.Work);
EngineEnvironment.DownloadRoot = Path.Combine(repositoryRoot, "engines");

var engineEnvironment = new EngineEnvironment(storage.Root);
var converters = WebConversionAssembly.CreateConverters(engineEnvironment);
var catalog = new ConverterCatalog(converters);
var queue = new JobQueue(catalog);
var registry = new WebJobRegistry();
var progress = new ProgressHub();
var sseOptions = new SseOptions();
var downloadTokens = new DownloadTokenService();

_ = catalog.CheckInBackgroundAsync();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<CsrfService>();
builder.Services.AddSingleton(storage);
builder.Services.AddSingleton(new JobStore(storage.Database));
builder.Services.AddSingleton(new WebConcurrencyGate(4));
builder.Services.AddSingleton(catalog);
builder.Services.AddSingleton(queue);
builder.Services.AddSingleton(registry);
builder.Services.AddSingleton(progress);
builder.Services.AddSingleton(sseOptions);
builder.Services.AddSingleton(downloadTokens);
builder.Services.AddSingleton<JobCancelService>();

var app = builder.Build();

var jobStore = app.Services.GetRequiredService<JobStore>();
queue.JobUpdated += (_, job) =>
{
    if (!registry.TryGetWebId(job.Id, out var webId))
        return;
    var status = JobStatusMapper.ToApi(job.State);
    progress.Publish(webId, status, job.Progress, force: IsTerminalStatus(status));
    jobStore.UpdateSummary(webId, status, job.Progress, IsTerminalStatus(status) ? DateTimeOffset.UtcNow : null);
};
queue.JobFinished += (_, job) =>
{
    if (!registry.TryGetWebId(job.Id, out var webId))
        return;
    var output = job.Outputs.FirstOrDefault(File.Exists);
    if (output is not null)
        registry.SetOutput(webId, output);
    var status = JobStatusMapper.ToApi(job.State);
    progress.Publish(webId, status, job.Progress, force: true);
    jobStore.UpdateSummary(webId, status, job.Progress, DateTimeOffset.UtcNow);
};

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/session", (HttpContext context, SessionService sessions) =>
    sessions.TryRead(context, out var identity)
        ? Results.Ok(new { tid = identity.Tid, sub = identity.Sub, csrfToken = identity.CsrfToken })
        : ApiProblem.Unauthenticated());

app.MapGet("/api/jobs/{id}", (string id, HttpContext context, SessionService sessions, JobStore store,
    WebJobRegistry jobs, DownloadTokenService tokens) =>
{
    if (!sessions.TryRead(context, out var identity))
        return ApiProblem.Unauthenticated();
    var record = store.Get(id);
    if (record is null || record.Tid != identity.Tid || record.Sub != identity.Sub)
        return ApiProblem.NotFound();

    var job = jobs.Get(id);
    var files = job is null
        ? []
        : job.Files.Select((file, index) => new JobFileSnapshot(
            $"f{index + 1}",
            Path.GetFileName(file.SourcePath),
            file.Transfer?.Bytes ?? 0,
            ToFileStatus(file.State),
            file.Progress,
            file.ErrorKey is null ? null : ApiErrorEnvelope.Create("conversion_failed", "转换失败，请重试").Error)).ToArray();

    if (job is not null && jobs.TryGetOutput(id, out _))
    {
        var token = tokens.Issue(identity.Tid, identity.Sub, id);
        files = files.Select(file => file with { DownloadUrl = $"/api/download?token={token}" }).ToArray();
    }

    return Results.Ok(JobSnapshot.From(record) with { Files = files });
});

app.MapGet("/api/jobs/{id}/events", async Task<IResult> (string id, HttpContext context, SessionService sessions,
    JobStore store, ProgressHub hub, SseOptions options) =>
{
    if (!sessions.TryRead(context, out var identity))
        return ApiProblem.Unauthenticated();
    var record = store.Get(id);
    if (record is null || record.Tid != identity.Tid || record.Sub != identity.Sub)
        return ApiProblem.NotFound();

    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    var lastId = long.TryParse(context.Request.Headers["Last-Event-ID"], out var parsed) ? parsed : (long?)null;

    while (!context.RequestAborted.IsCancellationRequested)
    {
        foreach (var sseEvent in hub.GetSince(id, lastId))
        {
            await WriteSseAsync(context.Response, sseEvent);
            lastId = sseEvent.Id;
        }
        await Task.Delay(TimeSpan.FromSeconds(options.HeartbeatSeconds), context.RequestAborted);
        await context.Response.WriteAsync("event: heartbeat\ndata: {}\n\n");
        await context.Response.Body.FlushAsync();
    }
    return Results.Empty;
});

app.MapPost("/api/jobs/{id}/cancel", (string id, HttpContext context, SessionService sessions, CsrfService csrf,
    JobStore store, JobCancelService canceller) =>
{
    if (!sessions.TryRead(context, out var identity))
        return ApiProblem.Unauthenticated();
    if (!csrf.IsValid(context, identity))
        return ApiProblem.CsrfFailed();
    var record = store.Get(id);
    if (record is null || record.Tid != identity.Tid || record.Sub != identity.Sub)
        return ApiProblem.NotFound();
    if (IsTerminalStatus(record.Status))
        return ApiProblem.AlreadyFinished();

    return Results.Json(canceller.Cancel(id), statusCode: StatusCodes.Status202Accepted);
});

app.MapGet("/api/download", (string? token, HttpContext context, SessionService sessions,
    DownloadTokenService tokens, WebJobRegistry jobs) =>
{
    if (!sessions.TryRead(context, out var identity))
        return ApiProblem.Unauthenticated();
    if (string.IsNullOrEmpty(token))
        return ApiProblem.InvalidToken();
    if (!tokens.TryResolve(token, identity.Tid, identity.Sub, out var jobId))
        return ApiProblem.Forbidden();
    if (!jobs.TryGetOutput(jobId, out var path) || !File.Exists(path))
        return ApiProblem.Expired();

    return Results.File(path, "application/octet-stream", Path.GetFileName(path));
});

app.Run();

static bool IsTerminalStatus(string status) =>
    status is "completed" or "completed_with_errors" or "failed" or "cancelled" or "interrupted";

static string ToFileStatus(FileState state) => state switch
{
    FileState.Pending => "pending",
    FileState.Running => "running",
    FileState.Done => "done",
    FileState.Skipped => "skipped",
    FileState.Failed => "failed",
    FileState.Cancelled => "cancelled",
    _ => throw new ArgumentOutOfRangeException(nameof(state)),
};

static async Task WriteSseAsync(HttpResponse response, SseEvent sseEvent)
{
    await response.WriteAsync($"id: {sseEvent.Id}\nevent: {sseEvent.Event}\ndata: {sseEvent.Data}\n\n");
    await response.Body.FlushAsync();
}

/// <summary>Entry point marker used by future integration tests.</summary>
public partial class Program;
