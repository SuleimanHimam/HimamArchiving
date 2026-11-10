using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

// Archiving — local scan agent.
//
// Runs on the USER's PC (where the scanner is plugged in) and exposes scanning over loopback so the
// cloud-hosted web app can acquire scans. Browsers allow an HTTPS page to call http://127.0.0.1, so
// this works even though the system itself is in the cloud. See AgentServer.cs for the endpoints.
//
// Modes:
//   archiving-scan-agent               tray icon + status window
//   archiving-scan-agent --background  starts straight to the tray (what the login entry runs)
//   archiving-scan-agent --console     no UI at all — the original headless behaviour, for scripts
//   archiving-scan-agent --mock        no scanner needed; returns a sample page (combines with the above)
//   archiving-scan-agent --install     install as a program: shortcuts, Apps entry, start at login
//   archiving-scan-agent --uninstall   remove it again (what Settings ▸ Apps calls)
//   --silent                           with either of the two above: no prompts, for unattended rollout
//   --app-url=https://archive.example  where the web app lives; used to build the pairing link and
//                                      worth passing at install time on a Remote Desktop host so
//                                      nobody has to type it into every session
//   --port=8770                        pin the listener instead of taking the first free port in
//                                      8765..8784 (diagnostics; the range is what makes several
//                                      Remote Desktop sessions able to run an agent each)

[SupportedOSPlatform("windows")]
internal static class Program
{
    // Local\ makes this per *session*, which is what we want: on a Remote Desktop host each signed-in
    // user runs their own agent for their own redirected devices, and only a second copy inside the
    // same session is a duplicate. Note this is NOT what keeps the sessions apart on the wire —
    // loopback is machine-wide, so that job belongs to the port range and the token in AgentServer.
    private const string MutexName = @"Local\ArchivingScanAgent";

    /// <summary>The live single-instance lock, kept so a hand-over can give it up deliberately.</summary>
    private static Mutex? _instanceLock;

    [STAThread]
    private static int Main(string[] args)
    {
        var mock = args.Contains("--mock");
        var console = args.Contains("--console") || args.Contains("--headless");
        var background = args.Contains("--background") || args.Contains("--minimized");
        var silent = args.Contains("--silent") || args.Contains("--quiet");
        var appUrl = ArgValue(args, "--app-url");
        var port = int.TryParse(ArgValue(args, "--port"), out var p) ? p : (int?)null;

        // Before anything prints — including the "already running" refusal below, which is the one
        // message a second instance ever emits and so must not be the one that comes out mangled.
        if (console) AttachParentConsole();

        // Installing and uninstalling run *before* the single-instance check: both are things you do
        // to the program rather than with it, and refusing to install because a copy is already in
        // the tray would make an update impossible without quitting first.
        // An unattended rollout onto a Remote Desktop host wants to set the address once, at install
        // time, rather than have every user type it into their own session — so --app-url is honoured
        // here too, not only on a normal run.
        if (args.Contains("--install"))
        {
            if (!string.IsNullOrWhiteSpace(appUrl)) AgentSettings.Load().SetAppUrl(appUrl!);
            return RunInstall(silent, console);
        }
        if (args.Contains("--uninstall")) return RunUninstall(silent, console);

        // One agent per session — without this the startup entry plus a manual launch would leave two
        // copies of the same user's agent racing for a port and for the scanner behind it.
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            var message = "وكيل المسح الضوئي يعمل بالفعل. ابحث عن الأيقونة في شريط المهام.";
            if (console) WriteConsole(message);
            else MessageBox.Show(message, "وكيل المسح الضوئي", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            return 0;
        }

        _instanceLock = mutex;

        var settings = AgentSettings.Load();
        if (!string.IsNullOrWhiteSpace(appUrl)) settings.SetAppUrl(appUrl!);

        var server = new AgentServer(settings, port) { MockMode = mock };
        return console ? RunConsole(server, settings) : RunTray(server, settings, background);
    }

    /// <summary>
    /// `--install` — copy into place, make the shortcuts, register at login, then start the
    /// installed copy so the user ends up where they would have been anyway.
    ///
    /// `--silent` suppresses both the confirmation and the launch, which is what an unattended
    /// rollout wants; a person double-clicking the download wants the opposite.
    /// </summary>
    private static int RunInstall(bool silent, bool console)
    {
        try
        {
            var exe = Install.Run(desktopShortcut: true, runAtLogin: true);
            var message = $"تم تثبيت وكيل المسح الضوئي.\n\nالمجلد: {Install.Directory}\n"
                        + "أُضيف اختصار إلى قائمة ابدأ وسطح المكتب، وسيبدأ الوكيل تلقائياً مع Windows.";
            if (console) WriteConsole(message);
            else if (!silent) Info(message, "تم التثبيت");

            // Nothing is running yet unless the user already had a copy up, in which case the mutex
            // in the new process stands aside quietly.
            if (!silent && !console)
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            return 0;
        }
        catch (Exception ex)
        {
            if (console) WriteConsole(ex.Message);
            else if (!silent) Error(ex.Message, "تعذّر التثبيت");
            return 1;
        }
    }

