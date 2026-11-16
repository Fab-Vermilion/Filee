using System.Text.Json.Serialization;
using Filee.Core.Platform;
using Filee.Engines.Infrastructure;
using Filee.Web.Contracts;
using Filee.Web.Jobs;
using Filee.Web.Security;

var builder = WebApplication.CreateBuilder(args);

var repositoryRoot = WebStoragePaths.FindRepositoryRoot(AppContext.BaseDirectory);
var storage = new WebStoragePaths(Path.Combine(repositoryRoot, ".filee-web"));
storage.Ensure();
WorkFolders.SetRootOverride(storage.Work);
EngineEnvironment.DownloadRoot = Path.Combine(repositoryRoot, "engines");

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

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/session", (HttpContext context, SessionService sessions) =>
    sessions.TryRead(context, out var identity)
        ? Results.Ok(new { tid = identity.Tid, sub = identity.Sub, csrfToken = identity.CsrfToken })
        : ApiProblem.Unauthenticated());

app.Run();

/// <summary>Entry point marker used by future integration tests.</summary>
public partial class Program;
