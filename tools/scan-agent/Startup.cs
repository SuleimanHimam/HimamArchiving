using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

/// <summary>Run-at-login registration.
///
/// Uses HKEY_CURRENT_USER\...\Run rather than a service or an HKLM entry, and that is deliberate:
/// the agent brokers access to a scanner physically attached to one person's desk and listens on
/// their loopback interface only, so it is per-user by nature. HKCU also needs no administrator
/// rights, which means the checkbox in the UI actually works for an ordinary employee.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class Startup
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ArchivingScanAgent";

    /// <summary>The shortcut the old install-startup.bat dropped in the Startup folder. Kept here so
    /// enabling the new mechanism can clear the old one — with both in place Windows launches the
    /// agent twice at login, and the second copy dies on the port with a confusing error.</summary>
    private static string LegacyShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Archiving Scan Agent.lnk");

    public static string ExecutablePath =>
        Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";

    /// <summary>
    /// The exact command Windows should run at login. --background starts the agent straight to the
    /// tray: signing in should not throw a window in the user's face.
    ///
    /// Always the *installed* copy when there is one, never whichever file happens to be running.
    /// Otherwise ticking the box while running the download registers a path inside Downloads — a
    /// folder people empty — and the agent silently stops starting one morning.
    /// </summary>
    private static string LaunchCommand =>
        $"\"{(Install.IsInstalled ? Install.InstalledExe : ExecutablePath)}\" --background";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string s && s.Length > 0;
        }
        catch { return false; }
    }

    /// <summary>True when a Run entry exists but points somewhere else — i.e. the agent was moved or
    /// replaced and the stale entry would launch the wrong (or a missing) file at next login.</summary>
    public static bool IsStale()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            if (key?.GetValue(ValueName) is not string s || s.Length == 0) return false;
            return !string.Equals(s, LaunchCommand, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static bool LegacyShortcutExists()
    {
        try { return File.Exists(LegacyShortcutPath); }
        catch { return false; }
    }

    /// <summary>Registers or removes the login entry. Throws with a readable message on failure —
    /// group policy can lock the Run key down, and silently ignoring that would leave the checkbox
    /// looking enabled while nothing happens at login.</summary>
    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException("تعذّر فتح إعدادات بدء التشغيل في السجل");

            if (enabled)
            {
                key.SetValue(ValueName, LaunchCommand, RegistryValueKind.String);
                RemoveLegacyShortcut();
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                "لا توجد صلاحية لتعديل إعدادات بدء التشغيل — قد تكون مقيدة بسياسة المؤسسة.", ex);
        }
    }

    private static void RemoveLegacyShortcut()
    {
        try { if (File.Exists(LegacyShortcutPath)) File.Delete(LegacyShortcutPath); }
        catch { /* best effort — a leftover shortcut is harmless once the mutex blocks the duplicate */ }
    }
}
