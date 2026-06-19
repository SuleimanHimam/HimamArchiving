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
