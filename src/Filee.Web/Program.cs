using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using Filee.Core.Conversion;
using Filee.Core.Formats;
using Filee.Core.Platform;
using Filee.Engines.Infrastructure;
using Filee.Web.Configuration;
using Filee.Web.Contracts;
using Filee.Web.Conversion;
using Filee.Web.Jobs;
using Filee.Web.Observability;
using Filee.Web.Security;
using Filee.Web.Sso;
using Filee.Web.Streaming;

// Single config source: appsettings.local.json (git-ignored) with env vars as overrides.
// Load before everything so bootstrap and CreateBuilder both see ASPNETCORE_URLS/TLS/storage.
var config = AppConfig.Load(Directory.GetCurrentDirectory(), Environment.GetEnvironmentVariable("FILEE_CONFIG"));

// Explicit empty-database bootstrap for deployment: `dotnet run -- --init-db` creates the schema
// via JobStore (the single source of truth) and exits, without starting the web host.
if (args.Contains("--init-db", StringComparer.OrdinalIgnoreCase))
{
    var bootstrapRoot = WebStoragePaths.FindRepositoryRoot(AppContext.BaseDirectory);
    var bootstrapStorage = new WebStoragePaths(Path.Combine(bootstrapRoot, config.Storage.Root));
    _ = new JobStore(bootstrapStorage.Database); // ctor runs CREATE TABLE IF NOT EXISTS
    Console.WriteLine($"数据库已就绪：{bootstrapStorage.Database}");
    return;
}

Environment.SetEnvironmentVariable("ASPNETCORE_URLS", config.Server.Urls);
// Engines still read FILEE_LO_WORKERS from the environment; seed it from config when present.
Environment.SetEnvironmentVariable("FILEE_LO_WORKERS", config.Limits.LoWorkers.ToString());
if (!string.IsNullOrWhiteSpace(config.Tls.PfxPath))
    Environment.SetEnvironmentVariable("FILEE_WEB_TLS_PFX", config.Tls.PfxPath);
if (!string.IsNullOrWhiteSpace(config.Tls.Password))
    Environment.SetEnvironmentVariable("FILEE_WEB_TLS_PASSWORD", config.Tls.Password);

var builder = WebApplication.CreateBuilder(args);
// Resolve a relative certificate path against the content root so it works from any cwd.
var pfxPath = string.IsNullOrWhiteSpace(config.Tls.PfxPath)
    ? null
    : Path.IsPathRooted(config.Tls.PfxPath)
        ? config.Tls.PfxPath
        : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), config.Tls.PfxPath!));
if (!string.IsNullOrWhiteSpace(pfxPath) && File.Exists(pfxPath))
{
    Environment.SetEnvironmentVariable("FILEE_WEB_TLS_PFX", pfxPath);
    builder.WebHost.ConfigureKestrel(options =>
        options.ConfigureHttpsDefaults(https => https.ServerCertificate =
            X509CertificateLoader.LoadPkcs12FromFile(pfxPath!, config.Tls.Password)));
}

var repositoryRoot = WebStoragePaths.FindRepositoryRoot(AppContext.BaseDirectory);
var storage = new WebStoragePaths(Path.Combine(repositoryRoot, config.Storage.Root));
storage.Ensure();
WorkFolders.SetRootOverride(storage.Work);
EngineEnvironment.DownloadRoot = Path.Combine(repositoryRoot, "engines");

var engineEnvironment = new EngineEnvironment(storage.Root);
var converters = WebConversionAssembly.CreateConverters(engineEnvironment);
var catalog = new ConverterCatalog(converters);
var queue = new JobQueue(catalog);
var registry = new WebJobRegistry();
var limits = WebLimits.FromConfig(config);
var progress = new ProgressHub();
var sseOptions = new SseOptions(limits.SseBufferSize, limits.SseThrottleMs, limits.SseHeartbeatSeconds);
var downloadTokens = new DownloadTokenService(TimeSpan.FromHours(config.Retention.DownloadTokenHours));
var jobStore = new JobStore(storage.Database);
var concurrencyGate = new WebConcurrencyGate(limits.MaxConcurrency);
var ssoOptions = SsoOptions.FromConfig(config);
var ssoHttp = new HttpClient();
var jwks = new JwksProvider(ssoHttp, ssoOptions.Issuer, config.Sso.WellKnownPath);
var tokenValidator = new OidcTokenValidator(jwks, ssoOptions.Issuer, ssoOptions.Audience,
    TimeSpan.FromMinutes(config.Sso.ClockSkewMinutes));
var tokenClient = new SsoTokenClient(ssoHttp, ssoOptions);
var targetPlanner = new RoutePlannerAdapter(catalog);
var formatCatalog = new FormatTargetCatalog(FormatRegistry.Known, targetPlanner);

