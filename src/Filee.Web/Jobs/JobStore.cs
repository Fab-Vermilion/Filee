using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Filee.Web.Jobs;

/// <summary>
/// Minimal SQLite persistence for Web job summaries. Paths, file names and error details are deliberately absent.
/// </summary>
public sealed class JobStore
{
    private const string CreateSql = """
        CREATE TABLE IF NOT EXISTS jobs (
            web_id TEXT PRIMARY KEY,
            internal_id TEXT NOT NULL,
            tid TEXT NOT NULL,
            sub TEXT NOT NULL,
            target TEXT NOT NULL,
            status TEXT NOT NULL,
            progress REAL NOT NULL,
            created_at TEXT NOT NULL,
            finished_at TEXT NULL,
            output_files TEXT NOT NULL DEFAULT '[]'
        );
        CREATE INDEX IF NOT EXISTS idx_jobs_owner ON jobs(tid, sub);
        """;
    // Older databases predate output_files; add it in place so existing records stay usable.
    private const string AddOutputFilesSql = "ALTER TABLE jobs ADD COLUMN output_files TEXT NOT NULL DEFAULT '[]';";

    private readonly string _connectionString;

    public JobStore(string databasePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = true }.ToString();

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = CreateSql;
        command.ExecuteNonQuery();
        // ALTER is idempotent-safe here: run only when the column is missing.
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('jobs') WHERE name = 'output_files';";
        if ((long)(command.ExecuteScalar() ?? 0L) == 0)
        {
            command.CommandText = AddOutputFilesSql;
            command.ExecuteNonQuery();
        }
    }

    public void Upsert(JobRecord record)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO jobs (web_id, internal_id, tid, sub, target, status, progress, created_at, finished_at, output_files)
            VALUES ($webId, $internalId, $tid, $sub, $target, $status, $progress, $createdAt, $finishedAt, $outputFiles)
            ON CONFLICT(web_id) DO UPDATE SET
                internal_id = excluded.internal_id,
                tid = excluded.tid,
                sub = excluded.sub,
                target = excluded.target,
                status = excluded.status,
                progress = excluded.progress,
                created_at = excluded.created_at,
                finished_at = excluded.finished_at,
                output_files = excluded.output_files;
            """;
        AddParameters(command, record);
        command.ExecuteNonQuery();
    }

    public void UpdateSummary(string webId, string status, double progress, DateTimeOffset? finishedAt)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE jobs SET status = $status, progress = $progress, finished_at = $finishedAt
            WHERE web_id = $webId;
            """;
        command.Parameters.AddWithValue("$webId", webId);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$progress", progress);
        command.Parameters.AddWithValue("$finishedAt", finishedAt is { } value ? Format(value) : DBNull.Value);
        command.ExecuteNonQuery();
    }

    /// <summary>Persists the output file names once a job finishes, so history can offer downloads after a restart.</summary>
    public void UpdateOutputFiles(string webId, IReadOnlyList<string> names)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE jobs SET output_files = $outputFiles WHERE web_id = $webId;";
        command.Parameters.AddWithValue("$webId", webId);
        command.Parameters.AddWithValue("$outputFiles", JsonSerializer.Serialize(names));
        command.ExecuteNonQuery();
    }

    /// <summary>Removes a job's summary when its files are reclaimed. Returns true if a row was deleted.</summary>
    public bool Delete(string webId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM jobs WHERE web_id = $webId;";
        command.Parameters.AddWithValue("$webId", webId);
        return command.ExecuteNonQuery() > 0;
    }

    public JobRecord? Get(string webId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM jobs WHERE web_id = $webId;";
        command.Parameters.AddWithValue("$webId", webId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadRecord(reader) : null;
    }

    public long Count()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM jobs;";
        return (long)(command.ExecuteScalar() ?? 0L);
    }

    public IReadOnlyList<JobRecord> ForOwner(string tid, string sub)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM jobs WHERE tid = $tid AND sub = $sub ORDER BY created_at DESC;";
        command.Parameters.AddWithValue("$tid", tid);
        command.Parameters.AddWithValue("$sub", sub);
        using var reader = command.ExecuteReader();
        var records = new List<JobRecord>();
        while (reader.Read())
            records.Add(ReadRecord(reader));
        return records;
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void AddParameters(SqliteCommand command, JobRecord record)
    {
        command.Parameters.AddWithValue("$webId", record.WebId);
        command.Parameters.AddWithValue("$internalId", record.InternalId);
        command.Parameters.AddWithValue("$tid", record.Tid);
        command.Parameters.AddWithValue("$sub", record.Sub);
        command.Parameters.AddWithValue("$target", record.Target);
        command.Parameters.AddWithValue("$status", record.Status);
        command.Parameters.AddWithValue("$progress", record.Progress);
        command.Parameters.AddWithValue("$createdAt", Format(record.CreatedAt));
        command.Parameters.AddWithValue("$finishedAt", record.FinishedAt is { } finished ? Format(finished) : DBNull.Value);
        command.Parameters.AddWithValue("$outputFiles", JsonSerializer.Serialize(record.OutputFiles ?? []));
    }

    private static JobRecord ReadRecord(SqliteDataReader reader) => new(
        reader.GetString(reader.GetOrdinal("web_id")),
        reader.GetString(reader.GetOrdinal("internal_id")),
        reader.GetString(reader.GetOrdinal("tid")),
        reader.GetString(reader.GetOrdinal("sub")),
        reader.GetString(reader.GetOrdinal("target")),
        reader.GetString(reader.GetOrdinal("status")),
        reader.GetDouble(reader.GetOrdinal("progress")),
        DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("created_at")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        reader.IsDBNull(reader.GetOrdinal("finished_at"))
            ? null
            : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("finished_at")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        JsonSerializer.Deserialize<IReadOnlyList<string>>(reader.GetString(reader.GetOrdinal("output_files"))) ?? []);

    private static string Format(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);
}
