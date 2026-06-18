using System.Runtime.Versioning;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Archiving.Api.Common;

/// <summary>Merges a document's attachments into a single PDF for "print all": existing PDFs have their
/// pages appended; images (JPEG/PNG/BMP/TIFF) each become a page. Unreadable parts are skipped.</summary>
public static class AttachmentMerger
{
    public static byte[] Combine(IReadOnlyList<(byte[] Bytes, string ContentType)> parts)
    {
        using var output = new PdfDocument();

        foreach (var (bytes, contentType) in parts)
        {
            try
            {
                if (IsPdf(bytes, contentType)) AppendPdf(output, bytes);
                else if (OperatingSystem.IsWindows()) AppendImagePage(output, bytes);
            }
            catch { /* skip an unreadable attachment rather than failing the whole print */ }
        }

        if (output.PageCount == 0) output.AddPage(); // never return an empty PDF
        using var ms = new MemoryStream();
        output.Save(ms);
        return ms.ToArray();
    }

    private static bool IsPdf(byte[] b, string contentType) =>
        contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) ||
        (b.Length >= 5 && b[0] == '%' && b[1] == 'P' && b[2] == 'D' && b[3] == 'F');

    private static void AppendPdf(PdfDocument output, byte[] bytes)
    {
        using var input = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        for (int i = 0; i < input.PageCount; i++)
            output.AddPage(input.Pages[i]);
    }

    [SupportedOSPlatform("windows")]
    private static void AppendImagePage(PdfDocument output, byte[] bytes)
    {
        // Normalize any image format to JPEG, then place it on a page sized to the image.
        byte[] jpeg;
        using (var inMs = new MemoryStream(bytes))
        using (var img = System.Drawing.Image.FromStream(inMs))
        using (var outMs = new MemoryStream())
        {
            img.Save(outMs, System.Drawing.Imaging.ImageFormat.Jpeg);
            jpeg = outMs.ToArray();
        }

        using var imgStream = new MemoryStream(jpeg, 0, jpeg.Length, writable: false, publiclyVisible: true);
        using var xImage = XImage.FromStream(imgStream);
        var page = output.AddPage();
        page.Width = XUnit.FromPoint(xImage.PixelWidth);
        page.Height = XUnit.FromPoint(xImage.PixelHeight);
        using var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawImage(xImage, 0, 0, xImage.PixelWidth, xImage.PixelHeight);
    }
}
