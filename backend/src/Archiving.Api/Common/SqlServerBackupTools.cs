using Microsoft.Data.SqlClient;

namespace Archiving.Api.Common;

/// <summary>
/// Backup and restore for SQL Server, the counterpart of <see cref="MySqlBackupTools"/>.
///
/// The shape of the job is different from MySQL's and that difference is visible here: mysqldump
/// is a client program that streams SQL to whoever ran it, while SQL Server's BACKUP runs *inside
/// the engine* and writes a .bak with the service account's identity, on the database server's own
/// filesystem. So a download is two steps — ask the engine to write the file, then stream that file
/// back — and it only works when the API can reach the path the engine wrote to. That is always
/// true for a single-box install (API and SQL Server on the same Windows Server, the usual layout
/// here) and needs a share when the database lives on another machine.
/// </summary>
public static class SqlServerBackupTools
{
    public static string DatabaseName(string connectionString)
    {
        var b = new SqlConnectionStringBuilder(connectionString);
        return string.IsNullOrWhiteSpace(b.InitialCatalog) ? "Archiving" : b.InitialCatalog;
    }

    /// <summary>A connection to <c>master</c>: a database cannot be restored while it is in use.</summary>
    private static string MasterConnection(string connectionString)
        => new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ConnectionString;

    /// <summary>Where the instance puts backups unless told otherwise — used when nothing is configured.</summary>
    public static async Task<string?> DefaultBackupDirectoryAsync(string connectionString, CancellationToken ct)
    {
        await using var cn = new SqlConnection(MasterConnection(connectionString));
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(
            "SELECT CONVERT(nvarchar(400), SERVERPROPERTY('InstanceDefaultBackupPath'))", cn);
        return await cmd.ExecuteScalarAsync(ct) as string;
    }

    /// <summary>Is the engine reachable, and can it write a backup where we intend to put one?</summary>
    public static async Task<(bool Ok, string? Directory, string? Error)> ProbeAsync(
        string connectionString, string? configuredDirectory, CancellationToken ct)
    {
        try
        {
            var dir = configuredDirectory ?? await DefaultBackupDirectoryAsync(connectionString, ct);
            return (true, dir, null);
        }
        catch (Exception ex) { return (false, configuredDirectory, ex.Message); }
    }

    /// <summary>
    /// Ask the engine to write a full backup and return the path it wrote to.
    /// COPY_ONLY so a manual download never disturbs an existing log-backup chain — this is a
    /// convenience export, not part of whoever's real backup policy.
    /// </summary>
    public static async Task<string> BackupAsync(
        string connectionString, string? directory, CancellationToken ct)
    {
        var dbName = DatabaseName(connectionString);
        var dir = directory ?? await DefaultBackupDirectoryAsync(connectionString, ct)
            ?? throw new InvalidOperationException("No backup directory is configured and the instance reports none.");
        var path = Path.Combine(dir, $"archiving_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak");

        await using var cn = new SqlConnection(MasterConnection(connectionString));
        await cn.OpenAsync(ct);
        // The database name cannot be a parameter (it is an identifier, not a value), so it is
        // quoted with QUOTENAME the way SQL Server itself does; the path is a real parameter.
        await using var cmd = new SqlCommand(
            $"BACKUP DATABASE {Quote(dbName)} TO DISK = @path WITH INIT, FORMAT, COPY_ONLY, NAME = N'Archiving full backup'", cn)
        { CommandTimeout = 0 };
        cmd.Parameters.AddWithValue("@path", path);
        await cmd.ExecuteNonQueryAsync(ct);
        return path;
    }

    /// <summary>
    /// Replace the database from a .bak the engine can read.
    /// The database is taken to single-user first: RESTORE cannot run while anything else — the
    /// API's own pool included — holds a connection, and rolling those back is the point.
    /// </summary>
    public static async Task RestoreAsync(string connectionString, string bakPath, CancellationToken ct)
    {
        var dbName = DatabaseName(connectionString);
        await using var cn = new SqlConnection(MasterConnection(connectionString));
        await cn.OpenAsync(ct);

        async Task Exec(string sql, params (string Name, object Value)[] ps)
        {
            await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = 0 };
            foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await Exec($"ALTER DATABASE {Quote(dbName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE");
        try
        {
            await Exec($"RESTORE DATABASE {Quote(dbName)} FROM DISK = @path WITH REPLACE", ("@path", bakPath));
        }
        finally
        {
            // Even a failed restore must not leave the database locked to one session.
            await Exec($"ALTER DATABASE {Quote(dbName)} SET MULTI_USER");
        }
    }

    /// <summary>SQL Server's own identifier quoting: `]` doubled, wrapped in brackets.</summary>
    private static string Quote(string identifier) => "[" + identifier.Replace("]", "]]") + "]";
}
