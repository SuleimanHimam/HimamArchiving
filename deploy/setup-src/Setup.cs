// Archiving — Setup.exe
//
// One file to hand to whoever runs the server: it carries the whole package inside it, unpacks it
// where they choose, and hands straight over to the install wizard. No zip to extract by hand, no
// script to find, and the icon on it is the system's own.
//
// Written against C# 5 / .NET Framework 4.x on purpose: that compiler ships inside Windows, so the
// installer builds with nothing installed beyond what is already on the machine, and the result
// runs on any Windows Server without a runtime of its own.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class Setup
{
    // The package zip, embedded at build time (see build-setup.ps1).
    const string PayloadResource = "package.zip";
    const string DefaultFolder = @"C:\Archiving";

    [STAThread]
    static int Main(string[] args)
    {
        // Silent mode, for deploying to several servers without sitting through the window:
        //   Archiving-Setup.exe /S [folder]
        // Unpacks and registers the Windows Service, left stopped — the database settings still
        // have to be given, by running install-service.ps1 with its parameters afterwards, which
        // is also what starts it.
        foreach (string a in args)
        {
            if (string.Equals(a, "/S", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "/silent", StringComparison.OrdinalIgnoreCase))
            {
                string folder = DefaultFolder;
                foreach (string b in args)
                    if (!b.StartsWith("/")) { folder = b; break; }
                try { ExtractTo(folder, null, null); return 0; }
                catch (Exception ex)
                {
                    // No console to write to from a windowed exe; leave the reason on disk.
                    try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "archiving-setup-error.txt"), ex.ToString()); }
                    catch { }
                    return 1;
                }
            }
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SetupForm());
        return 0;
    }

    const string ServiceName = "Archiving";
    const string ServiceDisplayName = "Archiving — الديوان الإلكتروني";
    const string ServiceDescription = "نظام الأرشفة الإلكترونية — الديوان الإلكتروني";
    // The account install-service.ps1 defaults to. The wizard reconfigures whatever service it
    // finds, so an installation that needs a domain account still gets one; this only has to be an
    // account that exists on every Windows Server and can run the API.
    const string ServiceAccount = @"NT AUTHORITY\NetworkService";

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern int GetLongPathName(string shortPath, StringBuilder buffer, int length);

    /// <summary>
    /// One spelling of a path, for comparing two of them.
    ///
    /// Path.GetFullPath does not expand 8.3 short names, so a folder handed in as
    /// C:\Users\ADMINI~1\… never matches the C:\Users\Administrator\… that Windows reports for a
    /// running process — and the check that is supposed to notice "this process is holding the
    /// file I am about to write" silently decides it is someone else's.
    /// </summary>
    static string Canonical(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        try
        {
            string full = Path.GetFullPath(path);
            StringBuilder sb = new StringBuilder(1024);
            int n = GetLongPathName(full, sb, sb.Capacity);
            // Returns 0 for a path that does not exist yet; the full form is then the best we have.
            return (n > 0 && n < sb.Capacity ? sb.ToString() : full).TrimEnd('\\');
        }
        catch { return path; }
    }

    /// <summary>
    /// Where the installed service runs from, or null if it is not installed. Read from the
    /// registry rather than WMI so this works on a locked-down server with no extra references.
    /// </summary>
    static string ServiceFolder()
    {
        try
        {
            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\" + ServiceName))
            {
                if (k == null) return null;
                object v = k.GetValue("ImagePath");
                if (v == null) return null;
                string image = v.ToString().Trim().Trim('"');
                return Canonical(Path.GetDirectoryName(image));
            }
        }
        catch { return null; }
    }

    /// <summary>
    /// Stop the service if it is the one installed in <paramref name="target"/>, and say whether it
    /// was actually stopped. A running service holds Archiving.Api.dll open, and extraction over it
    /// fails with "the process cannot access the file because it is being used by another process" —
    /// which is exactly what re-running setup on a working installation used to do.
    ///
    /// Scoped to a service running from this very folder: stopping an installation somewhere else
    /// on the machine is not this setup's business.
    /// </summary>
    static bool StopServiceIn(string target)
    {
        string folder = ServiceFolder();
        if (folder == null) return false;
        if (!string.Equals(folder, Canonical(target), StringComparison.OrdinalIgnoreCase)) return false;

        try
        {
            ServiceController sc = new ServiceController(ServiceName);
            if (sc.Status == ServiceControllerStatus.Stopped) return false;
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
            // The process exits a moment after the service reports Stopped; the handles on its
            // files go with it, not with the status change.
            Thread.Sleep(1500);
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Close the scan agent if the copy running is the one inside this folder.
    ///
    /// The package ships tools\archiving-scan-agent.exe for staff to download, and on a machine
    /// where someone has run it from there it holds its own file open — the second thing, after
    /// the service, that a re-run of setup collides with.
    ///
    /// Killed rather than asked politely: the agent hides to the tray when its window is closed,
    /// by design, so CloseMainWindow would hide it and leave the file locked. It has no unsaved
    /// state — it is a bridge to a scanner, not a document.
    /// </summary>
    static int StopScanAgentIn(string target)
    {
        string full = Canonical(target);
        int stopped = 0;

        foreach (Process p in Process.GetProcessesByName("archiving-scan-agent"))
        {
            try
            {
                string path = null;
                try { path = p.MainModule == null ? null : p.MainModule.FileName; }
                catch { /* another user's process: the path is not readable from here */ }

                // A copy running from somewhere else — a staff member's own install under their
                // profile — does not hold this folder's file open, so it is left running. A path
                // that cannot be read is treated as ours: the process name is this product's, and
                // the only file at stake is the one being replaced.
                if (path != null && !Canonical(path).StartsWith(full, StringComparison.OrdinalIgnoreCase)) continue;

                p.Kill();
                p.WaitForExit(8000);
                stopped++;
            }
            catch { /* already gone, or not ours to stop */ }
        }

        if (stopped > 0) Thread.Sleep(800);   // handles close with the process, not with the kill
        return stopped;
    }

    /// <summary>Run a tool and hand back its exit code, with nothing shown to the user.</summary>
    static int Run(string exe, string arguments)
    {
        ProcessStartInfo psi = new ProcessStartInfo(exe, arguments);
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        using (Process p = Process.Start(psi))
        {
            // Both streams are drained: sc.exe writes little, but a full pipe buffer would hang it.
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            if (!p.WaitForExit(60000)) return -1;
            return p.ExitCode;
        }
    }

    static int Sc(string arguments) { return Run("sc.exe", arguments); }

    static string Q(string value) { return "\"" + value + "\""; }

    /// <summary>
    /// A path for sc.exe's binPath=, carrying the quotes Windows has to keep.
    ///
    /// sc.exe strips the quotes it is handed, so binPath= "C:\Program Files\Archiving\Archiving.Api.exe"
    /// is stored as an unquoted ImagePath — which Windows reads as C:\Program with the rest as
    /// arguments, and the service never starts. Escaping them makes the quotes part of the value, so
    /// they reach the registry; install-service.ps1 does the same, for the same reason.
    /// </summary>
    static string QuotedPath(string path) { return "\"\\\"" + path + "\\\"\""; }

    /// <summary>
    /// Register the Archiving Windows Service on this machine, pointed at the folder just unpacked.
    ///
    /// Setup used to leave a machine with files and no service: the service was created only by
    /// install-service.ps1, which runs from the database wizard. Anyone who unticked the wizard
    /// box, or who deployed with /S across several servers, was left with a folder that looks like
    /// an installation and a machine with nothing installed on it — nothing in services.msc,
    /// nothing that survives a reboot. So the service is created here, as part of setup, on every
    /// path through it.
    ///
    /// It is registered and left stopped, not started: on a fresh install there is no
    /// appsettings.Production.json yet, so the API has no database to reach and starting it would
    /// only write a failure to the event log. The wizard writes the settings, reconfigures this
    /// same service (its "service exists" branch) and starts it.
    /// </summary>
    /// <returns>A line for the log; never null.</returns>
    static string EnsureServiceInstalled(string target)
    {
        string exe = Path.Combine(target, "Archiving.Api.exe");
        if (!File.Exists(exe)) return "service not registered: Archiving.Api.exe is not in " + target;

        // A service already registered against this folder is the wizard's to reconfigure, not ours
        // to recreate; one registered elsewhere on this machine is another installation, and taking
        // its name would point it at files that are not its own.
        string existing = ServiceFolder();
        if (existing != null)
        {
            return string.Equals(existing, Canonical(target), StringComparison.OrdinalIgnoreCase)
                ? "service '" + ServiceName + "' already registered here"
                : "service '" + ServiceName + "' belongs to another installation (" + existing + ") — left as it is";
        }

        int rc = Sc("create " + ServiceName +
                    " binPath= " + QuotedPath(exe) +
                    // delayed-auto for the reason install-service.ps1 gives: it puts the start after
                    // the boot burst, which is when the database engine finishes its own startup.
                    " start= delayed-auto" +
                    " obj= " + Q(ServiceAccount) +
                    " DisplayName= " + Q(ServiceDisplayName));
        if (rc != 0) return "could not register the service (sc create returned " + rc + ")";

        Sc("description " + ServiceName + " " + Q(ServiceDescription));
        // Restart on failure rather than leaving the site down until someone notices. The third
        // action also covers every later failure, so this keeps retrying instead of giving up.
        Sc("failure " + ServiceName + " reset= 86400 actions= restart/5000/restart/10000/restart/30000");
        // Without this flag Windows runs those actions only when the process dies *unexpectedly*,
        // and a service that fails on its own terms and exits cleanly is left stopped.
        Sc("failureflag " + ServiceName + " 1");

        // The account the service runs as has to be able to read the folder it runs from. Granted
        // here so that a files-only install leaves a service that can actually start.
        Run("icacls.exe", Q(target) + " /grant " + Q(ServiceAccount + ":(OI)(CI)RX") + " /T /C /Q");

        return "service '" + ServiceName + "' registered (delayed-auto, " + ServiceAccount + ")";
    }

    static void StartService()
    {
        try
        {
            ServiceController sc = new ServiceController(ServiceName);
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60));
        }
        catch { /* the wizard starts it too; a failure here is not worth stopping the install for */ }
    }

    /// <summary>Extract one entry, waiting out a handle that has not been released yet.</summary>
    static void ExtractWithRetry(ZipArchiveEntry entry, string dest)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { entry.ExtractToFile(dest, true); return; }
            catch (IOException) { if (attempt >= 4) throw; Thread.Sleep(700); }
            catch (UnauthorizedAccessException) { if (attempt >= 4) throw; Thread.Sleep(700); }
        }
    }

    /// <summary>
    /// Unpack the payload. <paramref name="progress"/> and <paramref name="status"/> may be null
    /// (silent mode).
    /// </summary>
    public static void ExtractTo(string target, Action<int, int> progress, Action<string> status)
    {
        Directory.CreateDirectory(target);
        string settingsPath = Path.Combine(target, "appsettings.Production.json");
        int unpacked = 0;

        if (status != null) status("فحص التنصيب القائم…");
        bool stopped = StopServiceIn(target);
        if (stopped && status != null) status("تم إيقاف الخدمة لتحديث الملفات…");

        int agents = StopScanAgentIn(target);
        if (agents > 0 && status != null) status("تم إغلاق برنامج المسح الضوئي…");

        using (Stream payload = OpenPayload())
        using (ZipArchive zip = new ZipArchive(payload, ZipArchiveMode.Read))
        {
            int total = zip.Entries.Count, done = 0;
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                string dest = Path.Combine(target, entry.FullName);
                // A directory entry has an empty name; create it and move on.
                if (entry.Name.Length == 0)
                {
                    Directory.CreateDirectory(dest);
                }
                else
                {
                    // Never overwrite this install's own settings — that file is written by the
                    // wizard and holds the database credentials and the key the stored documents
                    // are encrypted with, neither of which a package can bring with it.
                    bool isSettings = string.Equals(Path.GetFullPath(dest),
                        Path.GetFullPath(settingsPath), StringComparison.OrdinalIgnoreCase);
                    if (!isSettings)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        ExtractWithRetry(entry, dest);
                    }
                }

                done++;
                if (progress != null && (done % 10 == 0 || done == total)) progress(done, total);
            }

            unpacked = done;
        }

        // Registered after the files are in place: the service points at Archiving.Api.exe, and
        // Windows will not accept a path that is not there yet.
        if (status != null) status("تسجيل خدمة الأرشفة…");
        string service;
        try { service = EnsureServiceInstalled(target); }
        catch (Exception ex) { service = "could not register the service: " + ex.Message; }

        // A note of what happened, left in the folder. When someone says "it ran but nothing
        // came out", this is what settles where the files actually went.
        try
        {
            File.WriteAllText(Path.Combine(target, "setup.log"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  unpacked " + unpacked +
                " entries into " + target +
                (stopped ? "  (service stopped for the update)" : "") +
                (agents > 0 ? "  (scan agent closed: " + agents + ")" : "") + Environment.NewLine +
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + service + Environment.NewLine);
        }
        catch { }

        // Put back what we took down. The wizard restarts it as well when it runs, but nobody
        // should be left with a stopped service because they unticked that box.
        if (stopped)
        {
            if (status != null) status("إعادة تشغيل الخدمة…");
            StartService();
        }
    }

    public static Stream OpenPayload()
    {
        Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadResource);
        if (s == null) throw new InvalidOperationException(
            "هذا الملف لا يحوي حزمة التنصيب — أعد بناءه بـ build-setup.ps1.");
        return s;
    }

    public const string DefaultPath = DefaultFolder;
}

