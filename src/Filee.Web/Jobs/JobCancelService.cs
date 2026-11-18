namespace Filee.Web.Jobs;

public sealed record CancelResult(string JobId, string Status, bool CancellationRequested);

/// <summary>
/// Cancel only raises ConversionJob's flag and returns the status observed at call time (202 semantics).
/// The final state still arrives through SSE or GET.
/// </summary>
public sealed class JobCancelService(WebJobRegistry registry, JobStore store)
{
    public CancelResult Cancel(string webId)
    {
        var record = store.Get(webId) ?? throw new KeyNotFoundException("Job record not found.");
        // After a restart the in-memory job is gone (history is read-only). Nothing to cancel:
        // report it honestly instead of throwing a 500.
        var job = registry.Get(webId);
        if (job is null)
            return new CancelResult(webId, record.Status, CancellationRequested: false);
        job.Cancel();
        return new CancelResult(webId, record.Status, job.CancellationToken.IsCancellationRequested);
    }
}
