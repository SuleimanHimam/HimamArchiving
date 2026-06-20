using System.Drawing;
using System.Drawing.Printing;
using PDFtoImage;
using SkiaSharp;

/// <summary>Local printing: enumerate spooler queues and send a document (PDF or image) to a queue.
/// PDFs are rasterized with PDFtoImage so no external PDF reader is required — System.Drawing does
/// the actual spooling. Windows-only (the agent targets net*-windows).</summary>
internal static class Print
{
    public static string[] ListPrinters()
    {
        var list = new List<string>();
        foreach (string? name in PrinterSettings.InstalledPrinters)
            if (!string.IsNullOrWhiteSpace(name)) list.Add(name);
        return list.ToArray();
    }

    /// <summary>Print a file's bytes to the named queue (or the default queue when null).
    /// Returns the number of pages spooled.</summary>
    public static int PrintFile(byte[] data, string ext, string? printer)
    {
        ext = ext.TrimStart('.').ToLowerInvariant();
        var pages = ext switch
        {
            "pdf" => RenderPdf(data),
            "jpg" or "jpeg" or "png" or "bmp" or "tif" or "tiff" or "gif" => new List<Image> { BytesToImage(data) },
            _ => throw new InvalidOperationException($"نوع غير مدعوم للطباعة: {ext}"),
        };
        try { return PrintImages(pages, printer); }
        finally { foreach (var p in pages) p.Dispose(); }
    }

    // Copy into a standalone Bitmap so we don't have to keep the source stream alive (GDI+ requirement).
    private static Image BytesToImage(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var tmp = Image.FromStream(ms);
        return new Bitmap(tmp);
    }

    private static List<Image> RenderPdf(byte[] pdf)
    {
        var images = new List<Image>();
        foreach (var bitmap in Conversion.ToImages(pdf, options: new RenderOptions(Dpi: 200)))
        {
            using (bitmap)
            using (var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 90))
                images.Add(BytesToImage(encoded.ToArray()));
        }
        if (images.Count == 0) throw new InvalidOperationException("تعذّر تحويل ملف PDF للطباعة");
        return images;
    }

    private static int PrintImages(List<Image> images, string? printer)
    {
        using var doc = new PrintDocument();
        if (!string.IsNullOrWhiteSpace(printer)) doc.PrinterSettings.PrinterName = printer;
        if (!doc.PrinterSettings.IsValid)
            throw new InvalidOperationException($"الطابعة غير صالحة: {printer ?? "(افتراضية)"}");

        var index = 0;
        doc.PrintPage += (_, e) =>
        {
            var img = images[index];
            var area = e.MarginBounds;
            var scale = Math.Min((double)area.Width / img.Width, (double)area.Height / img.Height);
            var w = (int)(img.Width * scale);
            var h = (int)(img.Height * scale);
            var x = area.Left + (area.Width - w) / 2;
            var y = area.Top + (area.Height - h) / 2;
            e.Graphics!.DrawImage(img, x, y, w, h);
            index++;
            e.HasMorePages = index < images.Count;
        };
        doc.Print();
        return images.Count;
    }
}