class SetupForm : Form
{
    TextBox _path;
    Button _browse, _install, _cancel;
    ProgressBar _bar;
    Label _status;
    CheckBox _configure;

    public SetupForm()
    {
        Text = "تنصيب نظام الأرشفة — الديوان الإلكتروني";
        ClientSize = new Size(620, 300);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = new Font("Segoe UI", 10f);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { /* the icon is cosmetic; never let it stop the install */ }

        Label title = new Label();
        title.Text = "الديوان الإلكتروني — نظام إدارة الوثائق والأرشفة";
        title.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
        title.SetBounds(20, 18, 580, 30);
        Controls.Add(title);

        Label sub = new Label();
        sub.Text = "سيُفكّ الملفّ في المجلد التالي، ثم تُفتح نافذة إعداد الاتصال بـ SQL Server.";
        sub.ForeColor = Color.DimGray;
        sub.SetBounds(20, 50, 580, 24);
        Controls.Add(sub);

        Label lbl = new Label();
        lbl.Text = "مجلد التنصيب";
        lbl.SetBounds(20, 96, 110, 24);
        lbl.TextAlign = ContentAlignment.MiddleRight;
        Controls.Add(lbl);

        _path = new TextBox();
        _path.Text = Setup.DefaultPath;
        _path.SetBounds(140, 94, 350, 26);
        Controls.Add(_path);

        _browse = new Button();
        _browse.Text = "استعراض…";
        _browse.SetBounds(500, 92, 100, 28);
        _browse.Click += OnBrowse;
        Controls.Add(_browse);

        _configure = new CheckBox();
        _configure.Text = "متابعة إلى إعداد قاعدة البيانات بعد فك الملفات";
        _configure.Checked = true;
        _configure.SetBounds(140, 130, 460, 24);
        Controls.Add(_configure);

        _bar = new ProgressBar();
        _bar.SetBounds(20, 176, 580, 22);
        Controls.Add(_bar);

        _status = new Label();
        _status.SetBounds(20, 202, 580, 24);
        _status.ForeColor = Color.DimGray;
        Controls.Add(_status);

        _install = new Button();
        _install.Text = "تنصيب";
        _install.SetBounds(500, 244, 100, 34);
        _install.BackColor = Color.FromArgb(0, 90, 158);
        _install.ForeColor = Color.White;
        _install.FlatStyle = FlatStyle.Flat;
        _install.Click += OnInstall;
        Controls.Add(_install);
        AcceptButton = _install;

        _cancel = new Button();
        _cancel.Text = "إلغاء";
        _cancel.SetBounds(20, 244, 100, 34);
        _cancel.Click += delegate { Close(); };
        Controls.Add(_cancel);
    }

