using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>Serves client-side helper downloads (the local scan agent installer).
/// Anonymous so the link works from the login page.</summary>
[ApiController]
[Route("api/downloads")]
public sealed class DownloadsController(IConfiguration config) : ControllerBase
{
    [HttpGet("scan-agent")]
    [AllowAnonymous]
    public IActionResult ScanAgent()
    {
        var path = config["Downloads:ScanAgentPath"];
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            return NotFound(new { error = "ملف برنامج المسح غير متوفر على الخادم بعد" });

        return PhysicalFile(path, "application/octet-stream", "archiving-scan-agent.exe");
    }

    /// <summary>Lets the UI show/hide the download link without attempting a 70 MB request.</summary>
    [HttpGet("scan-agent/available")]
    [AllowAnonymous]
    public IActionResult ScanAgentAvailable()
    {
        var path = config["Downloads:ScanAgentPath"];
        var available = !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path);
        return Ok(new { available });
    }

    /// <summary>
    /// A one-click installer for the desktop app window, generated for whatever address this
    /// request arrived on.
    ///
    /// The browser's own "install app" prompt only appears on a secure origin, so a LAN
    /// deployment over plain http never offers it. This produces the same result — a window with
    /// no tab strip and no address bar, its own taskbar button and the system's icon — by writing
    /// a shortcut that launches the installed browser in application mode. It needs no
    /// administrator rights, installs nothing, and touches only the user's own desktop.
    /// </summary>
    [HttpGet("desktop-shortcut")]
    [AllowAnonymous]
    public IActionResult DesktopShortcut()
    {
        var script = BuildShortcutScript(PublicBaseUrl());
        // ASCII: cmd.exe renders a .cmd in the console code page, where anything else is mojibake.
        // The Arabic shortcut name travels as base64 inside the script instead.
        return File(System.Text.Encoding.ASCII.GetBytes(script),
                    "application/octet-stream", "install-desktop-app.cmd");
    }

    /// <summary>
    /// The address the user actually typed, which is not always the one the request carries.
    /// Behind the IIS reverse proxy the Host header is rewritten to 127.0.0.1:&lt;internal&gt;
    /// unless preserveHostHeader is on, and a shortcut pointing at loopback works on the server
    /// and nowhere else — so a forwarded header wins when one is present.
    /// </summary>
    private string PublicBaseUrl()
    {
        var host = First(Request.Headers["X-Forwarded-Host"]) ?? First(Request.Headers["X-Original-Host"]);
        var proto = First(Request.Headers["X-Forwarded-Proto"]);

        if (host is null || !IsSafeHost(host))
        {
            host = Request.Host.Value;
            proto = Request.Scheme;
        }
        if (proto is not ("http" or "https")) proto = Request.Scheme;

        return $"{proto}://{host}";
    }

    private static string? First(Microsoft.Extensions.Primitives.StringValues v)
    {
        var s = v.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(s)) return null;
        // A comma-joined chain (proxy after proxy) — the first entry is the original client's.
        var head = s.Split(',')[0].Trim();
        return head.Length == 0 ? null : head;
    }

    /// <summary>
    /// host[:port] of letters, digits, dots and dashes — nothing else.
    ///
    /// This value is written into a script the user then runs on their own machine, so a header
    /// carrying a quote, a space or an ampersand would be command injection with the server as
    /// the courier. Anything outside the pattern is discarded rather than escaped.
    /// </summary>
    private static bool IsSafeHost(string host) =>
        host.Length <= 255 &&
        System.Text.RegularExpressions.Regex.IsMatch(host, @"^[A-Za-z0-9.\-]+(:[0-9]{1,5})?$");

    private static string BuildShortcutScript(string baseUrl)
    {
        // Built as literals in the script rather than typed here: the .cmd is ASCII-only.
        var nameB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("الديوان الإلكتروني"));
        var descB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("نظام إدارة الوثائق والأرشفة"));

        // One PowerShell command on one line: single quotes throughout, no % and no nested double
        // quotes, because cmd.exe would eat all three.
        var ps = string.Join("; ", new[]
        {
            "$ErrorActionPreference='SilentlyContinue'",
            $"$base='{baseUrl}'",
            // /app is the system's own start route, matching start_url in the web manifest — the
            // shortcut lands inside the system, not on its front door.
            "$url=$base+'/app'",
            "$dir=Join-Path $env:LOCALAPPDATA 'Archiving'",
            "New-Item -ItemType Directory -Force -Path $dir | Out-Null",
            "$ico=Join-Path $dir 'app-icon.ico'",
            "try { Invoke-WebRequest ($base+'/app-icon.ico') -OutFile $ico -UseBasicParsing -TimeoutSec 20 } catch { }",
            "$b=@($env:ProgramFiles+'\\Microsoft\\Edge\\Application\\msedge.exe'," +
                "${env:ProgramFiles(x86)}+'\\Microsoft\\Edge\\Application\\msedge.exe'," +
                "$env:ProgramFiles+'\\Google\\Chrome\\Application\\chrome.exe'," +
                "${env:ProgramFiles(x86)}+'\\Google\\Chrome\\Application\\chrome.exe') | " +
                "Where-Object { Test-Path $_ } | Select-Object -First 1",
            $"$name=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{nameB64}'))",
            $"$desc=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{descB64}'))",
            "$lnk=Join-Path ([Environment]::GetFolderPath('Desktop')) ($name+'.lnk')",
            "$s=New-Object -ComObject WScript.Shell",
            "$l=$s.CreateShortcut($lnk)",
            // --app: no tab strip, no address bar, its own taskbar button. Without a Chromium
            // browser the URL goes to whatever opens http — a normal window, but it works.
            "if ($b) { $l.TargetPath=$b; $l.Arguments='--app='+$url+' --window-size=1400,900'; " +
                "$l.WorkingDirectory=Split-Path $b } " +
                "else { $l.TargetPath='rundll32.exe'; $l.Arguments='url.dll,FileProtocolHandler '+$url }",
            "if (Test-Path $ico) { $l.IconLocation=$ico+',0' }",
            "$l.Description=$desc",
            "$l.Save()",
            "Write-Host ''",
            "Write-Host ('  ' + $lnk) -ForegroundColor Green",
            "Write-Host ('  opens: ' + $url)",
            "if (-not $b) { Write-Host '  (no Edge or Chrome found - opens in the default browser)' -ForegroundColor Yellow }",
        });

        return string.Join("\r\n", new[]
        {
            "@echo off",
            "rem ==========================================================================",
            "rem  Archiving - put the system on this desktop as a program.",
            "rem",
            "rem  Downloaded from the system itself, so the address below is already the one",
            "rem  you use. Double-click it. Nothing is installed, no administrator rights",
            "rem  are needed, and the only thing written is a shortcut on your desktop.",
            "rem",
            "rem  ASCII-only on purpose: cmd.exe renders this file in the console code page.",
            "rem ==========================================================================",
            "echo.",
            "echo Creating the desktop shortcut...",
            $"powershell -NoProfile -ExecutionPolicy Bypass -Command \"{ps}\"",
            "echo.",
            "echo Done. Press any key to close.",
            "pause >nul",
            "",
        });
    }
}
