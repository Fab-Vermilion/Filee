using Filee.Web.Contracts;

namespace Filee.Web.Jobs;

/// <summary>Persisted job summary. Internal Filee job ids are stored for mapping only, never returned.</summary>
public sealed record JobRecord(
    string WebId,
    string InternalId,
    string Tid,
    string Sub,
    string Target,
    string Status,
    double Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt);

public sealed record JobFileSnapshot(
    string Id,
    string Name,
    long Size,
    string Status,
    double Progress,
    ApiError? Error = null,
    string? DownloadUrl = null);

public sealed record JobSnapshot(
    string JobId,
    string Status,
    double Progress,
    DateTimeOffset CreatedAt,
    IReadOnlyList<JobFileSnapshot> Files)
{
    public static JobSnapshot From(JobRecord record) =>
        new(record.WebId, record.Status, record.Progress, record.CreatedAt, []);
}
