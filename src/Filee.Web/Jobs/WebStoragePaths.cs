namespace Filee.Web.Jobs;

/// <summary>
/// Three separated storage areas: upload staging, Filee's scratch work root and final outputs.
/// The SQLite database lives beside them, never inside an upload or output folder.
/// </summary>
public sealed record WebStoragePaths(string Root)
{
    public string Uploads => Path.Combine(Root, "uploads");
    public string Work => Path.Combine(Root, "work");
    public string Output => Path.Combine(Root, "output");
    public string Database => Path.Combine(Root, "jobs.db");

    public string UploadDirectory(string jobId) => Path.Combine(Uploads, jobId);
    public string OutputDirectory(string jobId) => Path.Combine(Output, jobId);

    public void Ensure()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Uploads);
        Directory.CreateDirectory(Work);
        Directory.CreateDirectory(Output);
    }

    public static string FindRepositoryRoot(string start)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(start));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Filee.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }
        return Path.GetFullPath(start);
    }
}
