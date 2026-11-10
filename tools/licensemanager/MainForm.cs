using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace Archiving.LicenseManager;

/// <summary>
/// The main window: what has been issued, and the actions that change that list.
///
/// The list is the centre of the program rather than the issue form, and that ordering is the point.
/// Issuing a licence takes thirty seconds once a year per customer; answering "what does this
/// customer have, and when does it lapse" happens every week, and had no answer at all before.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class MainForm : Form
{
    private readonly Vault _vault;
    private Ledger _ledger;

    private readonly Label _keyState = new();
    private readonly TextBox _search = new();
    private readonly ListView _list = new();
    private readonly Label _summary = new();

    private readonly Button _issue = new();
    private readonly Button _renew = new();
    private readonly Button _save = new();
    private readonly Button _copy = new();
    private readonly Button _revoke = new();

    public MainForm(Vault vault, Ledger ledger)
    {
        _vault = vault;
        _ledger = ledger;

        Text = "تراخيص مراسيل";
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = new Font("Segoe UI", 9.5f);
        ClientSize = new Size(1000, 620);
        MinimumSize = new Size(820, 480);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;

        BuildLayout();
        RefreshKeyState();
        RefreshList();
    }

    // ── layout ───────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(14), BackColor = Color.White,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // key bar
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // actions
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // list
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // summary

        root.Controls.Add(BuildKeyBar());
        root.Controls.Add(BuildActions());
        root.Controls.Add(BuildList());

        _summary.AutoSize = true;
        _summary.ForeColor = Color.FromArgb(0x55, 0x5F, 0x6B);
        _summary.Margin = new Padding(2, 8, 0, 0);
        root.Controls.Add(_summary);

        Controls.Add(root);
    }

    private Control BuildKeyBar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill, AutoSize = true, BackColor = Color.FromArgb(0xF5, 0xF7, 0xF8),
            Padding = new Padding(12, 10, 12, 10),
        };

        _keyState.AutoSize = true;
        _keyState.MaximumSize = new Size(600, 0);

        var choose = Btn("مفتاح التوقيع…", ChooseKey);
        var create = Btn("إنشاء مفتاح…", CreateKey);
        var pub = Btn("نسخ المفتاح العام", CopyPublicKey);
        var ledgerBtn = Btn("مجلد السجل", OpenLedgerFolder);

        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 6, 0, 0) };
        row.Controls.AddRange([choose, create, pub, ledgerBtn]);

        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
        stack.Controls.Add(_keyState);
        stack.Controls.Add(row);
        panel.Controls.Add(stack);
        return panel;
    }

    private Control BuildActions()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false, AutoSize = true, Margin = new Padding(0, 12, 0, 8),
        };

        Style(_issue, "ترخيص جديد…", IssueNew);
        Style(_renew, "تجديد…", RenewSelected);
        Style(_save, "حفظ ملف .lic…", SaveSelected);
        Style(_copy, "نسخ الرمز", CopySelected);
        Style(_revoke, "سحب…", RevokeSelected);
        var inspect = Btn("فحص ترخيص…", InspectSomething);

        _search.Width = 220;
        _search.PlaceholderText = "بحث بالعميل أو البصمة…";
        _search.Margin = new Padding(24, 2, 0, 0);
        _search.TextChanged += (_, _) => RefreshList();

        bar.Controls.AddRange([_issue, _renew, _save, _copy, _revoke, inspect, _search]);
        return bar;
    }

    private Control BuildList()
    {
        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        _list.GridLines = false;
        _list.OwnerDraw = false;

        _list.Columns.Add("العميل", 220);
        _list.Columns.Add("بصمة الجهاز", 190);
        _list.Columns.Add("الإصدار", 100);
        _list.Columns.Add("صدر في", 100);
        _list.Columns.Add("ينتهي", 110);
        _list.Columns.Add("الحالة", 130);
        _list.Columns.Add("المعرّف", 110);

        _list.SelectedIndexChanged += (_, _) => SyncButtons();
        _list.DoubleClick += (_, _) => { if (Selected is not null) ShowDetails(Selected); };
        return _list;
    }

    private Button Btn(string text, Action onClick)
    {
        var b = new Button { AutoSize = true, Padding = new Padding(10, 4, 10, 4), Margin = new Padding(0, 0, 8, 0) };
        Style(b, text, onClick);
        return b;
    }

    private static void Style(Button b, string text, Action onClick)
    {
        b.Text = text;
        b.AutoSize = true;
        b.Padding = new Padding(10, 4, 10, 4);
        b.Margin = new Padding(0, 0, 8, 0);
        b.Click += (_, _) => onClick();
    }

    // ── state ────────────────────────────────────────────────────────────────

    private LedgerEntry? Selected =>
        _list.SelectedItems.Count == 1 ? _list.SelectedItems[0].Tag as LedgerEntry : null;

    private void SyncButtons()
    {
        var s = Selected;
        var hasKey = _vault.KeyAvailable;
        _issue.Enabled = hasKey;
        _renew.Enabled = s is not null && hasKey;
        _save.Enabled = s is not null;
        _copy.Enabled = s is not null;
        _revoke.Enabled = s is not null && s.RevokedAt is null;
    }

    private void RefreshKeyState()
    {
        if (_vault.KeyAvailable)
        {
            _keyState.ForeColor = Color.FromArgb(0x2C, 0x6B, 0x4E);
            _keyState.Text = $"مفتاح التوقيع محمَّل ✓\n{_vault.KeyPath}";
        }
        else
        {
            _keyState.ForeColor = Color.FromArgb(0xA8, 0x2C, 0x22);
            _keyState.Text = _vault.KeyPath is null
                ? "لم يُحدَّد مفتاح توقيع. لا يمكن إصدار تراخيص حتى تختاره — يمكنك تصفّح السجل فقط."
                : $"المفتاح غير موجود في مساره المحفوظ:\n{_vault.KeyPath}\nصِله بالجهاز أو اختر مساره من جديد.";
        }
        SyncButtons();
    }

    private void RefreshList()
    {
        var q = _search.Text.Trim();
        var rows = _ledger.Entries
            .Where(e => q.Length == 0
                     || e.Customer.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || e.MachineId.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || e.Id.Contains(q, StringComparison.OrdinalIgnoreCase))
            // Most recent first: the licence someone asks about is almost always a recent one.
            .OrderByDescending(e => e.IssuedAt)
            .ToList();

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var e in rows)
        {
            var item = new ListViewItem(e.Customer) { Tag = e };
            item.SubItems.Add(e.MachineId);
            item.SubItems.Add(e.Edition);
            item.SubItems.Add(e.IssuedAt.ToLocalTime().ToString("yyyy-MM-dd"));
            item.SubItems.Add(e.ExpiresAt?.ToLocalTime().ToString("yyyy-MM-dd") ?? "دائم");
            item.SubItems.Add(Describe(e));
            item.SubItems.Add(e.Id);
            item.ForeColor = Colour(e.State);
            _list.Items.Add(item);
        }
        _list.EndUpdate();

        var live = _ledger.Entries.Count(e => e.State is LicenceState.Active or LicenceState.Perpetual);
        var soon = _ledger.Entries.Count(e => e.State is LicenceState.Expiring or LicenceState.InGrace);
        _summary.Text = $"الإجمالي {_ledger.Entries.Count} · سارية {live} · تنتهي قريبًا {soon} · السجل: {_ledger.Path}";
        SyncButtons();
    }

    private static string Describe(LedgerEntry e) => e.State switch
    {
        LicenceState.Perpetual => "دائم",
        LicenceState.Active => $"سارٍ — {e.DaysLeft} يومًا",
        LicenceState.Expiring => $"ينتهي خلال {e.DaysLeft} يومًا",
        LicenceState.InGrace => $"في مهلة السماح ({e.GraceDays + e.DaysLeft!.Value} يومًا متبقّية)",
        LicenceState.Expired => "منتهٍ",
        LicenceState.Revoked => "مسحوب",
        _ => "",
    };

    private static Color Colour(LicenceState s) => s switch
    {
        LicenceState.Expiring or LicenceState.InGrace => Color.FromArgb(0x9A, 0x6B, 0x10),
        LicenceState.Expired => Color.FromArgb(0xA8, 0x2C, 0x22),
        LicenceState.Revoked => Color.FromArgb(0x8A, 0x8A, 0x8A),
        _ => Color.FromArgb(0x1A, 0x22, 0x2C),
    };

    // ── actions ──────────────────────────────────────────────────────────────

    private void ChooseKey()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "اختر مفتاح التوقيع",
            Filter = "مفتاح PEM (*.pem)|*.pem|كل الملفات (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var previous = _vault.KeyPath;
        _vault.KeyPath = dlg.FileName;
        try
        {
            using var _ = _vault.OpenKey();   // proves it before it is remembered
        }
        catch (Exception ex)
        {
            _vault.KeyPath = previous;
            Error(ex.Message);
            return;
        }

        _vault.LedgerPath ??= _vault.DefaultLedgerPath;
        _vault.Save();
        ReloadLedger();
        RefreshKeyState();
    }

    private void CreateKey()
    {
        var answer = MessageBox.Show(this,
            "سيُنشأ مفتاح توقيع جديد.\n\n" +
            "استخدم هذا مرّة واحدة فقط عند بدء المنتج. إن كان لديك مفتاح بالفعل فلا تُنشئ غيره: " +
            "كل ترخيص صدر بالمفتاح القديم سيتوقّف عن العمل تحت مفتاح جديد.\n\nالمتابعة؟",
            "إنشاء مفتاح توقيع", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2, Rtl);
        if (answer != DialogResult.Yes) return;

        using var dlg = new FolderBrowserDialog { Description = "أين يُحفظ المفتاح؟ اختر موقعًا آمنًا خارج المستودع." };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var path = Vault.CreateKey(dlg.SelectedPath);
            _vault.KeyPath = path;
            _vault.LedgerPath = _vault.DefaultLedgerPath;
            _vault.Save();
            ReloadLedger();
            RefreshKeyState();

            MessageBox.Show(this,
                $"أُنشئ المفتاح:\n{path}\n\n" +
                "احتفظ بنسخة احتياطية منه في مكان ستجده بعد خمس سنوات. لا يمكن استرجاعه، وفقدانه " +
                "يعني إعادة إصدار كل ترخيص للعملاء.\n\n" +
                "ضع محتوى license-public.txt في إعداد License:PublicKey في المنتج.",
                "تم", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, Rtl);
        }
        catch (Exception ex) { Error(ex.Message); }
    }

    private void CopyPublicKey()
    {
        try
        {
            Clipboard.SetText(_vault.PublicKeyBase64());
            Info("نُسخ المفتاح العام. ضعه في إعداد License:PublicKey في appsettings.json.");
        }
        catch (Exception ex) { Error(ex.Message); }
    }

    private void OpenLedgerFolder()
    {
        var dir = Path.GetDirectoryName(_ledger.Path);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { Error("لا يوجد مجلد سجل بعد."); return; }
        Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
    }

    private void ReloadLedger()
    {
        try
        {
            _ledger = Ledger.Load(_vault.LedgerPath ?? _vault.DefaultLedgerPath);
            RefreshList();
        }
        catch (Exception ex) { Error(ex.Message); }
    }

    private void IssueNew()
    {
        using var dlg = new IssueDialog(_ledger);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Result is null) return;
        DoIssue(() => Issuer.Issue(dlg.Result, _vault, _ledger));
    }

    private void RenewSelected()
    {
        if (Selected is not { } previous) return;
        using var dlg = new IssueDialog(_ledger, previous);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Result is null) return;
        DoIssue(() => Issuer.Renew(previous, dlg.Result.ExpiresAt, _vault, _ledger));
    }

    private void DoIssue(Func<LedgerEntry> action)
    {
        try
        {
            var entry = action();
            RefreshList();
            SelectById(entry.Id);
            OfferSave(entry);
        }
        catch (Exception ex) { Error(ex.Message); }
    }

    /// <summary>Offered immediately after issuing rather than left to the operator to remember: a
    /// licence recorded but never written to a file is one the customer never receives.</summary>
    private void OfferSave(LedgerEntry entry)
    {
        var answer = MessageBox.Show(this,
            $"صدر الترخيص {entry.Id} للعميل {entry.Customer}.\n\nحفظ ملف license.lic لإرساله؟",
            "تم الإصدار", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, Rtl);
        if (answer == DialogResult.Yes) SaveEntry(entry);
    }

    private void SaveSelected() { if (Selected is { } e) SaveEntry(e); }

    private void SaveEntry(LedgerEntry entry)
    {
        using var dlg = new SaveFileDialog
        {
            Title = "حفظ ملف الترخيص",
            FileName = "license.lic",
            Filter = "ملف ترخيص (*.lic)|*.lic|كل الملفات (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dlg.FileName, entry.Token + Environment.NewLine);
            Info($"حُفظ في {dlg.FileName}\n\nيوضع الملف بجانب برنامج الخادم باسم license.lic.");
        }
        catch (Exception ex) { Error(ex.Message); }
    }

    private void CopySelected()
    {
        if (Selected is not { } e) return;
        try { Clipboard.SetText(e.Token); Info("نُسخ رمز الترخيص."); }
        catch (Exception ex) { Error(ex.Message); }
    }

    private void RevokeSelected()
    {
        if (Selected is not { } e) return;

        var answer = MessageBox.Show(this,
            $"سحب الترخيص {e.Id} ({e.Customer})؟\n\n" +
            "هذا تعليم داخلي في سجلّك فقط: لا يوجد اتصال بخوادم العملاء، فالترخيص الموجود لديهم " +
            "سيستمرّ في العمل حتى ينتهي. الفائدة أن أحدًا لن يُعيد إرساله بالخطأ.",
            "سحب ترخيص", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2, Rtl);
        if (answer != DialogResult.Yes) return;

        try
        {
            _ledger.Replace(e with { RevokedAt = DateTime.UtcNow, RevokedReason = "سُحب يدويًا" });
            RefreshList();
        }
        catch (Exception ex) { Error(ex.Message); }
    }

    private void InspectSomething()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "اختر ملف ترخيص لفحصه",
            Filter = "ملف ترخيص (*.lic)|*.lic|كل الملفات (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        ShowText("فحص الترخيص", Issuer.Inspect(dlg.FileName, _vault));
    }

    private void ShowDetails(LedgerEntry e) => ShowText($"الترخيص {e.Id}", Issuer.Inspect(e.Token, _vault));

    private void SelectById(string id)
    {
        foreach (ListViewItem item in _list.Items)
        {
            if (item.Tag is LedgerEntry e && e.Id == id)
            {
                item.Selected = true;
                item.EnsureVisible();
                return;
            }
        }
    }

    // ── small helpers ────────────────────────────────────────────────────────

    private const MessageBoxOptions Rtl = MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign;

    private void Info(string text) =>
        MessageBox.Show(this, text, "تراخيص مراسيل", MessageBoxButtons.OK, MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1, Rtl);

    private void Error(string text) =>
        MessageBox.Show(this, text, "تعذّر إتمام العملية", MessageBoxButtons.OK, MessageBoxIcon.Error,
            MessageBoxDefaultButton.Button1, Rtl);

    private void ShowText(string title, string body)
    {
        using var form = new Form
        {
            Text = title, RightToLeft = RightToLeft.Yes, RightToLeftLayout = true,
            ClientSize = new Size(620, 420), StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.White, MinimizeBox = false, MaximizeBox = false,
        };
        var box = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 10f), Text = body, BorderStyle = BorderStyle.None,
            BackColor = Color.White,
        };
        form.Controls.Add(box);
        form.ShowDialog(this);
    }
}