_ = catalog.CheckInBackgroundAsync();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton(config);
builder.Services.AddSingleton(storage);
builder.Services.AddHostedService(sp => new JobDirectoryCleaner(
    storage, jobStore,
    TimeSpan.FromHours(config.Retention.FileAndRecordHours),
    TimeSpan.FromHours(config.Retention.CleanupIntervalHours),
    sp.GetRequiredService<ILogger<JobDirectoryCleaner>>()));
builder.Services.AddSingleton(new SessionService(TimeSpan.FromHours(config.Session.LifetimeHours)));
builder.Services.AddSingleton<CsrfService>();
builder.Services.AddSingleton(jobStore);
builder.Services.AddSingleton(concurrencyGate);
builder.Services.AddSingleton(catalog);
builder.Services.AddSingleton(queue);
builder.Services.AddSingleton(registry);
builder.Services.AddSingleton(progress);
builder.Services.AddSingleton(sseOptions);
builder.Services.AddSingleton(downloadTokens);
builder.Services.AddSingleton<JobCancelService>();
builder.Services.AddSingleton(new JobSubmissionService(
    storage, jobStore, registry, progress, concurrencyGate, job => queue.RunAsync(job), new UploadLimits(limits.MaxFileBytes)));
builder.Services.AddSingleton(new OpsHealthService(storage, jobStore, concurrencyGate));
builder.Services.AddSingleton(targetPlanner);
builder.Services.AddSingleton(formatCatalog);
builder.Services.AddSingleton(ssoOptions);
builder.Services.AddSingleton(ssoHttp);
builder.Services.AddSingleton(jwks);
builder.Services.AddSingleton(tokenValidator);
builder.Services.AddSingleton(tokenClient);

var app = builder.Build();

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
    // Index-aligned with job.Files: each source file's own first existing output.
    var outputs = job.Files
        .Select(f => f.Outputs.FirstOrDefault(File.Exists) ?? "")
        .ToArray();
    registry.SetOutputs(webId, outputs);
    // Persist names so a later process (restart / page reload) can still offer history downloads.
    jobStore.UpdateOutputFiles(webId, outputs.Where(p => !string.IsNullOrEmpty(p)).Select(p => Path.GetFileName(p)!).ToArray());
    // Safety net: if the engine still reports Running although every file is done, persist a terminal
    // status so the UI never sticks on "进行中" with downloadable outputs already present.
    var status = JobFinalStatus.Resolve(job);
    if (status == "running" && outputs.Any(p => !string.IsNullOrEmpty(p)))
        status = "completed";
    progress.Publish(webId, status, job.Progress, force: true);
    jobStore.UpdateSummary(webId, status, job.Progress, IsTerminalStatus(status) ? DateTimeOffset.UtcNow : null);
};

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/ops/health", async Task<IResult> (HttpContext context, OpsHealthService ops, AppConfig appConfig) =>
{
    var expected = appConfig.OpsToken;
    var provided = context.Request.Headers["X-Ops-Token"].ToString();
    if (string.IsNullOrEmpty(expected)
        || !CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected)))
        return ApiProblem.Unauthenticated();
    return Results.Ok(await ops.GetAsync());
});

app.MapGet("/sso", async Task<IResult> (string? code, string? iss, string? target_link_uri, HttpContext context,
    SsoOptions options, SsoTokenClient tokens, OidcTokenValidator validator, SessionService sessions) =>
{
    if (string.IsNullOrWhiteSpace(code) || !SsoLandingGuard.IsTrustedIssuer(options, iss))
        return BadLanding();
    var target = string.IsNullOrWhiteSpace(target_link_uri) ? "/" : target_link_uri;
    var requestOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
    if (!SsoLandingGuard.IsAllowedTarget(options, target, requestOrigin))
        return BadLanding();

    try
    {
        var idToken = await tokens.ExchangeAsync(code);
        var identity = await validator.ValidateAsync(idToken);
        sessions.Issue(context, identity.Tid, identity.Sub);
        return Results.Redirect(target);
    }
    catch
    {
        return BadLanding();
    }
});

app.MapGet("/api/session", (HttpContext context, SessionService sessions) =>
    sessions.TryRead(context, out var identity)
        ? Results.Ok(new { tid = identity.Tid, sub = identity.Sub, csrfToken = identity.CsrfToken })
        : ApiProblem.Unauthenticated());

app.MapGet("/api/formats", (HttpContext context, SessionService sessions, FormatTargetCatalog formats) =>
    sessions.TryRead(context, out _)
        ? Results.Ok(formats.BuildCollection())
        : ApiProblem.Unauthenticated());