    /// <summary>`--uninstall` — what the Settings ▸ Apps entry invokes.</summary>
    private static int RunUninstall(bool silent, bool console)
    {
        if (!silent && !console)
        {
            var answer = MessageBox.Show(
                "إزالة وكيل المسح الضوئي من هذا الجهاز؟\n\nسيتوقف المسح الضوئي من داخل النظام حتى تُعيد تثبيته.",
                "إزالة البرنامج", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2, RtlBox);
            if (answer != DialogResult.Yes) return 0;
        }

        // A copy sitting in the tray keeps its own file locked, so ask it to go first: without this
        // the folder survives the uninstall and the program appears to still be there.
        StopRunningAgent();
        Install.Remove(deleteFiles: true);

        const string done = "تمت إزالة وكيل المسح الضوئي.";
        if (console) WriteConsole(done);
        else if (!silent) Info(done, "تمت الإزالة");
        return 0;
    }

    /// <summary>
    /// Hands this session over to another copy of the program — the installed one, when the user has
    /// been running the download.
    ///
    /// The single-instance mutex has to be given up first: it is held for the whole life of this
    /// process, so a replacement started while we still own it would meet the "already running"
    /// message, exit, and leave the user with no agent at all. Released from the UI thread, which is
    /// the thread that took it. Returns false when nothing was started, in which case the lock is
    /// taken back and this copy carries on.
    /// </summary>
    internal static bool HandOverTo(string exePath)
    {
        try { _instanceLock?.ReleaseMutex(); } catch { /* already given up */ }
        try
        {
            Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? "",
            });
            return true;
        }
        catch
        {
            try { _instanceLock?.WaitOne(0); } catch { /* someone else took it — we are the spare now */ }
            return false;
        }
    }

    /// <summary>Closes an agent already in the tray, so its executable can be deleted.</summary>
    private static void StopRunningAgent()
    {
        try
        {
            var me = Environment.ProcessId;
            foreach (var p in Process.GetProcessesByName("archiving-scan-agent"))
            {
                using (p)
                {
                    if (p.Id == me) continue;
                    if (!p.CloseMainWindow()) p.Kill();
                    p.WaitForExit(4000);
                }
            }
        }
        catch { /* nothing to stop, or no rights to stop it — the deletion below reports the truth */ }
    }

    private const MessageBoxOptions RtlBox = MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign;

    private static void Info(string text, string title) =>
        MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1, RtlBox);

    private static void Error(string text, string title) =>
        MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.Error,
            MessageBoxDefaultButton.Button1, RtlBox);

    private static int RunTray(AgentServer server, AgentSettings settings, bool background)
    {
        ApplicationConfiguration.Initialize();

        using var form = new AgentForm(server, settings, background);
        try
        {
            server.Start();
        }
        catch (AgentStartException ex)
        {
            // Show the window regardless: a tray-only failure would be invisible, and the log line
            // plus the red status dot are the only explanation the user gets.
            MessageBox.Show(ex.Message, "تعذّر تشغيل وكيل المسح الضوئي", MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            return 1;
        }

        Application.Run(form);
        server.Dispose();
        return 0;
    }

    /// <summary>The pre-UI behaviour, kept so existing scripts and service wrappers still work.</summary>
    private static int RunConsole(AgentServer server, AgentSettings settings)
    {
        server.Logged += WriteConsole;
        try
        {
            server.Start();
        }
        catch (AgentStartException ex)
        {
            WriteConsole(ex.Message);
            return 1;
        }

        // A headless agent has no button to press, so the one thing a shared-host operator needs —
        // the pairing link — is printed instead. Nothing else can hand it over without a UI.
        if (server.RequiresToken)
        {
            WriteConsole($"رمز الاقتران: {server.Token}");
            WriteConsole(string.IsNullOrWhiteSpace(settings.PairingUrl)
                ? "لم يُضبط عنوان النظام؛ مرّر ‎--app-url=‎ للحصول على رابط اقتران جاهز."
                : $"رابط الاقتران: {settings.PairingUrl}");
        }

        WriteConsole("اضغط Ctrl+C للإيقاف.");
        using var quit = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; quit.Set(); };
        quit.Wait();
        server.Dispose();
        return 0;
    }

    /// <summary>Reads <c>--name=value</c> (or <c>--name value</c>) out of the command line.
    /// Returns null when the switch is absent, which every caller treats as "leave it alone" rather
    /// than "clear it" — an install that forgot --app-url must not wipe a URL already configured.</summary>
    private static string? ArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                return args[i][(name.Length + 1)..].Trim('"');
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1].Trim('"');
        }
        return null;
    }

    private static void WriteConsole(string message)
    {
        try { Console.WriteLine(message); } catch { /* no console attached */ }
    }

    // The app is a WinExe so no console window flashes on login. That also means --console has no
    // stdout of its own, so borrow the terminal that launched it.
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);
    private const int AttachParentProcess = -1;

    private static void AttachParentConsole()
    {
        // May fail when stdout is already a pipe (`agent --console | tee log`) — that is fine and not
        // an error: there is nothing to attach to because output is already going somewhere.
        AttachConsole(AttachParentProcess);

        // Every message this agent prints is Arabic, and the default codepage mangles all of it, so
        // force UTF-8 — by two different routes, because the two cases need different treatment:
        //
        //   attached to a real console  -> set OutputEncoding, which also switches the console itself
        //   stdout redirected to a pipe -> OutputEncoding THROWS IOException, and swallowing that was
        //                                  the original bug: output silently fell back to the OEM
        //                                  codepage. Wrap the pipe in a UTF-8 writer instead.
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (IOException)
        {
            var stdout = new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false))
            {
                AutoFlush = true,
            };
            Console.SetOut(stdout);
        }
    }
}
