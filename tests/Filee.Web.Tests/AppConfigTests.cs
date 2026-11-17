using System.Text.Json;
using Filee.Web.Configuration;

namespace Filee.Web.Tests;

public sealed class AppConfigTests
{
    private static string WriteConfig(string json)
    {
        var dir = Path.Combine(Path.GetTempPath(), "filee-cfg-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, AppConfig.LocalFileName);
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Load_ReadsAllSectionsFromJson()
    {
        var path = WriteConfig("""
        {
          "Server": { "urls": "https://localhost:9000", "redirectUri": "https://localhost:9000/sso" },
          "Sso": { "issuer": "https://issuer.test", "audience": "aud", "clientId": "cid", "clientSecret": "sec",
                   "allowedRedirectOrigins": ["https://a.test"], "wellKnownPath": "/.well-known/wk" },
          "Limits": { "maxFileBytes": 1234, "maxConcurrency": 7, "jobTimeoutMinutes": 3,
                      "sseThrottleMs": 100, "sseBufferSize": 50, "sseHeartbeatSeconds": 9, "loWorkers": 2 },
          "Tls": { "pfxPath": "cert.pfx", "password": "pw" },
          "Session": { "lifetimeHours": 12 },
          "Retention": { "fileAndRecordHours": 36, "downloadTokenHours": 30, "cleanupIntervalHours": 2 },
          "Storage": { "root": "custom-state" },
          "opsToken": "ops-secret"
        }
        """);
        try
        {
            var c = AppConfig.Load(Path.GetDirectoryName(path)!);
            Assert.Equal("https://localhost:9000", c.Server.Urls);
            Assert.Equal("https://localhost:9000/sso", c.Server.RedirectUri);
            Assert.Equal("https://issuer.test", c.Sso.Issuer);
            Assert.Equal("sec", c.Sso.ClientSecret);
            Assert.Equal(["https://a.test"], c.Sso.AllowedRedirectOrigins);
            Assert.Equal(1234, c.Limits.MaxFileBytes);
            Assert.Equal(7, c.Limits.MaxConcurrency);
            Assert.Equal(2, c.Limits.LoWorkers);
            Assert.Equal("pw", c.Tls.Password);
            Assert.Equal(12, c.Session.LifetimeHours);
            Assert.Equal(36, c.Retention.FileAndRecordHours);
            Assert.Equal(30, c.Retention.DownloadTokenHours);
            Assert.Equal(2, c.Retention.CleanupIntervalHours);
            Assert.Equal("custom-state", c.Storage.Root);
            Assert.Equal(5, c.Sso.ClockSkewMinutes); // default
            Assert.Equal("ops-secret", c.OpsToken);
        }
        finally { try { Directory.Delete(Path.GetDirectoryName(path)!, recursive: true); } catch { } }
    }

    [Fact]
    public void Load_UsesDefaultsWhenFileMissing()
    {
        var c = AppConfig.Load(Path.Combine(Path.GetTempPath(), "filee-no-such-" + Guid.NewGuid().ToString("N")[..8]));
        Assert.Equal(AppServerConfig.DefaultUrls, c.Server.Urls);
        Assert.Equal(AppServerConfig.DefaultRedirectUri, c.Server.RedirectUri);
        Assert.Equal(500L * 1024 * 1024, c.Limits.MaxFileBytes);
        Assert.Equal(4, c.Limits.MaxConcurrency);
        Assert.Empty(c.Sso.ClientId);
        Assert.Null(c.OpsToken);
    }

    [Fact]
    public void Load_EnvironmentOverridesJson()
    {
        var path = WriteConfig("""{ "Sso": { "clientId": "from-json", "clientSecret": "json-secret" } }""");
        try
        {
            Environment.SetEnvironmentVariable("FILEE_WEB_CLIENT_ID", "from-env");
            Environment.SetEnvironmentVariable("FILEE_WEB_CLIENT_SECRET", "env-secret");
            try
            {
                var c = AppConfig.Load(Path.GetDirectoryName(path)!);
                Assert.Equal("from-env", c.Sso.ClientId);
                Assert.Equal("env-secret", c.Sso.ClientSecret);
            }
            finally
            {
                Environment.SetEnvironmentVariable("FILEE_WEB_CLIENT_ID", null);
                Environment.SetEnvironmentVariable("FILEE_WEB_CLIENT_SECRET", null);
            }
        }
        finally { try { Directory.Delete(Path.GetDirectoryName(path)!, recursive: true); } catch { } }
    }

    [Fact]
    public void ExampleConfig_IsValidJsonAndHasAllSections()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? example = null;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "appsettings.local.json.example");
            if (File.Exists(candidate)) { example = candidate; break; }
            dir = dir.Parent;
        }
        Assert.True(example is not null, "example config not found by walking up from test output");
        using var doc = JsonDocument.Parse(File.ReadAllText(example));
        var root = doc.RootElement;
        foreach (var section in new[] { "Server", "Sso", "Limits", "Tls", "Session", "Retention", "Storage", "opsToken" })
            Assert.True(root.TryGetProperty(section, out _), $"missing section {section}");
        var sso = root.GetProperty("Sso");
        Assert.True(sso.TryGetProperty("wellKnownPath", out _), "wellKnownPath must be documented");
        Assert.True(sso.TryGetProperty("clockSkewMinutes", out _), "clockSkewMinutes must be documented");
    }

    [Fact]
    public void Load_ReadsSsoWellKnownPathAndClockSkew()
    {
        var path = WriteConfig("""
        { "Sso": { "wellKnownPath": "/custom/well-known", "clockSkewMinutes": 9 } }
        """);
        try
        {
            var c = AppConfig.Load(Path.GetDirectoryName(path)!);
            Assert.Equal("/custom/well-known", c.Sso.WellKnownPath);
            Assert.Equal(9, c.Sso.ClockSkewMinutes);
        }
        finally { try { Directory.Delete(Path.GetDirectoryName(path)!, recursive: true); } catch { } }
    }
}
