using System.Text.Json;

namespace Filee.Web.Configuration;

/// <summary>
/// Single configuration source for the Web app. All values may come from appsettings.local.json
/// (git-ignored, contains secrets), with environment variables taking precedence so containers can
/// override without editing files. Secrets are never written to logs or HTTP responses.
/// </summary>
public sealed record AppConfig(
    AppServerConfig Server,
    AppSsoConfig Sso,
    AppLimitsConfig Limits,
    AppTlsConfig Tls,
    AppSessionConfig Session,
    AppRetentionConfig Retention,
    AppStorageConfig Storage,
    string? OpsToken)
{
    /// <summary>Path of the git-ignored local config, next to the app's content root.</summary>
    public const string LocalFileName = "appsettings.local.json";

    public static AppConfig Load(string contentRoot, string? configPath = null)
    {
        var path = string.IsNullOrWhiteSpace(configPath)
            ? Path.Combine(contentRoot, LocalFileName)
            : configPath!;
        JsonDocument? document = null;
        if (File.Exists(path))
        {
            try { document = JsonDocument.Parse(File.ReadAllText(path)); }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Invalid JSON in {path}: {ex.Message}", ex);
            }
        }
        using (document)
        {
            var root = document?.RootElement;
            return new AppConfig(
                Server: AppServerConfig.From(root, path),
                Sso: AppSsoConfig.From(root),
                Limits: AppLimitsConfig.From(root),
                Tls: AppTlsConfig.From(root),
                Session: AppSessionConfig.From(root),
                Retention: AppRetentionConfig.From(root),
                Storage: AppStorageConfig.From(root),
                OpsToken: Str(root, "OpsToken", "opsToken", "FILEE_WEB_OPS_TOKEN"));
        }
    }

    // ----- helpers shared by the section records -----
    internal static JsonElement? Section(JsonElement? root, string name) =>
        root is { ValueKind: JsonValueKind.Object } r
        && r.TryGetProperty(name, out var s)
        && s.ValueKind == JsonValueKind.Object
            ? s : null;

    internal static string? Str(JsonElement? root, string camel, string pascal, string? env = null)
    {
        var fromEnv = env is null ? null : Environment.GetEnvironmentVariable(env);
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;
        if (root is { ValueKind: JsonValueKind.Object } r)
        {
            if (r.TryGetProperty(camel, out var a) && a.ValueKind == JsonValueKind.String) return a.GetString();
            if (r.TryGetProperty(pascal, out var b) && b.ValueKind == JsonValueKind.String) return b.GetString();
        }
        return null;
    }

    internal static long Long(JsonElement? section, string camel, string pascal, string env, long fallback)
    {
        var fromEnv = env is null ? null : Environment.GetEnvironmentVariable(env);
        if (long.TryParse(fromEnv, out var ev) && ev > 0) return ev;
        if (section is { ValueKind: JsonValueKind.Object } s)
            foreach (var key in new[] { camel, pascal })
                if (s.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Number
                    && el.TryGetInt64(out var jv) && jv > 0) return jv;
        var raw = Str(section, camel, pascal);
        return long.TryParse(raw, out var v) && v > 0 ? v : fallback;
    }

    internal static int Int(JsonElement? section, string camel, string pascal, string env, int fallback)
    {
        var fromEnv = env is null ? null : Environment.GetEnvironmentVariable(env);
        if (int.TryParse(fromEnv, out var ev) && ev > 0) return ev;
        if (section is { ValueKind: JsonValueKind.Object } s)
            foreach (var key in new[] { camel, pascal })
                if (s.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Number
                    && el.TryGetInt32(out var jv) && jv > 0) return jv;
        var raw = Str(section, camel, pascal);
        return int.TryParse(raw, out var v) && v > 0 ? v : fallback;
    }

    internal static string[] List(JsonElement? section, string camel, string pascal, string env)
    {
        var fromEnv = Environment.GetEnvironmentVariable(env);
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (section is { ValueKind: JsonValueKind.Object } s)
        {
            foreach (var key in new[] { camel, pascal })
                if (s.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    return arr.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => x.GetString()!).ToArray();
        }
        return [];
    }
}
