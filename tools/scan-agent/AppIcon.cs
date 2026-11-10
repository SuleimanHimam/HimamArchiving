using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

/// <summary>Draws the tray/window icon at runtime instead of shipping an .ico.
///
/// Two reasons: the agent stays a single self-contained file with no embedded binary asset to keep in
/// sync, and the icon can change colour to signal state — blue when the agent is listening, grey when
/// it is stopped — which is the whole point of a tray icon.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class AppIcon
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    private static readonly Color Live = Color.FromArgb(0x24, 0x75, 0x9F);   // brand sky — matches the app
    private static readonly Color Idle = Color.FromArgb(0x8A, 0x8A, 0x94);   // muted grey

    /// <summary>Builds a scanner glyph. The caller owns the Icon and must dispose it —
    /// GetHicon allocates an unmanaged HICON that the finalizer would otherwise leak.</summary>
    public static Icon Create(bool running, int size = 32)
    {
        var accent = running ? Live : Idle;

        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var pad = size * 0.09f;
            var body = new RectangleF(pad, size * 0.34f, size - pad * 2, size * 0.46f);

            // Sheet feeding out of the top of the scanner.
            var sheet = new RectangleF(size * 0.24f, pad, size * 0.52f, size * 0.34f);
            using (var paper = new SolidBrush(Color.White))
            using (var edge = new Pen(accent, Math.Max(1f, size * 0.055f)))
            {
                g.FillRectangle(paper, sheet);
                g.DrawRectangle(edge, sheet.X, sheet.Y, sheet.Width, sheet.Height);
            }

            // Scanner body.
            using (var fill = new SolidBrush(accent))
            using (var path = Rounded(body, size * 0.13f))
                g.FillPath(fill, path);

            // The scan line — white bar across the body.
            using (var line = new SolidBrush(Color.White))
                g.FillRectangle(line, body.X + body.Width * 0.14f, body.Y + body.Height * 0.40f,
                    body.Width * 0.72f, Math.Max(1.5f, size * 0.06f));
        }

        var hIcon = bmp.GetHicon();
        try
        {
            // Clone so the Icon owns managed data, then release the HICON immediately — an Icon built
            // straight from a handle does not free it, which leaks a GDI object per state change.
            using var fromHandle = Icon.FromHandle(hIcon);
            return (Icon)fromHandle.Clone();
        }
        finally { DestroyIcon(hIcon); }
    }

    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
