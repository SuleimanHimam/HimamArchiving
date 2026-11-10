using System.Diagnostics;
using System.Drawing;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Text;
using System.Windows.Forms;

/// <summary>The agent's window and tray icon.
///
/// The agent spends its life idle in the background, so the tray icon is the primary surface and this
/// window is the detail view behind it. Closing the window therefore hides it rather than exiting —
/// quitting is an explicit choice in the tray menu, because a closed window that silently killed
/// scanning would look like the scanner had broken.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class AgentForm : Form
{
    private const int MaxLogLines = 300;

    private readonly AgentServer _server;
    private readonly AgentSettings _settings;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _trayStartup;
    private readonly ToolStripMenuItem _trayMock;

    private readonly Label _statusDot = new();
    private readonly Label _statusText = new();
    private readonly LinkLabel _endpoint = new();
    private readonly Label _sessionLine = new();
    private readonly Panel _pairing = new();
    private readonly TextBox _appUrl = new();
    private readonly Button _pair = new();
    private readonly Button _copyToken = new();
    private readonly LinkLabel _newToken = new();
    private readonly ListBox _scanners = new();
    private readonly ListBox _printers = new();
    private readonly CheckBox _runAtLogin = new();
    private readonly Label _installState = new();
    private readonly Button _install = new();
    private readonly LinkLabel _uninstall = new();
    private readonly ToolTip _installTip = new();
    private readonly CheckBox _mock = new();
    private readonly Button _refresh = new();
    private readonly Button _testScan = new();
    private readonly ListBox _log = new();

    private Icon? _currentIcon;
    private bool _startHidden;
    private bool _reallyExit;
    private bool _warnedAboutTray;
    private bool _suppressToggleEvents;

    public AgentForm(AgentServer server, AgentSettings settings, bool startHidden)
    {
        _server = server;
        _settings = settings;
        _startHidden = startHidden;

        Text = "وكيل المسح الضوئي — نظام الأرشفة";
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = new Font("Segoe UI", 9.75f);
        ClientSize = new Size(560, 620);
        MinimumSize = new Size(460, 520);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;

        _trayStartup = new ToolStripMenuItem("التشغيل مع بدء Windows") { CheckOnClick = true };
        _trayMock = new ToolStripMenuItem("وضع المحاكاة (بدون ماسح)") { CheckOnClick = true };

        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };
        menu.Items.Add(new ToolStripMenuItem("فتح النافذة", null, (_, _) => ShowWindow()) { Font = new Font(Font, FontStyle.Bold) });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_trayStartup);
        menu.Items.Add(_trayMock);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("إنهاء الوكيل", null, (_, _) => ExitApp()));

        _tray = new NotifyIcon
        {
            Text = "وكيل المسح الضوئي",
            ContextMenuStrip = menu,
            Visible = true,
        };
        // Single left-click as well as double: a tray icon that ignores a plain click reads as broken,
        // and it is how most background apps behave. Filtered to the left button so right-click still
        // belongs to the context menu. ShowWindow is idempotent, so the click+click+double-click burst
        // a real double-click produces is harmless.
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowWindow(); };
        _tray.DoubleClick += (_, _) => ShowWindow();

        BuildLayout();
        WireEvents();

        _server.Logged += OnServerLog;
        _server.PreviewRequested = ShowPreview;
    }

    /// <summary>Runs the review dialog for a scan that asked for `preview`. Called from a request
    /// worker thread, so it marshals to the UI thread and blocks there until the user decides —
    /// the HTTP response cannot be written before that answer exists.</summary>
    private List<byte[]>? ShowPreview(List<byte[]> pages, ScanOptions options)
    {
        if (InvokeRequired) return (List<byte[]>?)Invoke(() => ShowPreview(pages, options));

        // Re-scanning from inside the dialog goes straight to the scanner rather than back through
        // HTTP: the request that opened this dialog is still occupying the caller's connection.
        List<byte[]> Rescan(ScanOptions o) => _server.MockMode ? Mock.SamplePages(o) : Wia.ScanPages(o);

        ShowWindow();                       // the dialog must have a visible owner to sit over
        using var dialog = new PreviewDialog(pages, options, Rescan);
        dialog.ShowDialog(this);
        return dialog.Result;
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(14),
            BackColor = Color.White,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // status
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // pairing (shared hosts only)
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); // devices
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // options
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // buttons
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // log

        root.Controls.Add(BuildStatusPanel());
        root.Controls.Add(BuildPairingPanel());
        root.Controls.Add(BuildDevicesPanel());
        root.Controls.Add(BuildOptionsPanel());
        root.Controls.Add(BuildButtonsPanel());
        root.Controls.Add(BuildLogPanel());
        Controls.Add(root);
    }

    private Control BuildStatusPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Height = 66, BackColor = Color.FromArgb(0xF6, 0xF8, 0xFA), Padding = new Padding(12, 10, 12, 10) };

        // A coloured bullet plus a word — never colour alone, so the state survives a
        // colour-blind reader or a greyscale remote session.
        _statusDot.AutoSize = true;
        _statusDot.Text = "●";
        _statusDot.Font = new Font("Segoe UI", 14f);
        _statusDot.Location = new Point(0, 0);

        _statusText.AutoSize = true;
        _statusText.Font = new Font("Segoe UI", 11f, FontStyle.Bold);

        _endpoint.AutoSize = true;
        _endpoint.Font = new Font("Segoe UI", 9f);
        _endpoint.LinkColor = Color.FromArgb(0x24, 0x75, 0x9F);
        _endpoint.Click += (_, _) => OpenInBrowser(_server.Prefix + "status");

        // On a Remote Desktop host several agents run side by side on one machine, so "which one am
        // I looking at" stops being obvious the moment there is more than one window open.
        _sessionLine.AutoSize = true;
        _sessionLine.Font = new Font("Segoe UI", 9f);
        _sessionLine.ForeColor = Color.FromArgb(0x4A, 0x4A, 0x63);
        _sessionLine.Text = Session.Describe();

        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
        var line1 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0) };
        line1.Controls.Add(_statusDot);
        line1.Controls.Add(_statusText);
        stack.Controls.Add(line1);
        stack.Controls.Add(_endpoint);
        stack.Controls.Add(_sessionLine);
        panel.Controls.Add(stack);
        panel.Height = 86;
        return panel;
    }

    /// <summary>
    /// Pairing — shown only on a machine the agent is sharing with other sessions.
    ///
    /// The problem it solves is specific to Remote Desktop. Every session on the host shares one
    /// 127.0.0.1, so a browser cannot tell its own session's agent from a colleague's by address
    /// alone, and the agent cannot tell which session a request came from. The token settles both:
    /// the browser that holds it is this user's, and it is handed over by opening the web app from
    /// *inside this session*, which lands it in this user's own browser profile.
    ///
    /// On an ordinary single-user PC the whole panel stays hidden — there is nothing to disambiguate
    /// and nothing to protect against, and the agent asks for no token at all.
    /// </summary>
    private Control BuildPairingPanel()
    {
        _pairing.Dock = DockStyle.Fill;
        _pairing.AutoSize = true;
        _pairing.Visible = false;                 // OnLoad decides, once the server has bound a port
        _pairing.BackColor = Color.FromArgb(0xFF, 0xF8, 0xE6);
        _pairing.Padding = new Padding(12, 10, 12, 10);
        _pairing.Margin = new Padding(0, 10, 0, 0);

        var stack = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoSize = true,
        };

        var title = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Text = "جهاز مشترك — يلزم ربط المتصفح",
        };
        var hint = new Label
        {
            AutoSize = true, MaximumSize = new Size(520, 0),
            ForeColor = Color.FromArgb(0x4A, 0x4A, 0x63),
            Text = "هذا الخادم يشغّل أكثر من جلسة، لذا يجب أن يعرف متصفحك أي وكيل يخصّك. "
                 + "اكتب عنوان النظام ثم اضغط «ربط المتصفح» — ستُفتح صفحة تحفظ الرمز في هذا المتصفح مرة واحدة.",
        };

        _appUrl.Width = 320;
        _appUrl.Font = new Font("Segoe UI", 9f);
        _appUrl.PlaceholderText = "https://archive.example.com";

        _pair.Text = "ربط المتصفح";
        _pair.AutoSize = true;
        _pair.Padding = new Padding(8, 4, 8, 4);
        _pair.Click += (_, _) => PairBrowser();

        _copyToken.Text = "نسخ الرمز";
        _copyToken.AutoSize = true;
        _copyToken.Padding = new Padding(8, 4, 8, 4);
        _copyToken.Click += (_, _) => CopyToken();

        _newToken.Text = "إصدار رمز جديد";
        _newToken.AutoSize = true;
        _newToken.Font = new Font("Segoe UI", 9f);
        _newToken.LinkColor = Color.FromArgb(0x24, 0x75, 0x9F);
        _newToken.Margin = new Padding(0, 8, 0, 0);
        _newToken.Click += (_, _) => ResetToken();

        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            AutoSize = true, Margin = new Padding(0, 8, 0, 0),
        };
        row.Controls.Add(_appUrl);
        row.Controls.Add(_pair);
        row.Controls.Add(_copyToken);

        stack.Controls.Add(title);
        stack.Controls.Add(hint);
        stack.Controls.Add(row);
        stack.Controls.Add(_newToken);
        _pairing.Controls.Add(stack);
        return _pairing;
    }

    /// <summary>Opens the web app's pairing page in this session's default browser, which is the
    /// point of the exercise: the browser Windows launches here belongs to this user's profile, so
    /// the token it stores can only ever be read back by this user's own pages.</summary>
    private void PairBrowser()
    {
        _settings.SetAppUrl(_appUrl.Text);
        if (string.IsNullOrWhiteSpace(_settings.AppUrl))
        {
            Warn("اكتب عنوان النظام أولاً، مثل https://archive.example.com", "لم يُضبط العنوان");
            return;
        }

        OpenInBrowser(_settings.PairingUrl);
        _settings.Paired = true;
        _settings.Save();
        AppendLog($"فُتحت صفحة الاقتران: {_settings.AppUrl}");
    }

    /// <summary>The manual route, for the browser that will not open from here — a locked-down
    /// session, or a second browser the user prefers. They paste it into the scanner settings page.</summary>
    private void CopyToken()
    {
        try
        {
            Clipboard.SetText(_server.Token);
            AppendLog("نُسخ رمز الاقتران إلى الحافظة");
        }
        catch (Exception ex) { Warn(ex.Message, "تعذّر النسخ"); }
    }

    private void ResetToken()
    {
        var answer = MessageBox.Show(this,
            "إصدار رمز جديد سيقطع الارتباط بكل المتصفحات المرتبطة حالياً، وستحتاج إلى الضغط على «ربط المتصفح» من جديد.\n\nالمتابعة؟",
            "رمز اقتران جديد", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2, Rtl);
        if (answer != DialogResult.Yes) return;

        _settings.ResetToken();
        AppendLog("صدر رمز اقتران جديد — أعِد ربط المتصفح");
    }

    private Control BuildDevicesPanel()
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = new Padding(0, 10, 0, 0) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        grid.Controls.Add(SectionLabel("الماسحات الضوئية"), 0, 0);
        grid.Controls.Add(SectionLabel("الطابعات"), 1, 0);

        foreach (var (list, col) in new[] { (_scanners, 0), (_printers, 1) })
        {
            list.Dock = DockStyle.Fill;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.IntegralHeight = false;
            list.Margin = new Padding(col == 0 ? 0 : 6, 2, col == 0 ? 6 : 0, 0);
            grid.Controls.Add(list, col, 1);
        }
        return grid;
    }

    private Control BuildOptionsPanel()
    {
        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0, 12, 0, 0) };

        _runAtLogin.Text = "تشغيل الوكيل تلقائياً عند بدء تشغيل Windows";
        _runAtLogin.AutoSize = true;

        _mock.Text = "وضع المحاكاة — يُرجع صفحة تجريبية بدون ماسح ضوئي";
        _mock.AutoSize = true;

        stack.Controls.Add(_runAtLogin);
        stack.Controls.Add(_mock);
        stack.Controls.Add(BuildInstallRow());
        return stack;
    }

    /// <summary>
    /// Installing the agent as a program.
    ///
    /// This sits with the settings rather than behind a menu because of where the file comes from:
    /// people download the agent and run it out of Downloads, a folder they empty, and until it is
    /// installed there is no Start-menu entry to find it by again. Showing the state in words —
    /// where it is installed, or that it is not — is the whole point; a bare button would leave the
    /// user guessing which of the two copies on their disk is the one Windows starts.
    /// </summary>
    private Control BuildInstallRow()
    {
        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true,
            Margin = new Padding(0, 10, 0, 0),
        };

        _install.AutoSize = true;
        _install.Padding = new Padding(8, 4, 8, 4);
        _install.Click += (_, _) => ToggleInstall();

        _installState.AutoSize = true;
        _installState.Margin = new Padding(8, 8, 0, 0);
        _installState.ForeColor = Color.FromArgb(0x4A, 0x4A, 0x63);

        // Removal stays reachable in the states where it is no longer the button's job, but as a link
        // rather than a second button: nobody opens this window in order to delete the program, and a
        // copy running from the wrong folder in particular must not make deletion the easy click.
        _uninstall.Text = "إزالة التثبيت";
        _uninstall.AutoSize = true;
        _uninstall.Visible = false;
        _uninstall.Font = new Font("Segoe UI", 9f);
        _uninstall.LinkColor = Color.FromArgb(0x24, 0x75, 0x9F);
        _uninstall.Margin = new Padding(8, 9, 0, 0);
        _uninstall.Click += (_, _) => Uninstall();

        row.Controls.Add(_install);
        row.Controls.Add(_installState);
        row.Controls.Add(_uninstall);
        return row;
    }

    /// <summary>Reflects where the program actually lives, so the button never claims to do
    /// something the state does not support.</summary>
    private void RefreshInstallState()
    {
        _uninstall.Visible = false;

        if (!Install.IsInstalled)
        {
            _install.Text = "تثبيت البرنامج على هذا الجهاز";
            _installState.Text = "غير مثبّت — يعمل من الملف الذي نزّلته";
        }
        else if (Install.NeedsUpdate)
        {
            _install.Text = "تحديث النسخة المثبّتة";
            _installState.Text = "النسخة المثبّتة أقدم من هذه";
            _uninstall.Visible = true;
        }
        else if (!Install.RunningInstalled)
        {
            // The running file is not the installed one - an old download opened again out of
            // Downloads, or a second copy kept in a folder somewhere. Uninstalling is the last thing
            // this state wants: it would delete the good copy on the strength of a button that merely
            // happens to sit here. The useful move is to hand over to the installed copy, so that is
            // what the button does, and removal drops back to the link beside it.
            _install.Text = "تشغيل النسخة المثبّتة";
            _installState.Text = "مثبّت — لكنك تشغّل نسخة أخرى من مجلد آخر";
            _uninstall.Visible = true;
        }
        else
        {
            _install.Text = "إزالة التثبيت";
            _installState.Text = "مثبّت — اختصار في قائمة ابدأ ويبدأ مع Windows";
        }

        // Which two copies are meant is exactly what the sentence cannot say without running long.
        _installTip.SetToolTip(_installState, Install.IsInstalled
            ? $"مجلد التثبيت: {Install.Directory}\nالنسخة العاملة الآن: {Startup.ExecutablePath}"
            : $"النسخة العاملة الآن: {Startup.ExecutablePath}");
    }

    /// <summary>The button's one click, routed to whatever the current state actually needs.</summary>
    private void ToggleInstall()
    {
        if (!Install.IsInstalled || Install.NeedsUpdate) InstallOrUpdate();
        else if (!Install.RunningInstalled) SwitchToInstalled(justInstalled: false);
        else Uninstall();
    }

    private void InstallOrUpdate()
    {
        var updating = Install.IsInstalled;
        try
        {
            Install.Run(desktopShortcut: true, runAtLogin: true);
            AppendLog(updating
                ? $"تم تحديث النسخة المثبّتة في {Install.Directory}"
                : $"تم تثبيت البرنامج في {Install.Directory}");
        }
        catch (Exception ex)
        {
            Warn(ex.Message, "تعذّر إكمال العملية");
            AfterInstallChange();
            return;
        }

        AfterInstallChange();

        // Carry on in the copy we just wrote rather than in the one that wrote it. Without this the
        // window announces a successful install and in the same breath reports that you are running
        // some other copy - and the copy it means is the download, the file people delete.
        if (!Install.RunningInstalled)
        {
            var title = updating ? "تم التحديث" : "تم التثبيت";
            MessageBox.Show(this,
                $"{title} في:\n{Install.Directory}\n\nسيُعاد تشغيل الوكيل من هناك الآن.",
                title, MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, Rtl);
            SwitchToInstalled(justInstalled: true);
        }
    }

    /// <summary>
    /// Closes this copy and starts the installed one in its place.
    ///
    /// Two processes cannot both be this session's agent, so it is a hand-over rather than a launch:
    /// <see cref="Program.HandOverTo"/> gives up the single-instance lock first, and this window goes
    /// away only once something has actually started in its place.
    /// </summary>
    private void SwitchToInstalled(bool justInstalled)
    {
        if (!justInstalled)
        {
            var answer = MessageBox.Show(this,
                $"سيُغلق هذا الوكيل وتُفتح النسخة المثبّتة في:\n{Install.Directory}\n\nالمتابعة؟",
                "تشغيل النسخة المثبّتة", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1, Rtl);
            if (answer != DialogResult.Yes) return;
        }

        if (!Program.HandOverTo(Install.InstalledExe))
        {
            Warn($"تعذّر تشغيل النسخة المثبّتة من:\n{Install.InstalledExe}", "تعذّر التحويل");
            return;
        }

        AppendLog("جارٍ التحويل إلى النسخة المثبّتة…");
        ExitApp();
    }

    private void Uninstall()
    {
        // Removing an install you are not standing in is a different act from removing the one you
        // are, and the question has to name the folder that is about to go: in the two-copies state
        // the user otherwise cannot tell which of them they are agreeing to lose.
        var confirm = MessageBox.Show(this,
            Install.RunningInstalled
                ? "إزالة البرنامج من هذا الجهاز؟ سيبقى الوكيل الحالي يعمل حتى تُغلقه."
                : $"إزالة النسخة المثبّتة في:\n{Install.Directory}\n\nستبقى النسخة التي تشغّلها الآن كما هي.",
            "إزالة التثبيت", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2, Rtl);
        if (confirm != DialogResult.Yes) return;

        try
        {
            // The files go only when we are not standing on them.
            Install.Remove(deleteFiles: !Install.RunningInstalled);
            AppendLog("تمت إزالة تثبيت البرنامج");
        }
        catch (Exception ex) { Warn(ex.Message, "تعذّر إكمال العملية"); }
        AfterInstallChange();
    }

    /// <summary>Installing, updating and removing all change what the login entry points at, so the
    /// state text and both run-at-login toggles are re-read from Windows rather than assumed.</summary>
    private void AfterInstallChange()
    {
        RefreshInstallState();
        SyncToggles(startup: Startup.IsEnabled(), mock: null);
    }

    /// <summary>
    /// Offered once, the first time the agent runs from somewhere other than its install folder.
    ///
    /// Asked rather than done: copying yourself into a user's profile and adding yourself to their
    /// login is exactly the shape of behaviour they are right to distrust when it happens silently.
    /// The answer is remembered either way so this is never a second interruption.
    /// </summary>
    private void OfferInstall()
    {
        if (Install.IsInstalled || _startHidden) return;
        const string key = @"Software\ArchivingScanAgent";
        try
        {
            using (var read = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(key))
                if (read?.GetValue("InstallOffered") is not null) return;
            using (var write = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(key))
                write?.SetValue("InstallOffered", 1);
        }
        catch { return; }   // cannot remember the answer, so do not ask a question we would repeat

        var answer = MessageBox.Show(this,
            "تثبيت وكيل المسح الضوئي على هذا الجهاز؟\n\n"
            + "سيُنسخ إلى مجلد ثابت، ويُضاف اختصار إلى قائمة ابدأ وسطح المكتب، "
            + "وسيبدأ تلقائياً مع تشغيل Windows.",
            "تثبيت البرنامج", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1, Rtl);
        if (answer == DialogResult.Yes) ToggleInstall();
    }

    private const MessageBoxOptions Rtl = MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign;

    private Control BuildButtonsPanel()
    {
        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0, 10, 0, 0) };

        _refresh.Text = "تحديث الأجهزة";
        _refresh.AutoSize = true;
        _refresh.Padding = new Padding(8, 4, 8, 4);

        _testScan.Text = "اختبار المسح";
        _testScan.AutoSize = true;
        _testScan.Padding = new Padding(8, 4, 8, 4);

        var hide = new Button { Text = "إخفاء إلى شريط المهام", AutoSize = true, Padding = new Padding(8, 4, 8, 4) };
        hide.Click += (_, _) => HideToTray(announce: false);

        stack.Controls.Add(_refresh);
        stack.Controls.Add(_testScan);
        stack.Controls.Add(hide);
        return stack;
    }

    private Control BuildLogPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 12, 0, 0) };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _log.Dock = DockStyle.Fill;
        _log.BorderStyle = BorderStyle.FixedSingle;
        _log.IntegralHeight = false;
        _log.Font = new Font("Consolas", 8.75f);
        _log.HorizontalScrollbar = true;

        panel.Controls.Add(SectionLabel("سجل النشاط"), 0, 0);
        panel.Controls.Add(_log, 0, 1);
        return panel;
    }

    private static Label SectionLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        ForeColor = Color.FromArgb(0x4A, 0x4A, 0x63),
        Margin = new Padding(0, 0, 0, 4),
    };

    // ── Behaviour ─────────────────────────────────────────────────────────────

    private void WireEvents()
    {
        _refresh.Click += async (_, _) => await RefreshDevicesAsync();
        _testScan.Click += async (_, _) => await TestScanAsync();

        _runAtLogin.CheckedChanged += (_, _) => ToggleStartup(_runAtLogin.Checked);
        _trayStartup.CheckedChanged += (_, _) => ToggleStartup(_trayStartup.Checked);

        _mock.CheckedChanged += (_, _) => ToggleMock(_mock.Checked);
        _trayMock.CheckedChanged += (_, _) => ToggleMock(_trayMock.Checked);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        _suppressToggleEvents = true;
        _runAtLogin.Checked = _trayStartup.Checked = Startup.IsEnabled();
        _mock.Checked = _trayMock.Checked = _server.MockMode;
        _suppressToggleEvents = false;

        RefreshStatus();
        RefreshPairing();

        // A Run entry pointing at an old copy of the exe launches the wrong file at next login and is
        // invisible until then, so repair it now rather than reporting it.
        if (Startup.IsStale())
        {
            try { Startup.SetEnabled(true); AppendLog("تم تحديث مسار بدء التشغيل التلقائي إلى الموقع الحالي للبرنامج"); }
            catch (Exception ex) { AppendLog($"تعذّر تحديث مسار بدء التشغيل: {ex.Message}"); }
        }

        RefreshInstallState();

        await RefreshDevicesAsync();

        if (_startHidden) HideToTray(announce: true);
        else OfferInstall();
    }

    /// <summary>Suppresses the initial show when launched with --background, so signing in to Windows
    /// does not pop a window. Only the first show is blocked — the tray menu can still open it.</summary>
    protected override void SetVisibleCore(bool value)
    {
        if (_startHidden && value && !IsHandleCreated)
        {
            CreateHandle();
            base.SetVisibleCore(false);
            return;
        }
        base.SetVisibleCore(value);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // The X button parks the agent in the tray; only the tray menu, Windows shutting down, or
        // Task Manager actually exits.
        //
        // Note that a bare WM_CLOSE posted by another process reports CloseReason.TaskManagerClosing,
        // not UserClosing — clicking the X goes through WM_SYSCOMMAND/SC_CLOSE and is the only thing
        // that produces UserClosing. Exiting on TaskManagerClosing is intended: "End task" is an
        // explicit kill, and Task Manager terminates the process anyway if this handler cancels.
        var mustExit = _reallyExit
            || e.CloseReason == CloseReason.ApplicationExitCall
            || e.CloseReason == CloseReason.WindowsShutDown
            || e.CloseReason == CloseReason.TaskManagerClosing;

        if (!mustExit)
        {
            e.Cancel = true;
            HideToTray(announce: true);
            return;
        }
        base.OnFormClosing(e);
    }

    private void ShowWindow()
    {
        _startHidden = false;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    private void HideToTray(bool announce)
    {
        // Hide() alone removes the taskbar button, so there is no ShowInTaskbar to toggle here —
        // and assigning it would force WinForms to recreate the window handle, which is worth
        // avoiding on the very form Application.Run is pumping.
        Hide();
        if (announce && !_warnedAboutTray)
        {
            _warnedAboutTray = true;
            _tray.BalloonTipTitle = "وكيل المسح الضوئي يعمل في الخلفية";
            _tray.BalloonTipText = "الوكيل ما زال يعمل. انقر نقراً مزدوجاً على الأيقونة لفتح النافذة.";
            _tray.ShowBalloonTip(4000);
        }
    }

    private void ExitApp()
    {
        _reallyExit = true;
        _tray.Visible = false;
        Application.Exit();
    }

    private void ToggleStartup(bool enabled)
    {
        if (_suppressToggleEvents) return;
        try
        {
            Startup.SetEnabled(enabled);
            AppendLog(enabled
                ? "تم تفعيل التشغيل التلقائي عند بدء Windows"
                : "تم إلغاء التشغيل التلقائي عند بدء Windows");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "تعذّر تغيير إعداد بدء التشغيل",
                MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            enabled = Startup.IsEnabled();   // fall back to what the registry actually says
        }
        SyncToggles(startup: enabled, mock: null);
    }

    private void ToggleMock(bool enabled)
    {
        if (_suppressToggleEvents) return;
        _server.MockMode = enabled;
        AppendLog(enabled ? "تم تفعيل وضع المحاكاة" : "تم إيقاف وضع المحاكاة");
        SyncToggles(startup: null, mock: enabled);
        _ = RefreshDevicesAsync();
    }

    /// <summary>Keeps the window checkboxes and the tray menu items in step. Both edit the same
    /// setting, so each has to reflect a change made through the other without re-firing the handler.</summary>
    private void SyncToggles(bool? startup, bool? mock)
    {
        _suppressToggleEvents = true;
        if (startup is { } s) { _runAtLogin.Checked = s; _trayStartup.Checked = s; }
        if (mock is { } m) { _mock.Checked = m; _trayMock.Checked = m; }
        _suppressToggleEvents = false;
    }

    /// <summary>Shows the pairing panel only where it means something, and — the first time an agent
    /// runs on a shared host with a URL already configured — opens the pairing page unprompted.
    /// Doing it automatically matters more than it looks: without it every user on the server would
    /// have to be told to press a button before scanning worked even once.</summary>
    private void RefreshPairing()
    {
        _pairing.Visible = _server.RequiresToken;
        if (!_server.RequiresToken) return;

        _appUrl.Text = _settings.AppUrl;

        if (!_settings.Paired && !string.IsNullOrWhiteSpace(_settings.AppUrl))
        {
            OpenInBrowser(_settings.PairingUrl);
            _settings.Paired = true;
            _settings.Save();
            AppendLog("فُتحت صفحة الاقتران تلقائياً لأن هذه أول مرة على هذا الخادم");
        }
        else if (string.IsNullOrWhiteSpace(_settings.AppUrl))
        {
            AppendLog("جهاز مشترك: اكتب عنوان النظام في الأعلى ثم اضغط «ربط المتصفح».");
        }
    }

    private void RefreshStatus()
    {
        var running = _server.IsRunning;
        _statusDot.ForeColor = running ? Color.FromArgb(0x2F, 0x85, 0x5A) : Color.FromArgb(0xC5, 0x30, 0x30);
        _statusText.Text = running ? "الوكيل يعمل" : "الوكيل متوقف";
        _endpoint.Text = running ? _server.Prefix + "status" : "غير متصل";
        _endpoint.Enabled = running;

        _currentIcon?.Dispose();
        _currentIcon = AppIcon.Create(running);
        Icon = _currentIcon;
        _tray.Icon = _currentIcon;
        _tray.Text = running
            ? $"وكيل المسح الضوئي — المنفذ {_server.Port} · جلسة {Session.Id}"
            : "وكيل المسح الضوئي — متوقف";
    }

    private async Task RefreshDevicesAsync()
    {
        _refresh.Enabled = false;
        try
        {
            // WIA enumeration blocks on the driver, which can take seconds on a sleeping scanner —
            // off the UI thread so the window does not freeze while it answers.
            var mockMode = _server.MockMode;
            var (scanners, printers) = await Task.Run(() => mockMode
                ? (new[] { "Mock Scanner" }, new[] { "Mock Printer" })
                : (AgentServer.SafeListScanners(), AgentServer.SafeListPrinters()));

            Fill(_scanners, scanners, "لا يوجد ماسح ضوئي متصل");
            Fill(_printers, printers, "لا توجد طابعات مثبتة");
        }
        finally { _refresh.Enabled = true; }
    }

    private static void Fill(ListBox list, string[] items, string emptyMessage)
    {
        list.BeginUpdate();
        list.Items.Clear();
        if (items.Length == 0) list.Items.Add(emptyMessage);
        else foreach (var i in items) list.Items.Add(i);
        list.EndUpdate();
    }

    /// <summary>Exercises the same HTTP endpoint the web app calls, rather than invoking WIA directly —
    /// a test that skipped the listener would pass while the browser still could not reach the agent.</summary>
    private async Task TestScanAsync()
    {
        _testScan.Enabled = false;
        var original = _testScan.Text;
        _testScan.Text = "جارٍ المسح…";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            // The test goes through the listener, so it has to carry the token the listener demands.
            // Otherwise the one button meant to prove the agent works would fail with a 401 on every
            // shared host.
            if (_server.RequiresToken) http.DefaultRequestHeaders.Add("X-Agent-Token", _server.Token);
            var body = new StringContent("{\"format\":\"pdf\"}", Encoding.UTF8, "application/json");
            var res = await http.PostAsync(_server.Prefix + "scan", body);
            var bytes = await res.Content.ReadAsByteArrayAsync();

            if (!res.IsSuccessStatusCode)
            {
                var message = Encoding.UTF8.GetString(bytes);
                AppendLog($"فشل الاختبار: {message}");
                Warn(message, "فشل اختبار المسح");
                return;
            }

            var path = Path.Combine(Path.GetTempPath(), $"archiving-test-scan-{DateTime.Now:HHmmss}.pdf");
            await File.WriteAllBytesAsync(path, bytes);
            AppendLog($"نجح الاختبار — {bytes.Length / 1024} ك.ب، حُفظ في {path}");

            if (MessageBox.Show(this, $"تم المسح بنجاح ({bytes.Length / 1024} ك.ب).\nهل تريد فتح الملف؟",
                    "اختبار المسح", MessageBoxButtons.YesNo, MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign) == DialogResult.Yes)
                OpenInBrowser(path);
        }
        catch (Exception ex)
        {
            AppendLog($"فشل الاختبار: {ex.Message}");
            Warn(ex.Message, "فشل اختبار المسح");
        }
        finally
        {
            _testScan.Text = original;
            _testScan.Enabled = true;
        }
    }

    private void Warn(string message, string title) =>
        MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

    private static void OpenInBrowser(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch { /* no handler registered — nothing useful to do */ }
    }

    // ── Logging ───────────────────────────────────────────────────────────────

    /// <summary>Server events arrive on request-handling threads; marshal before touching the ListBox.</summary>
    private void OnServerLog(string message)
    {
        if (IsDisposed) return;
        try
        {
            if (InvokeRequired) BeginInvoke(() => AppendLog(message));
            else AppendLog(message);
        }
        catch (ObjectDisposedException) { /* shutting down */ }
    }

    private void AppendLog(string message)
    {
        _log.BeginUpdate();
        _log.Items.Add($"{DateTime.Now:HH:mm:ss}  {message}");
        while (_log.Items.Count > MaxLogLines) _log.Items.RemoveAt(0);
        _log.TopIndex = _log.Items.Count - 1;
        _log.EndUpdate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _server.Logged -= OnServerLog;
            _tray.Visible = false;
            _tray.Dispose();
            _installTip.Dispose();
            _currentIcon?.Dispose();
        }
        base.Dispose(disposing);
    }
}
