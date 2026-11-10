using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

/// <summary>Review the pages a scan produced before they are handed to the caller: drop the blank
/// or crooked ones, re-scan a page in place, then approve.
///
/// Shown only when the request asks for it (<c>preview: true</c>). The HTTP request is held open
/// while this is on screen — that is the point of asking for a reviewed scan — so the dialog is
/// deliberately modal and always offers a way out.</summary>
[SupportedOSPlatform("windows")]
internal sealed class PreviewDialog : Form
{
    private readonly List<byte[]> _pages;
    private readonly ScanOptions _options;
    private readonly Func<ScanOptions, List<byte[]>> _rescan;

    private readonly ListBox _list = new();
    private readonly PictureBox _preview = new();
    private readonly Label _summary = new();
    private readonly Button _rescanPage = new();
    private readonly Button _appendPages = new();
    private readonly Button _remove = new();
    private readonly Button _rotate = new();
    private readonly Button _approve = new();
    private readonly Button _cancel = new();

    /// <summary>The pages the user approved, or null if they cancelled.</summary>
    public List<byte[]>? Result { get; private set; }

    public PreviewDialog(List<byte[]> pages, ScanOptions options, Func<ScanOptions, List<byte[]>> rescan)
    {
        _pages = [.. pages];
        _options = options;
        _rescan = rescan;

        Text = "مراجعة الصفحات الممسوحة";
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 560);
        Size = new Size(940, 660);
        ShowInTaskbar = true;
        MinimizeBox = false;

        BuildLayout();
        RefreshList(select: 0);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(12) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _summary.AutoSize = true;
        _summary.Margin = new Padding(3, 0, 3, 10);
        root.Controls.Add(_summary, 0, 0);
        root.SetColumnSpan(_summary, 2);

        _list.Dock = DockStyle.Fill;
        _list.IntegralHeight = false;
        _list.SelectedIndexChanged += (_, _) => ShowSelected();
        root.Controls.Add(_list, 0, 1);

        _preview.Dock = DockStyle.Fill;
        _preview.SizeMode = PictureBoxSizeMode.Zoom;
        _preview.BorderStyle = BorderStyle.FixedSingle;
        _preview.BackColor = Color.FromArgb(0xF3, 0xF4, 0xF6);
        root.Controls.Add(_preview, 1, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 10, 0, 0), WrapContents = true,
        };

        void Add(Button b, string text, EventHandler onClick, bool primary = false)
        {
            b.Text = text;
            b.AutoSize = true;
            b.Padding = new Padding(10, 5, 10, 5);
            b.Margin = new Padding(6, 0, 0, 0);
            if (primary) { b.BackColor = Color.FromArgb(0x1F, 0x6E, 0x5E); b.ForeColor = Color.White; b.FlatStyle = FlatStyle.Flat; }
            b.Click += onClick;
            buttons.Controls.Add(b);
        }

        Add(_approve, "اعتماد وإرسال", (_, _) => Approve(), primary: true);
        Add(_cancel, "إلغاء", (_, _) => Cancel());
        Add(_rescanPage, "إعادة مسح الصفحة", (_, _) => RescanSelected());
        Add(_appendPages, "مسح صفحات إضافية", (_, _) => AppendMore());
        Add(_remove, "حذف الصفحة", (_, _) => RemoveSelected());
        Add(_rotate, "تدوير ١٨٠°", (_, _) => RotateSelected());

        root.Controls.Add(buttons, 0, 2);
        root.SetColumnSpan(buttons, 2);

        Controls.Add(root);
        AcceptButton = _approve;
        CancelButton = _cancel;
    }

    private void RefreshList(int select)
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        for (int i = 0; i < _pages.Count; i++)
            _list.Items.Add($"صفحة {i + 1} — {_pages[i].Length / 1024} ك.ب");
        _list.EndUpdate();

        if (_pages.Count > 0)
            _list.SelectedIndex = Math.Clamp(select, 0, _pages.Count - 1);
        else
            ShowSelected();

        _summary.Text = _pages.Count == 0
            ? "لم تبقَ أي صفحة — أعد المسح أو ألغِ العملية."
            : $"{_pages.Count} صفحة · {_options.Dpi} dpi · {_options.Color} · {_options.Paper}" +
              (_options.Source == PaperSource.Feeder ? " · وحدة التغذية" : " · الزجاج") +
              (_options.Duplex ? " · وجهان" : "");

        var has = _pages.Count > 0;
        _approve.Enabled = has;
        _rescanPage.Enabled = has;
        _remove.Enabled = has;
        _rotate.Enabled = has;
    }

    private void ShowSelected()
    {
        _preview.Image?.Dispose();
        _preview.Image = null;
        var i = _list.SelectedIndex;
        if (i < 0 || i >= _pages.Count) return;
        try
        {
            // Copy into a MemoryStream the Image keeps owning; Image.FromStream requires the stream
            // to outlive the image, so it is deliberately not disposed here.
            _preview.Image = Image.FromStream(new MemoryStream(_pages[i]));
        }
        catch { /* an unreadable page still shows in the list so it can be removed */ }
    }

    private void RemoveSelected()
    {
        var i = _list.SelectedIndex;
        if (i < 0) return;
        _pages.RemoveAt(i);
        RefreshList(select: i);
    }

    /// <summary>180° covers the case that actually happens with a feeder — a stack loaded upside
    /// down. Re-encoded as JPEG so the result stays the same kind of payload as a scan.</summary>
    private void RotateSelected()
    {
        var i = _list.SelectedIndex;
        if (i < 0) return;
        try
        {
            using var src = Image.FromStream(new MemoryStream(_pages[i]));
            src.RotateFlip(RotateFlipType.Rotate180FlipNone);
            using var ms = new MemoryStream();
            src.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            _pages[i] = ms.ToArray();
            RefreshList(select: i);
        }
        catch (Exception ex) { Warn(ex.Message); }
    }

    private void RescanSelected()
    {
        var i = _list.SelectedIndex;
        if (i < 0) return;
        // One sheet, whatever the original request asked for — this replaces a single bad page.
        Rescan(_options with { Source = PaperSource.Flatbed, Duplex = false, MaxPages = 1, Preview = false },
            replaceAt: i);
    }

    private void AppendMore() =>
        Rescan(_options with { Preview = false }, replaceAt: null);

    private void Rescan(ScanOptions opts, int? replaceAt)
    {
        UseWaitCursor = true;
        Enabled = false;
        try
        {
            var fresh = _rescan(opts);
            if (fresh.Count == 0) return;
            if (replaceAt is { } at && at < _pages.Count)
            {
                _pages[at] = fresh[0];
                RefreshList(select: at);
            }
            else
            {
                _pages.AddRange(fresh);
                RefreshList(select: _pages.Count - 1);
            }
        }
        catch (Exception ex) { Warn(ex.Message); }
        finally
        {
            Enabled = true;
            UseWaitCursor = false;
        }
    }

    private void Approve()
    {
        if (_pages.Count == 0) return;
        Result = _pages;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Cancel()
    {
        Result = null;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, "مراجعة المسح", MessageBoxButtons.OK, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _preview.Image?.Dispose();
        base.Dispose(disposing);
    }
}
