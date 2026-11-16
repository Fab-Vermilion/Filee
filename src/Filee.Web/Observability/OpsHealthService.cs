using Filee.Web.Jobs;

namespace Filee.Web.Observability;

public sealed record OpsHealthSnapshot(
    string Status,
    long FreeBytes,
    int ConcurrencyBudget,
    long StoredJobs,
    string? TlsCertificateExpiry);

/// <summary>Protected operational summary. It deliberately contains no paths, file names or credentials.</summary>
public sealed class OpsHealthService(WebStoragePaths storage, JobStore store, WebConcurrencyGate gate)
{
    public Task<OpsHealthSnapshot> GetAsync()
    {
        long freeBytes = 0;
        try
        {
            freeBytes = new DriveInfo(storage.Root).AvailableFreeSpace;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // The summary stays usable even when the volume cannot report free space.
        }

        return Task.FromResult(new OpsHealthSnapshot(
            "ok", freeBytes, gate.MaxConcurrency, store.Count(), null));
    }
}