    void OnBrowse(object sender, EventArgs e)
    {
        FolderBrowserDialog d = new FolderBrowserDialog();
        d.Description = "اختر مجلد التنصيب — يُفضَّل قرص غير قرص النظام";
        if (d.ShowDialog() == DialogResult.OK) _path.Text = d.SelectedPath;
    }

    void OnInstall(object sender, EventArgs e)
    {
        string target = _path.Text.Trim();
        if (target.Length == 0) { Warn("أدخل مجلد التنصيب."); return; }

        // An existing installation is updated, not silently overwritten without warning: its
        // settings file carries the database credentials and the key the documents are encrypted
        // with, and losing that loses the archive.
        string settings = Path.Combine(target, "appsettings.Production.json");
        if (File.Exists(settings))
        {
            DialogResult r = MessageBox.Show(
                "يوجد تنصيب سابق في هذا المجلد.\n\n" +
                "سيتم تحديث الملفات مع الإبقاء على إعداداتك (appsettings.Production.json).\n\n" +
                "المتابعة؟", "تنصيب سابق", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
        }

        _install.Enabled = _browse.Enabled = _cancel.Enabled = _path.Enabled = false;
        Thread t = new Thread(new ThreadStart(delegate { Extract(target); }));
        t.IsBackground = true;
        t.Start();
    }

    void Extract(string target)
    {
        try
        {
            // Same routine the silent switch uses, so the window and /S cannot drift apart.
            Setup.ExtractTo(target, delegate(int done, int total)
            {
                Invoke((MethodInvoker)delegate
                {
                    _bar.Maximum = total;
                    _bar.Value = done;
                    _status.Text = "فكّ الملفات… " + done + " / " + total;
                });
            },
            delegate(string text)
            {
                Invoke((MethodInvoker)delegate { _status.Text = text; });
            });

            Invoke((MethodInvoker)delegate { Done(target); });
        }
        catch (Exception ex)
        {
            string msg = ex.Message;
            // A locked file is the one failure with a specific, actionable cause: something is
            // still holding the installation open. Say which, instead of only echoing the HRESULT.
            if (ex is IOException || ex is UnauthorizedAccessException)
                msg += "\n\nملف قيد الاستخدام. أغلق أي نافذة مفتوحة على مجلد التنصيب، ثم أوقف ما يمسك بالملفات:" +
                       "\n    sc stop Archiving" +
                       "\n    taskkill /IM archiving-scan-agent.exe /F" +
                       "\nوأعد المحاولة.";
            Invoke((MethodInvoker)delegate
            {
                _status.Text = "";
                Warn("تعذّر فكّ الملفات:\n\n" + msg);
                _install.Enabled = _browse.Enabled = _cancel.Enabled = _path.Enabled = true;
            });
        }
    }

    void Done(string target)
    {
        int files = 0;
        try { files = Directory.GetFiles(target, "*", SearchOption.AllDirectories).Length; }
        catch { }
        _status.Text = "تم فكّ " + files + " ملفًا في " + target;

        if (_configure.Checked)
        {
            string gui = Path.Combine(target, "install-gui.ps1");
            if (File.Exists(gui))
            {
                // Setup already runs elevated (see app.manifest), so the wizard inherits it and
                // there is no second UAC prompt.
                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -File \"" + gui + "\"");
                psi.WorkingDirectory = target;
                psi.UseShellExecute = false;
                Process.Start(psi);
                Close();
                return;
            }
            Warn("لم يُعثر على install-gui.ps1 في " + target);
        }

        // Open the folder rather than only naming it: the commonest confusion is looking for the
        // files next to the setup file, when they went to the folder chosen in the box above.
        MessageBox.Show("تم فكّ " + files + " ملفًا في:\n" + target +
            "\n\nوسُجّلت خدمة Archiving في خدمات ويندوز، وهي متوقفة إلى حين إعداد قاعدة البيانات." +
            "\n\nلإكمال التنصيب وتشغيل الخدمة شغّل Install.cmd من هذا المجلد.",
            "اكتمل", MessageBoxButtons.OK, MessageBoxIcon.Information);
        try { Process.Start("explorer.exe", "\"" + target + "\""); } catch { }
        Close();
    }

    void Warn(string text)
    {
        MessageBox.Show(text, "تنصيب", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
