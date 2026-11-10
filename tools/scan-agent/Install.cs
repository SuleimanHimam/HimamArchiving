using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Win32;

/// <summary>
/// Turns the downloaded file into an installed program.
///
/// Until now the agent was whatever .exe the user happened to download, run from wherever the
/// browser put it — usually Downloads, which people empty. Nothing appeared in the Start menu, so
/// the only way back to it after quitting was to find that file again, and the run-at-login entry
/// pointed into a folder whose contents are routinely deleted.
///
/// Installing means: one copy in a stable per-user location, a Start-menu and desktop shortcut, an
/// entry in Settings ▸ Apps that can uninstall it, and a login entry pointing at the installed copy
/// rather than at the download.
///
/// Everything is per-user — %LOCALAPPDATA% and HKCU, no elevation. That is the same reasoning as
/// <see cref="Startup"/>: the agent brokers one person's scanner over their own loopback interface,
/// so it is per-user by nature, and an installer that demanded an administrator would put it out of
/// reach of the employees who actually need it.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class Install
{
    public const string DisplayName = "وكيل المسح الضوئي — نظام الأرشفة";
    private const string ShortcutName = "وكيل المسح الضوئي";
    private const string ExeName = "archiving-scan-agent.exe";
    private const string IconName = "agent.ico";
    private const string UninstallKey =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ArchivingScanAgent";

    /// <summary>%LOCALAPPDATA%\Programs — where per-user installs belong on Windows, and what
    /// Settings ▸ Apps expects an unelevated program to occupy.</summary>
    public static string Directory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "ArchivingScanAgent");

    public static string InstalledExe => Path.Combine(Directory, ExeName);
    public static string IconPath => Path.Combine(Directory, IconName);

    public static string StartMenuShortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), ShortcutName + ".lnk");

    public static string DesktopShortcut => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName + ".lnk");

    public static bool IsInstalled => File.Exists(InstalledExe);

    /// <summary>Whether *this* process is the installed copy. False means the user is running the
    /// download, and anything we register must point at the installed file, not at this one.</summary>
    public static bool RunningInstalled =>
        string.Equals(Startup.ExecutablePath, InstalledExe, StringComparison.OrdinalIgnoreCase);

    /// <summary>The installed copy is a different build from this one — i.e. the user downloaded a
    /// newer agent and is running it from Downloads while an older copy sits installed.</summary>
    public static bool NeedsUpdate =>
        IsInstalled && !RunningInstalled &&
        File.GetLastWriteTimeUtc(Startup.ExecutablePath) > File.GetLastWriteTimeUtc(InstalledExe).AddSeconds(2);

    /// <summary>
    /// Copies the running file into place, writes the icon and the shortcuts, and registers the
    /// program with Windows. Returns the installed executable's path.
    ///
    /// Safe to run over an existing install: it is how an update is applied.
    /// </summary>
    public static string Run(bool desktopShortcut, bool runAtLogin)
    {
        var source = Startup.ExecutablePath;
        if (string.IsNullOrEmpty(source) || !File.Exists(source))
            throw new InvalidOperationException("تعذّر تحديد مسار ملف البرنامج الحالي");

        System.IO.Directory.CreateDirectory(Directory);

        // Copying onto a running file is what fails here, and the message has to say which file and
        // what to do about it — "access denied" on its own sends people to the wrong place.
        if (!string.Equals(source, InstalledExe, StringComparison.OrdinalIgnoreCase))
        {
            try { CopyProgram(source); }
            catch (IOException ex)
            {
                throw new InvalidOperationException(
                    "نسخة أخرى من الوكيل قيد التشغيل من مجلد التثبيت. أغلقها من أيقونة شريط المهام ثم أعد المحاولة.", ex);
            }
        }

        WriteIcon();

        CreateShortcut(StartMenuShortcut);
        if (desktopShortcut) CreateShortcut(DesktopShortcut);
        else TryDelete(DesktopShortcut);

        Register();
        if (runAtLogin) Startup.SetEnabled(true);
        return InstalledExe;
    }

    /// <summary>
    /// Removes the shortcuts, the login entry and the Apps entry — and, when asked, the files.
    ///
    /// The files go last and from outside this process: an executable cannot delete itself while it
    /// is running, so a detached shell waits for it to exit and then removes the folder.
    /// </summary>
    public static void Remove(bool deleteFiles)
    {
        TryDelete(StartMenuShortcut);
        TryDelete(DesktopShortcut);
        try { Startup.SetEnabled(false); } catch { /* already gone, or locked by policy */ }
        try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false); } catch { }

        if (!deleteFiles || !System.IO.Directory.Exists(Directory)) return;
        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe",
                $"/c timeout /t 3 /nobreak >nul & rd /s /q \"{Directory}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        }
        catch { /* the folder stays; the program is already unregistered and unreachable from the shell */ }
    }

    /// <summary>
    /// Brings the program across.
    ///
    /// What ships is a self-contained single file, so copying that one file is the whole install.
    /// A build from source is not: the exe is a stub beside its runtime DLLs, and copying it alone
    /// produces an installed program that cannot start — it fails at launch, long after the install
    /// reported success. The runtimeconfig beside the exe is what tells the two apart, and when it
    /// is there the whole folder comes with it.
    /// </summary>
    private static void CopyProgram(string source)
    {
        var sourceDir = Path.GetDirectoryName(source)!;
        var multiFile = File.Exists(Path.Combine(sourceDir, Path.GetFileNameWithoutExtension(source) + ".runtimeconfig.json"));

        if (!multiFile) { File.Copy(source, InstalledExe, overwrite: true); return; }

        foreach (var dir in System.IO.Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            System.IO.Directory.CreateDirectory(Path.Combine(Directory, Path.GetRelativePath(sourceDir, dir)));

        foreach (var file in System.IO.Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(Directory, Path.GetRelativePath(sourceDir, file));
            // The icon is ours and is rewritten below; anything else is part of the program.
            if (string.Equals(Path.GetFileName(target), IconName, StringComparison.OrdinalIgnoreCase)) continue;
            File.Copy(file, target, overwrite: true);
        }

        // The exe may be named after the project rather than sitting at InstalledExe's name.
        if (!File.Exists(InstalledExe)) File.Copy(source, InstalledExe, overwrite: true);
    }

    /// <summary>The Settings ▸ Apps entry, so the agent can be found and removed the way every other
    /// program is, instead of being an orphan file the user has to know about.</summary>
    private static void Register()
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey, writable: true);
        if (key is null) return;

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        key.SetValue("DisplayName", DisplayName);
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "نظام الأرشفة الإلكتروني");
        key.SetValue("DisplayIcon", File.Exists(IconPath) ? IconPath : InstalledExe);
        key.SetValue("InstallLocation", Directory);
        key.SetValue("UninstallString", $"\"{InstalledExe}\" --uninstall");
        key.SetValue("QuietUninstallString", $"\"{InstalledExe}\" --uninstall --silent");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        try
        {
            // Reported in KB, which is what the Apps list shows as the size.
            key.SetValue("EstimatedSize", (int)(new FileInfo(InstalledExe).Length / 1024), RegistryValueKind.DWord);
        }
        catch { /* size is cosmetic */ }
    }

    /// <summary>
    /// Writes the shortcut icon.
    ///
    /// The agent draws its icon at runtime rather than embedding one (see <see cref="AppIcon"/>), so
    /// there is no resource for the shell to point at — a shortcut would fall back to the generic
    /// executable icon. Rendering the same glyph once at install time keeps the single-file agent
    /// intact and still gives the Start menu the real icon.
    /// </summary>
    private static void WriteIcon()
    {
        try
        {
            using var icon = AppIcon.Create(running: true, size: 256);
            using var bmp = icon.ToBitmap();
            using var png = new MemoryStream();
            bmp.Save(png, ImageFormat.Png);
            var bytes = png.ToArray();

            // A Vista-era PNG-compressed .ico: 6-byte directory, one 16-byte entry, then the PNG.
            using var file = File.Create(IconPath);
            using var w = new BinaryWriter(file);
            w.Write((short)0); w.Write((short)1); w.Write((short)1);   // reserved, type=icon, count
            w.Write((byte)0); w.Write((byte)0);                        // 0 × 0 means 256 × 256
            w.Write((byte)0); w.Write((byte)0);                        // palette, reserved
            w.Write((short)1); w.Write((short)32);                     // planes, bits per pixel
            w.Write(bytes.Length);
            w.Write(6 + 16);                                           // offset to the image data
            w.Write(bytes);
        }
        catch { /* a missing icon costs appearance, not function — never fail an install over it */ }
    }

    /// <summary>
    /// Creates a .lnk through the shell's own automation object.
    ///
    /// Late-bound on purpose: it is the same approach the agent already uses for WIA, and it keeps
    /// the project free of an interop assembly for the sake of four property assignments.
    /// </summary>
    private static void CreateShortcut(string linkPath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("تعذّر إنشاء الاختصار — مكوّن Windows Script Host غير متاح");
        var shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("تعذّر إنشاء الاختصار");

        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        var link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [linkPath])
            ?? throw new InvalidOperationException("تعذّر إنشاء الاختصار");
        var t = link.GetType();

        void Set(string name, object value) =>
            t.InvokeMember(name, BindingFlags.SetProperty, null, link, [value]);

        Set("TargetPath", InstalledExe);
        Set("WorkingDirectory", Directory);
        Set("Description", DisplayName);
        if (File.Exists(IconPath)) Set("IconLocation", IconPath + ",0");
        t.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}
