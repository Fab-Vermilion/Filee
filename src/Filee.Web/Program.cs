using System.Text.Json.Serialization;
using Filee.Web.Contracts;
using Filee.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<CsrfService>();

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/session", (HttpContext context, SessionService sessions) =>
    sessions.TryRead(context, out var identity)
        ? Results.Ok(new { tid = identity.Tid, sub = identity.Sub, csrfToken = identity.CsrfToken })
        : ApiProblem.Unauthenticated());

app.Run();

/// <summary>Entry point marker used by future integration tests.</summary>
public partial class Program;
