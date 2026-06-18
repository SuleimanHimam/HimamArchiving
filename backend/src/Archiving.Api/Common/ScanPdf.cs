using System.Runtime.Versioning;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Archiving.Api.Common;

/// <summary>Normalizes a scanned image — in whatever format the scanner's WIA driver produced
/// (BMP / JPEG / PNG / TIFF) — into the user-chosen output: <c>pdf</c>, <c>jpg</c>, or <c>png</c>.
/// Scanners commonly return BMP even when JPEG is requested, so we decode and re-encode.</summary>
public static class ScanPdf
{
    public static async Task<(Stream Stream, string FileName, string ContentType)> ToOutputAsync(
        Stream input, string fileName, string contentType, string format, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await input.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "مسح-ضوئي";

        // Decode any scanner image format and re-encode to the requested output (Windows host).
        if (OperatingSystem.IsWindows())
        {
            try { return BuildOnWindows(bytes, baseName, format); }
            catch { /* decode failed — fall back to the passthrough logic below */ }
        }

        // Off-Windows fallback: only JPEG can be wrapped to PDF; otherwise pass through.
        bool isJpeg = bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8;
        if (format is "pdf" && isJpeg)
            return (new MemoryStream(ImageToPdf(bytes)), $"{baseName}.pdf", "application/pdf");
        return (new MemoryStream(bytes), fileName, contentType);
    }

    [SupportedOSPlatform("windows")]
    private static (Stream, string, string) BuildOnWindows(byte[] bytes, string baseName, string format)
    {
        using var inMs = new MemoryStream(bytes);
        using var image = System.Drawing.Image.FromStream(inMs); // handles BMP/JPEG/PNG/TIFF/GIF

        switch (format)
        {
            case "jpg":
            case "jpeg":
                return (new MemoryStream(Encode(image, System.Drawing.Imaging.ImageFormat.Jpeg)),
                    $"{baseName}.jpg", "image/jpeg");

            case "png":
                return (new MemoryStream(Encode(image, System.Drawing.Imaging.ImageFormat.Png)),
                    $"{baseName}.png", "image/png");

            case "pdf":
            default:
                // Normalize to JPEG, then embed on a PDF page via PDFsharp.
                var jpeg = Encode(image, System.Drawing.Imaging.ImageFormat.Jpeg);
                return (new MemoryStream(ImageToPdf(jpeg)), $"{baseName}.pdf", "application/pdf");
        }
    }

    [SupportedOSPlatform("windows")]
    private static byte[] Encode(System.Drawing.Image image, System.Drawing.Imaging.ImageFormat fmt)
    {
        using var outMs = new MemoryStream();
        image.Save(outMs, fmt);
        return outMs.ToArray();
    }

    /// <summary>Embeds a JPEG on a single page sized to the image. PDFsharp writes a valid, viewer-
    /// compatible PDF.</summary>
    private static byte[] ImageToPdf(byte[] jpeg)
    {
        // publiclyVisible:true so PDFsharp can read the buffer (it calls GetBuffer internally).
        using var imgStream = new MemoryStream(jpeg, 0, jpeg.Length, writable: false, publiclyVisible: true);
        using var xImage = XImage.FromStream(imgStream);
        using var doc = new PdfDocument();
        var page = doc.AddPage();
        page.Width = XUnit.FromPoint(xImage.PixelWidth);
        page.Height = XUnit.FromPoint(xImage.PixelHeight);
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(xImage, 0, 0, xImage.PixelWidth, xImage.PixelHeight);

        using var outMs = new MemoryStream();
        doc.Save(outMs);
        return outMs.ToArray();
    }
}
