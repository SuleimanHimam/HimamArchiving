using System.Runtime.Versioning;
using System.Text;
using Archiving.Infrastructure.Services;
using Xunit;

namespace Archiving.Tests;

[SupportedOSPlatform("windows")]
public class PdfaNormalizerTests
{
    static PdfaNormalizerTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    [Fact]
    public void Produces_pdf_with_pdfa_identifier()
    {
        if (!OperatingSystem.IsWindows()) return; // normalizer is Windows-only (System.Drawing)

        var pdf = PdfaNormalizer.ImageToPdfA(MakePng());

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        // PDF/A mandates an uncompressed XMP metadata stream carrying the pdfaid identification schema.
        Assert.Contains("pdfaid", Encoding.Latin1.GetString(pdf));
        Assert.Equal("PDF/A-2B", PdfaNormalizer.Conformance);
    }

    [Fact]
    public void Recognizes_image_content()
    {
        Assert.True(PdfaNormalizer.CanNormalize("image/png", MakePng()));
        Assert.False(PdfaNormalizer.CanNormalize("application/pdf", "%PDF-1.4"u8.ToArray()));
    }

    private static byte[] MakePng()
    {
        using var bmp = new System.Drawing.Bitmap(48, 48);
        using (var g = System.Drawing.Graphics.FromImage(bmp)) g.Clear(System.Drawing.Color.White);
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }
}
