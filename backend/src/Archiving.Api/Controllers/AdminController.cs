using Archiving.Api.Authorization;
using Archiving.Api.Common;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace Archiving.Api.Controllers;

public sealed record AutoBackupSettingsDto(
    bool Enabled, string? TargetPath, int IntervalHours,
    DateTime? LastRunAt, string? LastRunStatus, string? LastRunError);

public sealed record UpdateAutoBackupRequest(bool Enabled, string? TargetPath, int IntervalHours);

public sealed record TestPathRequest(string Path);

public sealed record BrowseDirEntry(string Name, string FullPath);

public sealed record BrowseResult(string? CurrentPath, string? ParentPath, IReadOnlyList<BrowseDirEntry> Directories, string? Error);

/// <summary>Database backup and restore — admin only.</summary>
[ApiController]
[Route("api/admin")]
[Authorize]
public sealed class AdminController(
    IConfiguration config, ILogger<AdminController> logger, AppDbContext db,
    Archiving.Application.Common.Interfaces.IAuditWriter audit) : ControllerBase
{
    // ── helpers ────────────────────────────────────────────────────────────

    private Dictionary<string, string> CsParams() =>
        MySqlBackupTools.ParseConnectionString(config.GetConnectionString("Default") ?? "");

    private static string? FindTool(string name) => MySqlBackupTools.FindTool(name);

    private static string WriteTempOptionFile(Dictionary<string, string> p) => MySqlBackupTools.WriteTempOptionFile(p);

    /// <summary>Verifies a directory exists (creating it if needed) and is writable.</summary>
    private static (bool Ok, string? Error) TestDirectoryWritable(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, $".write_test_{Guid.NewGuid():N}.tmp");
            System.IO.File.WriteAllText(probe, "ok");
            System.IO.File.Delete(probe);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ── endpoints ──────────────────────────────────────────────────────────

    /// <summary>Stream a full mysqldump SQL file for download.</summary>
    [HttpGet("backup")]
    [HasPermission("Backup.Edit")]
    public async Task<IActionResult> Backup(CancellationToken ct)
    {
        var dump = FindTool("mysqldump");
        if (dump is null)
            return StatusCode(503, new { error = "mysqldump_not_found" });

        var p = CsParams();
        p.TryGetValue("database", out var db); db ??= "archiving_db";

        var optFile = WriteTempOptionFile(p);
        try
        {
            var psi = new ProcessStartInfo(dump,
                $"--defaults-extra-file={optFile} --single-transaction --routines --triggers {db}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            var proc = Process.Start(psi)!;
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            Response.Headers.Append("Content-Disposition", $"attachment; filename=\"archiving_backup_{stamp}.sql\"");
            Response.ContentType = "application/octet-stream";

            await proc.StandardOutput.BaseStream.CopyToAsync(Response.Body, ct);
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0)
            {
                var err = await proc.StandardError.ReadToEndAsync(ct);
                logger.LogError("mysqldump exited {Code}: {Err}", proc.ExitCode, err);
            }
            return new EmptyResult();
        }
        finally { System.IO.File.Delete(optFile); }
    }

    /// <summary>Restore database from an uploaded SQL dump.</summary>
    [HttpPost("restore")]
    [HasPermission("Backup.Edit")]
    [RequestSizeLimit(500 * 1024 * 1024)]
    public async Task<IActionResult> Restore(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "no_file" });

        var mysql = FindTool("mysql");
        if (mysql is null)
            return StatusCode(503, new { error = "mysql_not_found" });

        var p = CsParams();
        p.TryGetValue("database", out var db); db ??= "archiving_db";

        var optFile = WriteTempOptionFile(p);
        try
        {
            var psi = new ProcessStartInfo(mysql,
                $"--defaults-extra-file={optFile} {db}")
            {
                RedirectStandardInput  = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            var proc = Process.Start(psi)!;
            await using (var stdin = proc.StandardInput.BaseStream)
                await file.OpenReadStream().CopyToAsync(stdin, ct);

            await proc.WaitForExitAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);

            if (proc.ExitCode != 0)
            {
                logger.LogError("mysql restore exited {Code}: {Err}", proc.ExitCode, stderr);
                return StatusCode(500, new { error = "restore_failed", details = stderr });
            }

            logger.LogInformation("Database restored from {File} ({Bytes} bytes)",
                file.FileName, file.Length);
            return Ok(new { restored = true, file = file.FileName, bytes = file.Length });
        }
        finally { System.IO.File.Delete(optFile); }
    }

    /// <summary>Check whether mysqldump and mysql are available on the server.</summary>
    [HttpGet("backup/status")]
    [HasPermission("Backup.View")]
    public IActionResult Status() => Ok(new
    {
        mysqldumpFound = FindTool("mysqldump") is not null,
        mysqlFound     = FindTool("mysql") is not null,
        mysqldumpPath  = FindTool("mysqldump"),
        mysqlPath      = FindTool("mysql"),
    });

    // ── scheduled auto-backup ────────────────────────────────────────────────

    private async Task<AutoBackupSettings> LoadOrCreateSettingsAsync(CancellationToken ct)
    {
        var s = await db.AutoBackupSettings.FirstOrDefaultAsync(ct);
        if (s is not null) return s;

        s = new AutoBackupSettings { Enabled = false, IntervalHours = 24 };
        db.AutoBackupSettings.Add(s);
        await db.SaveChangesAsync(ct);
        return s;
    }

    private static AutoBackupSettingsDto ToDto(AutoBackupSettings s) => new(
        s.Enabled, s.TargetPath, s.IntervalHours, s.LastRunAt, s.LastRunStatus, s.LastRunError);

    /// <summary>Current scheduled-backup configuration.</summary>
    [HttpGet("backup/auto")]
    [HasPermission("Backup.View")]
    public async Task<IActionResult> GetAutoBackup(CancellationToken ct)
        => Ok(ToDto(await LoadOrCreateSettingsAsync(ct)));

    /// <summary>Lists sub-folders of a server-side path (or top-level drives when path is empty), for the
    /// backup-destination folder picker. Read-only — never creates or modifies anything.</summary>
    [HttpGet("backup/auto/browse")]
    [HasPermission("Backup.Edit")]
    public IActionResult Browse([FromQuery] string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                if (OperatingSystem.IsWindows())
                {
                    var drives = DriveInfo.GetDrives()
                        .Where(d => d.IsReady)
                        .Select(d => new BrowseDirEntry(d.Name.TrimEnd('\\'), d.RootDirectory.FullName))
                        .ToList();
                    return Ok(new BrowseResult(null, null, drives, null));
                }
                path = "/";
            }

            var full = Path.GetFullPath(path);
            if (!Directory.Exists(full))
                return Ok(new BrowseResult(path, null, [], "المسار غير موجود"));

            var dirs = new DirectoryInfo(full)
                .GetDirectories()
                .Where(d => (d.Attributes & FileAttributes.Hidden) == 0 && (d.Attributes & FileAttributes.System) == 0)
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => new BrowseDirEntry(d.Name, d.FullName))
                .ToList();

            // Going up from a drive root (e.g. D:\) lands back on the drive list.
            var parentInfo = Directory.GetParent(full);
            var parent = parentInfo?.FullName;
            if (parent is null && OperatingSystem.IsWindows()) parent = "";

            return Ok(new BrowseResult(full, parent, dirs, null));
        }
        catch (UnauthorizedAccessException)
        {
            return Ok(new BrowseResult(path, null, [], "تم رفض الوصول إلى هذا المسار"));
        }
        catch (Exception ex)
        {
            return Ok(new BrowseResult(path, null, [], ex.Message));
        }
    }

    /// <summary>Tests whether a folder path exists (creating it if needed) and is writable, without saving anything.</summary>
    [HttpPost("backup/auto/test-path")]
    [HasPermission("Backup.Edit")]
    public IActionResult TestPath([FromBody] TestPathRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Path))
            return Ok(new { ok = false, error = "المسار مطلوب" });

        var (ok, error) = TestDirectoryWritable(req.Path.Trim());
        return Ok(new { ok, error });
    }

    /// <summary>Enable/disable the scheduled backup job and set its destination folder + interval.</summary>
    [HttpPut("backup/auto")]
    [HasPermission("Backup.Edit")]
    public async Task<IActionResult> UpdateAutoBackup([FromBody] UpdateAutoBackupRequest req, CancellationToken ct)
    {
        if (req.IntervalHours < 1 || req.IntervalHours > 720)
            return BadRequest(new { error = "الفاصل الزمني يجب أن يكون بين 1 و 720 ساعة" });

        if (req.Enabled)
        {
            if (string.IsNullOrWhiteSpace(req.TargetPath))
                return BadRequest(new { error = "حدد مسار حفظ النسخ الاحتياطية" });

            var (ok, error) = TestDirectoryWritable(req.TargetPath.Trim());
            if (!ok)
                return BadRequest(new { error = $"تعذّر الكتابة في المسار المحدد: {error}" });
        }

        var s = await LoadOrCreateSettingsAsync(ct);
        s.Enabled = req.Enabled;
        s.TargetPath = req.TargetPath?.Trim();
        s.IntervalHours = req.IntervalHours;
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(s.Enabled ? "AutoBackupEnabled" : "AutoBackupDisabled", "AutoBackupSettings", s.Id, s.TargetPath, ct: ct);

        return Ok(ToDto(s));
    }
}