app.MapPost("/api/jobs", async Task<IResult> (HttpContext context, SessionService sessions, CsrfService csrf,
    JobSubmissionService submissions) =>
{
    if (!sessions.TryRead(context, out var identity))
        return ApiProblem.Unauthenticated();
    if (!csrf.IsValid(context, identity))
        return ApiProblem.CsrfFailed();
    if (!context.Request.HasFormContentType)
        return ApiProblem.InvalidTarget();

    var form = await context.Request.ReadFormAsync();
    try
    {
        var record = await submissions.SubmitAsync(
            identity,
            form["target"].ToString(),
            form.Files,
            form.ContainsKey("options"));
        return Results.Created($"/api/jobs/{record.WebId}", new { jobId = record.WebId });
    }
    catch (UploadException error)
    {
        return error.Code switch
        {
            "unsupported_option" => ApiProblem.UnsupportedOption(),
            "file_too_large" => ApiProblem.FileTooLarge(),
            "invalid_target" => ApiProblem.InvalidTarget(),
            _ => ApiProblem.BadMagic(),
        };
    }
});

app.MapGet("/api/jobs/{id}", (string id, HttpContext context, SessionService sessions, JobStore store,
    WebJobRegistry jobs, DownloadTokenService tokens) =>
{
    if (!sessions.TryRead(context, out var identity))
        return ApiProblem.Unauthenticated();
    var record = store.Get(id);
    if (record is null || record.Tid != identity.Tid || record.Sub != identity.Sub)
        return ApiProblem.NotFound();

    return Results.Ok(JobSnapshots.Build(record, jobs, storage, tokens, identity));
});

app.MapGet("/api/jobs", (HttpContext context, SessionService sessions, JobStore store,
    WebJobRegistry jobs, DownloadTokenService tokens) =>
{
    if (!sessions.TryRead(context, out var identity))
        return ApiProblem.Unauthenticated();
    // TTL-bounded durable summaries. Download links are available only while the job is still in the
    // in-memory registry (i.e. this process has seen it); expired/old records remain visible as history.
    var snapshots = store.ForOwner(identity.Tid, identity.Sub)
        .Select(record => JobSnapshots.Build(record, jobs, storage, tokens, identity))
        .ToArray();
    return Results.Ok(new { jobs = snapshots });
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
    var lastId = long.TryParse(context.Request.Headers["Last-Event-ID"].ToString(), out var parsed) ? parsed : (long?)null;

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
    if (!tokens.TryResolve(token, identity.Tid, identity.Sub, out _))
        return ApiProblem.Forbidden();
    // Each token pins the exact output it was issued for, so a batch's N links resolve to N files.
    if (!tokens.TryResolvePath(token, identity.Tid, identity.Sub, out var path) || !File.Exists(path))
        return ApiProblem.Expired();

    return Results.File(path, "application/octet-stream", Path.GetFileName(path));
});

app.MapGet("/api/download/all", (string? token, HttpContext context, SessionService sessions,
    DownloadTokenService tokens, WebJobRegistry jobs, WebStoragePaths storage) =>
{
    if (!sessions.TryRead(context, out var identity))
        return ApiProblem.Unauthenticated();
    if (string.IsNullOrEmpty(token))
        return ApiProblem.InvalidToken();
    // Job-level token (issued without a pinned path) authorises bundling this job's outputs.
    if (!tokens.TryResolve(token, identity.Tid, identity.Sub, out var jobId))
        return ApiProblem.Forbidden();
    var paths = jobs.GetOutputs(jobId).Where(File.Exists).ToArray();
    if (paths.Length == 0)
    {
        // History after a restart: registry is empty, but the output directory is durable.
        var dir = storage.OutputDirectory(jobId);
        paths = Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir).Where(p => !Path.GetFileName(p).StartsWith("._")).ToArray()
            : [];
    }
    if (paths.Length == 0)
        return ApiProblem.Expired();

    try
    {
        var archive = ArchiveBundle.Build(paths);
        return Results.File(archive, "application/zip", $"filee-{jobId[..Math.Min(8, jobId.Length)]}.zip");
    }
    catch (InvalidOperationException)
    {
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

static IResult BadLanding() => Results.Json(
    ApiErrorEnvelope.Create("invalid_token", "免登参数无效或已过期"),
    statusCode: StatusCodes.Status400BadRequest);

static bool IsTerminalStatus(string status) =>
    status is "completed" or "completed_with_errors" or "failed" or "cancelled" or "interrupted";

static async Task WriteSseAsync(HttpResponse response, SseEvent sseEvent)
{
    await response.WriteAsync($"id: {sseEvent.Id}\nevent: {sseEvent.Event}\ndata: {sseEvent.Data}\n\n");
    await response.Body.FlushAsync();
}

/// <summary>Entry point marker used by future integration tests.</summary>
public partial class Program;
