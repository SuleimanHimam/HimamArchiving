using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Archiving.Infrastructure.Persistence;

/// <summary>Used by the EF Core CLI (dotnet ef) at design time. Falls back to the local dev connection.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Design-time only (dotnet ef). Set ARCHIVING_DB to your local connection string, and
        // ARCHIVING_DB_PROVIDER to MySql or SqlServer when adding migrations for a given engine.
        // The placeholder below has no real password committed.
        var provider = DatabaseProviders.Parse(
            Environment.GetEnvironmentVariable("ARCHIVING_DB_PROVIDER"),
            Environment.GetEnvironmentVariable("ARCHIVING_DB"));

        var conn = Environment.GetEnvironmentVariable("ARCHIVING_DB")
            ?? (provider == DbProvider.SqlServer
                ? @"Server=.\SQLEXPRESS;Database=Archiving;Integrated Security=true;TrustServerCertificate=true;"
                : "server=localhost;port=3306;database=archiving_db;uid=archiver;pwd=CHANGE_ME;SslMode=Preferred;");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseArchivingDatabase(provider, conn)
            .Options;

        return new AppDbContext(options);
    }
}
