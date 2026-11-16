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
        var job = registry.Get(webId) ?? throw new KeyNotFoundException("Web job not found.");
        var record = store.Get(webId) ?? throw new KeyNotFoundException("Job record not found.");
        job.Cancel();
        return new CancelResult(webId, record.Status, job.CancellationToken.IsCancellationRequested);
    }
}
