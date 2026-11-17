namespace Filee.Web.Jobs;

/// <summary>
/// Single source of truth for how long job records, files and download links are kept.
/// Kept in one place so the download token never outlives/underlives the files it points at.
/// </summary>
public static class WebRetention
{
    public static readonly TimeSpan FileAndRecordTtl = TimeSpan.FromHours(24);
    public static readonly TimeSpan DownloadTokenTtl = TimeSpan.FromHours(24);
    public static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
}
