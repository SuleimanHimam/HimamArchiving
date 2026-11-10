using System.Drawing;
using System.Globalization;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace Archiving.LicenseManager;

/// <summary>The issue form. Everything a licence carries, in the order an operator fills it.</summary>
[SupportedOSPlatform("windows")]
internal sealed class IssueDialog : Form
{
    private readonly TextBox _customer = new();
    private readonly TextBox _machine = new();
    private readonly Label _machineState = new();
    private readonly ComboBox _edition = new();

    private readonly RadioButton _perpetual = new() { Text = "دائم" };
    private readonly RadioButton _forDays = new() { Text = "لمدّة", Checked = true };
    private readonly NumericUpDown _days = new() { Minimum = 1, Maximum = 3650, Value = 365, Width = 80 };
    private readonly RadioButton _untilDate = new() { Text = "حتى تاريخ" };
    private readonly DateTimePicker _until = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", Width = 130 };

    private readonly NumericUpDown _grace = new() { Minimum = 0, Maximum = 365, Value = 14, Width = 80 };
    private readonly CheckBox _limitUsers = new() { Text = "حدّ لعدد المستخدمين" };
    private readonly NumericUpDown _maxUsers = new() { Minimum = 1, Maximum = 100000, Value = 50, Width = 90, Enabled = false };
    private readonly TextBox _features = new();
    private readonly TextBox _notes = new();

    private readonly Ledger _ledger;

    public IssueRequest? Result { get; private set; }

    public IssueDialog(Ledger ledger, LedgerEntry? template = null)
    {
        _ledger = ledger;

        Text = template is null ? "إصدار ترخيص جديد" : $"تجديد ترخيص — {template.Customer}";
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 520);
        BackColor = Color.White;

        BuildLayout();

        _machine.TextChanged += (_, _) => ValidateMachine();
        _limitUsers.CheckedChanged += (_, _) => _maxUsers.Enabled = _limitUsers.Checked;
        foreach (var r in new[] { _perpetual, _forDays, _untilDate })
            r.CheckedChanged += (_, _) => SyncTerm();

        _until.Value = DateTime.Today.AddYears(1);

