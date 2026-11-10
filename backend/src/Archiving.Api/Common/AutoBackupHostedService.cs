using System.Diagnostics;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Common;

/// <summary>Periodically checks the AutoBackupSettings row and runs a mysqldump to the configured
/// folder once the configured interval has elapsed since the last successful/attempted run.</summary>
public sealed class AutoBackupHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<AutoBackupHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try { await RunIfDueAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Auto-backup check failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunIfDueAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var settings = await db.AutoBackupSettings.FirstOrDefaultAsync(ct);
        if (settings is null || !settings.Enabled || string.IsNullOrWhiteSpace(settings.TargetPath))
            return;

        var due = settings.LastRunAt is null
            || DateTime.UtcNow - settings.LastRunAt.Value >= TimeSpan.FromHours(settings.IntervalHours);
        if (!due) return;

        try
        {
            var file = await DumpToFileAsync(settings.TargetPath, ct);
            settings.LastRunAt = DateTime.UtcNow;
            settings.LastRunStatus = "Success";
            settings.LastRunError = null;
            logger.LogInformation("Auto-backup completed: {File}", file);
        }
        catch (Exception ex)
        {
            settings.LastRunAt = DateTime.UtcNow;
            settings.LastRunStatus = "Failed";
            settings.LastRunError = ex.Message;
            logger.LogError(ex, "Auto-backup run failed");
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<string> DumpToFileAsync(string targetPath, CancellationToken ct)
    {
        var conn = config.GetConnectionString("Default") ?? "";
        if (DatabaseProviders.Parse(config["Database:Provider"], conn) == DbProvider.SqlServer)
        {
            // SQL Server writes the file itself, so the scheduled job hands it the target folder
            // instead of piping a dump into one. The folder must be writable by the SQL Server
            // service account — the settings screen's "test path" check tests the API's access,
            // which is the same account on a single-box install and not on a split one.
            Directory.CreateDirectory(targetPath);
            return await SqlServerBackupTools.BackupAsync(conn, targetPath, ct);
        }

        var dump = MySqlBackupTools.FindTool("mysqldump")
            ?? throw new InvalidOperationException("mysqldump not found on server");

        var p = MySqlBackupTools.ParseConnectionString(config.GetConnectionString("Default") ?? "");
        p.TryGetValue("database", out var dbName); dbName ??= "archiving_db";

        Directory.CreateDirectory(targetPath);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var filePath = Path.Combine(targetPath, $"archiving_autobackup_{stamp}.sql");

        var optFile = MySqlBackupTools.WriteTempOptionFile(p);
        try
        {
            var psi = new ProcessStartInfo(dump,
                $"--defaults-extra-file={optFile} --single-transaction --routines --triggers {dbName}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            var proc = Process.Start(psi)!;
            await using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                await proc.StandardOutput.BaseStream.CopyToAsync(fs, ct);
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0)
            {
                var err = await proc.StandardError.ReadToEndAsync(ct);
                File.Delete(filePath);
                throw new InvalidOperationException($"mysqldump exited {proc.ExitCode}: {err}");
            }
            return filePath;
        }
        finally { File.Delete(optFile); }
    }
}
