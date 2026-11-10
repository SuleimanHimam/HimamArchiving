using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

/// <summary>Sample pages for <c>--mock</c> mode, so the full pipeline (browser → agent → cloud
/// upload) can be exercised without a physical scanner.
///
/// Pages are drawn rather than replayed from a fixed literal: a multi-page batch is only really
/// testable if the pages are visibly distinct, and honouring the requested paper size, colour mode
/// and page count means mock exercises the same option plumbing a real driver would.</summary>
[SupportedOSPlatform("windows")]
internal static class Mock
{
    /// <summary>How many sheets the imaginary feeder holds when the caller sets no limit.</summary>
    private const int DefaultFeederSheets = 3;

    /// <summary>Rendering is capped well below real scanner resolutions: an A4 page at 600 dpi is a
    /// ~140 MB bitmap, and nothing about mock mode is improved by allocating it.</summary>
    private const int MaxRenderDpi = 150;

    public static List<byte[]> SamplePages(ScanOptions opts)
    {
        var sheets = opts.Source == PaperSource.Feeder
            ? (opts.MaxPages > 0 ? Math.Min(opts.MaxPages, Wia.PageCap) : DefaultFeederSheets)
            : 1;

        // Duplex turns each sheet into two images (front and back), exactly as a real ADF reports.
        var pages = opts.Source == PaperSource.Feeder && opts.Duplex ? sheets * 2 : sheets;
        if (opts.MaxPages > 0) pages = Math.Min(pages, opts.MaxPages);

        var result = new List<byte[]>(pages);
        for (int i = 0; i < pages; i++)
        {
            var side = opts.Duplex && opts.Source == PaperSource.Feeder
                ? (i % 2 == 0 ? "الوجه" : "الظهر")
                : null;
            result.Add(RenderPage(i + 1, pages, side, opts));
        }
        return result;
    }

    /// <summary>Back-compat single page (the original entry point).</summary>
    public static byte[] SampleJpeg() => RenderPage(1, 1, null, new ScanOptions());

    private static byte[] RenderPage(int number, int total, string? side, ScanOptions opts)
    {
        var dpi = Math.Clamp(opts.Dpi, 50, MaxRenderDpi);
        var (wIn, hIn) = opts.Paper.Inches() ?? (8.27, 11.69);
        int w = (int)Math.Round(wIn * dpi), h = (int)Math.Round(hIn * dpi);

        using var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.Clear(Color.White);

            var ink = Color.FromArgb(0x1F, 0x6E, 0x5E);
            using var border = new Pen(ink, Math.Max(2f, dpi / 50f));
            var margin = dpi / 2;
            g.DrawRectangle(border, margin, margin, w - margin * 2, h - margin * 2);

            using var big = new Font("Segoe UI", dpi / 3f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var small = new Font("Segoe UI", dpi / 8f, FontStyle.Regular, GraphicsUnit.Pixel);
            using var inkBrush = new SolidBrush(ink);
            using var grey = new SolidBrush(Color.FromArgb(0x66, 0x66, 0x66));
            var centre = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            g.DrawString($"{number} / {total}", big, inkBrush, new RectangleF(0, 0, w, h), centre);

            var caption = side is null ? "صفحة تجريبية" : $"صفحة تجريبية — {side}";
            g.DrawString(caption, small, grey,
                new RectangleF(0, h / 2f + dpi / 2f, w, dpi), centre);
            g.DrawString($"{opts.Dpi} dpi · {opts.Color} · {opts.Paper} · {opts.Source}", small, grey,
                new RectangleF(0, h - margin - dpi / 3f, w, dpi / 2f), centre);
        }

        using var render = ApplyColorMode(bmp, opts.Color);
        using var ms = new MemoryStream();
        render.Save(ms, ImageFormat.Jpeg);
        return ms.ToArray();
    }

    /// <summary>Mirrors what a scanner's colour setting does to the output, so a caller asking for
    /// grayscale actually receives a single-channel-looking page rather than colour.</summary>
    private static Bitmap ApplyColorMode(Bitmap source, ColorMode mode)
    {
        if (mode == ColorMode.Color) return (Bitmap)source.Clone();

        var grey = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(grey))
        {
            // Standard luminance weights; the extra threshold pass below turns grey into 1-bit.
            var matrix = new ColorMatrix(
            [
                [0.299f, 0.299f, 0.299f, 0, 0],
                [0.587f, 0.587f, 0.587f, 0, 0],
                [0.114f, 0.114f, 0.114f, 0, 0],
                [0, 0, 0, 1, 0],
                [0, 0, 0, 0, 1],
            ]);
            using var attrs = new ImageAttributes();
            attrs.SetColorMatrix(matrix);
            g.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height),
                0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);
        }

        if (mode == ColorMode.Grayscale) return grey;

        // Black & white: hard threshold, matching what a scanner's 1-bit mode produces.
        for (int y = 0; y < grey.Height; y++)
            for (int x = 0; x < grey.Width; x++)
                grey.SetPixel(x, y, grey.GetPixel(x, y).R < 128 ? Color.Black : Color.White);
        return grey;
    }
}
