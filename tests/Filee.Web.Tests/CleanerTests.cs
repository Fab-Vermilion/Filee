using Filee.Web.Jobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Filee.Web.Tests;

public sealed class CleanerTests
{
    [Fact]
    public void CleanOnce_RemovesOnlyStaleJobDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "filee-cleaner-" + Guid.NewGuid().ToString("N")[..8]);
        var storage = new WebStoragePaths(root) { };
        try
        {
            storage.Ensure();
            var stale = Path.Combine(storage.Output, "stale-job");
            var fresh = Path.Combine(storage.Output, "fresh-job");
            Directory.CreateDirectory(stale);
            Directory.CreateDirectory(fresh);
            File.WriteAllText(Path.Combine(stale, "x.pdf"), "x");
            File.WriteAllText(Path.Combine(fresh, "y.pdf"), "y");
            var old = DateTimeOffset.UtcNow.AddDays(-3);
            File.SetLastWriteTimeUtc(Path.Combine(stale, "x.pdf"), old.UtcDateTime);

            var cleaner = new JobDirectoryCleaner(storage, NullLogger<JobDirectoryCleaner>.Instance);
            var removed = cleaner.CleanOnce(DateTimeOffset.UtcNow.AddDays(-1));

            Assert.True(removed >= 1);
            Assert.False(Directory.Exists(stale));
            Assert.True(Directory.Exists(fresh));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