        if (template is not null) Prefill(template);
        SyncTerm();
        ValidateMachine();
    }

    private void Prefill(LedgerEntry t)
    {
        _customer.Text = t.Customer;
        _machine.Text = t.MachineId;
        _edition.Text = t.Edition;
        _grace.Value = Math.Clamp(t.GraceDays, 0, 365);
        if (t.MaxUsers is { } mu) { _limitUsers.Checked = true; _maxUsers.Value = Math.Clamp(mu, 1, 100000); }
        _features.Text = string.Join(", ", t.Features);
        _notes.Text = t.Notes ?? "";

        // A renewal of a perpetual licence is a contradiction, so the term resets to a fresh year
        // rather than carrying over a past expiry date that is already behind us.
        _forDays.Checked = true;
        _days.Value = 365;
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16), AutoSize = true,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _customer.Width = 360;
        _machine.Width = 360;
        _machine.Font = new Font("Consolas", 10f);
        _machine.CharacterCasing = CharacterCasing.Upper;
        _features.Width = 360;
        _notes.Width = 360;
        _notes.Multiline = true;
        _notes.Height = 56;

        _edition.Width = 200;
        _edition.DropDownStyle = ComboBoxStyle.DropDown;
        _edition.Items.AddRange(["Standard", "Professional", "Enterprise"]);
        _edition.SelectedIndex = 0;

        _machineState.AutoSize = true;
        _machineState.MaximumSize = new Size(360, 0);
        _machineState.Font = new Font("Segoe UI", 8.5f);

        AddRow(root, "العميل", _customer);
        AddRow(root, "بصمة الجهاز", _machine);
        AddRow(root, "", _machineState);
        AddRow(root, "الإصدار", _edition);

        var term = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        term.Controls.AddRange([_forDays, _days, new Label { Text = "يومًا", AutoSize = true, Margin = new Padding(2, 6, 12, 0) }, _perpetual]);
        AddRow(root, "المدّة", term);

        var until = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        until.Controls.AddRange([_untilDate, _until]);
        AddRow(root, "", until);

        var grace = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        grace.Controls.AddRange([_grace, new Label
        {
            Text = "يومًا يستمرّ النظام بعد الانتهاء مع تحذير", AutoSize = true, Margin = new Padding(6, 6, 0, 0),
            ForeColor = Color.FromArgb(0x55, 0x5F, 0x6B),
        }]);
        AddRow(root, "مهلة السماح", grace);

        var users = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        users.Controls.AddRange([_limitUsers, _maxUsers]);
        AddRow(root, "المستخدمون", users);

        AddRow(root, "الوحدات", _features);
        AddRow(root, "", new Label
        {
            Text = "اتركه فارغًا لتمكين كل الوحدات، أو اكتب مفاتيح الوحدات مفصولة بفواصل.",
            AutoSize = true, MaximumSize = new Size(360, 0),
            ForeColor = Color.FromArgb(0x55, 0x5F, 0x6B), Font = new Font("Segoe UI", 8.5f),
        });
        AddRow(root, "ملاحظات", _notes);

        var ok = new Button { Text = "إصدار", AutoSize = true, Padding = new Padding(14, 4, 14, 4), DialogResult = DialogResult.None };
        var cancel = new Button { Text = "إلغاء", AutoSize = true, Padding = new Padding(14, 4, 14, 4), DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => Accept();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true, Padding = new Padding(16, 8, 16, 12),
        };
        buttons.Controls.AddRange([ok, cancel]);

        AcceptButton = ok;
        CancelButton = cancel;

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scroll.Controls.Add(root);
        Controls.Add(scroll);
        Controls.Add(buttons);
    }

    private static void AddRow(TableLayoutPanel root, string label, Control control)
    {
        var row = root.RowCount++;
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(new Label
        {
            Text = label, AutoSize = true, Margin = new Padding(0, 7, 8, 0),
            ForeColor = Color.FromArgb(0x2A, 0x33, 0x3E),
        }, 0, row);
        control.Margin = new Padding(0, 4, 0, 4);
        root.Controls.Add(control, 1, row);
    }

    private void SyncTerm()
    {
        _days.Enabled = _forDays.Checked;
        _until.Enabled = _untilDate.Checked;
        // Grace has no meaning without an expiry date, so it is disabled rather than left showing a
        // number that would silently do nothing.
        _grace.Enabled = !_perpetual.Checked;
    }

    /// <summary>Live feedback on the fingerprint, including the two things worth interrupting for:
    /// a character that cannot be part of a fingerprint, and a machine that already has a licence.</summary>
    private void ValidateMachine()
    {
        var check = Fingerprint.Check(_machine.Text);
        if (!check.Usable)
        {
            _machineState.ForeColor = Color.FromArgb(0xA8, 0x2C, 0x22);
            _machineState.Text = check.Message ?? "";
            return;
        }

        var existing = _ledger.LiveFor(check.Value);
        if (existing.Count > 0)
        {
            _machineState.ForeColor = Color.FromArgb(0x9A, 0x6B, 0x10);
            _machineState.Text =
                $"تنبيه: يوجد ترخيص سارٍ لهذا الجهاز ({existing[0].Customer} — {existing[0].Id}). "
              + "الأفضل تجديده من القائمة بدل إصدار ترخيص ثانٍ لنفس الجهاز.";
            return;
        }

        _machineState.ForeColor = check.Verdict == Fingerprint.Verdict.Repaired
            ? Color.FromArgb(0x9A, 0x6B, 0x10)
            : Color.FromArgb(0x2C, 0x6B, 0x4E);
        _machineState.Text = check.Message ?? $"بصمة صالحة: {check.Value}";
    }

    private void Accept()
    {
        var check = Fingerprint.Check(_machine.Text);
        if (!check.Usable)
        {
            Warn(check.Message ?? "بصمة الجهاز غير صالحة.");
            return;
        }
        if (string.IsNullOrWhiteSpace(_customer.Text))
        {
            Warn("اسم العميل مطلوب — وهو يظهر داخل الترخيص ويقرأه العميل.");
            return;
        }

        DateTime? expires =
            _perpetual.Checked ? null
            : _untilDate.Checked ? DateTime.SpecifyKind(_until.Value.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc)
            : DateTime.UtcNow.Date.AddDays((int)_days.Value).AddSeconds(-1);

        if (expires is not null && expires <= DateTime.UtcNow)
        {
            Warn("تاريخ الانتهاء في الماضي. اختر تاريخًا لاحقًا.");
            return;
        }

        Result = new IssueRequest
        {
            Customer = _customer.Text.Trim(),
            MachineId = check.Value,
            Edition = string.IsNullOrWhiteSpace(_edition.Text) ? "Standard" : _edition.Text.Trim(),
            ExpiresAt = expires,
            GraceDays = (int)_grace.Value,
            MaxUsers = _limitUsers.Checked ? (int)_maxUsers.Value : null,
            Features = _features.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Notes = _notes.Text,
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, "تحقّق من البيانات", MessageBoxButtons.OK, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

    private static string Fmt(decimal d) => d.ToString(CultureInfo.InvariantCulture);
}
