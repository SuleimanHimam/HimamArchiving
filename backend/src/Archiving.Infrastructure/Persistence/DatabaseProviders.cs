using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Archiving.Infrastructure.Persistence;

/// <summary>The relational engines this system runs on.</summary>
public enum DbProvider
{
    MySql,
    SqlServer,
}

/// <summary>
/// One place that decides which engine is in use and what the engine-specific bits of the model
/// look like.
///
/// The provider is configured, not guessed: <c>Database:Provider</c> in appsettings (or the
/// <c>ARCHIVING_DB_PROVIDER</c> environment variable for the design-time tooling). When it is
/// absent the connection string decides, so an existing MySQL deployment keeps working untouched
/// and a SQL Server connection string does the right thing without a second setting to remember.
/// </summary>
public static class DatabaseProviders
{
    public const string SqlServerMigrationsAssembly = "Archiving.Migrations.SqlServer";

    public static DbProvider Parse(string? name, string? connectionString = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Trim().ToLowerInvariant() switch
            {
                "sqlserver" or "mssql" or "sql-server" or "microsoftsqlserver" => DbProvider.SqlServer,
                "mysql" or "mariadb" => DbProvider.MySql,
                _ => throw new InvalidOperationException(
                    $"Unknown database provider '{name}'. Use 'MySql' or 'SqlServer'."),
            };
        }
        return Detect(connectionString);
    }

    /// <summary>
    /// Read the engine off the connection string. MySQL keyword syntax (<c>uid=</c>/<c>pwd=</c>,
    /// port 3306) is distinct enough from SQL Server's (<c>Initial Catalog</c>, integrated security,
    /// a named instance, <c>TrustServerCertificate</c>) that a wrong guess needs a deliberately
    /// malformed string — and an explicit <c>Database:Provider</c> always wins over this.
    /// </summary>
    private static DbProvider Detect(string? cs)
    {
        if (string.IsNullOrWhiteSpace(cs)) return DbProvider.MySql;
        var s = cs.ToLowerInvariant();
        if (s.Contains("uid=") || s.Contains("pwd=") || s.Contains("port=3306") || s.Contains("sslmode="))
            return DbProvider.MySql;
        if (s.Contains("initial catalog") || s.Contains("integrated security") || s.Contains("trusted_connection")
            || s.Contains("trustservercertificate") || s.Contains("user id=") || s.Contains(@"server=.\")
            || s.Contains("(localdb)") || s.Contains("data source="))
            return DbProvider.SqlServer;
        return DbProvider.MySql;
    }

    /// <summary>Point EF at the engine, including where that engine's migrations live.</summary>
    public static DbContextOptionsBuilder UseArchivingDatabase(
        this DbContextOptionsBuilder options, DbProvider provider, string connectionString)
        => provider switch
        {
            DbProvider.SqlServer => options.UseSqlServer(connectionString, o =>
            {
                // Each engine keeps its own migration history: the two schemas are generated from
                // the same model but are not interchangeable SQL.
                o.MigrationsAssembly(SqlServerMigrationsAssembly);
                // A dropped connection during a long report should retry rather than surface as a
                // 500 — the default is no retry at all.
                o.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            }),
            _ => options.UseMySQL(connectionString),
        };

    /// <summary>Typed overload, so the design-time factory keeps its <c>DbContextOptions&lt;T&gt;</c>.</summary>
    public static DbContextOptionsBuilder<TContext> UseArchivingDatabase<TContext>(
        this DbContextOptionsBuilder<TContext> options, DbProvider provider, string connectionString)
        where TContext : DbContext
    {
        UseArchivingDatabase((DbContextOptionsBuilder)options, provider, connectionString);
        return options;
    }

    /// <summary>
    /// The column type for free text with no useful length limit: a note, a JSON blob, an OCR
    /// transcript. Both are the engine's largest text type and neither can be indexed, which is
    /// exactly why the model states it explicitly instead of letting the convention decide.
    /// </summary>
    public static string LongText(this DatabaseFacade db)
        => db.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer" ? "nvarchar(max)" : "longtext";
}
