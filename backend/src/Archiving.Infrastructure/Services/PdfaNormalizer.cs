using System.Runtime.Versioning;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Archiving.Infrastructure.Services;

/// <summary>Produces a PDF/A-2b preservation copy of a scanned image (ISO 19005 / 14721 AIP).
/// The original bytes are kept separately as the submitted copy (SIP).</summary>
public static class PdfaNormalizer
{
    public const string Conformance = "PDF/A-2B";

    /// <summary>True for image types we can normalize to PDF/A here (the scan ingest path).</summary>
    public static bool CanNormalize(string contentType, byte[] bytes)
    {
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return true;
        // sniff common image magic numbers
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8) return true;                 // JPEG
        if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50) return true;                 // PNG
        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D) return true;                 // BMP
        return false;
    }

    [SupportedOSPlatform("windows")]
    public static byte[] ImageToPdfA(byte[] imageBytes, string conformance = Conformance)
    {
        // Normalize to PNG first (lossless, Skia-friendly) so any scanner format (BMP/TIFF/…) works.
        var png = ToPng(imageBytes);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.Content().AlignCenter().AlignMiddle().Image(png).FitArea();
            });
        }).WithSettings(new DocumentSettings
        {
            PDFA_Conformance = MapConformance(conformance),
            ImageCompressionQuality = ImageCompressionQuality.VeryHigh,
            ImageRasterDpi = 300,
        });

        return document.GeneratePdf();
    }

    private static PDFA_Conformance MapConformance(string conformance) => conformance.ToUpperInvariant() switch
    {
        "PDF/A-2A" => PDFA_Conformance.PDFA_2A,
        "PDF/A-2B" => PDFA_Conformance.PDFA_2B,
        "PDF/A-2U" => PDFA_Conformance.PDFA_2U,
        "PDF/A-3A" => PDFA_Conformance.PDFA_3A,
        "PDF/A-3B" => PDFA_Conformance.PDFA_3B,
        "PDF/A-3U" => PDFA_Conformance.PDFA_3U,
        _ => PDFA_Conformance.PDFA_2B,
    };

    [SupportedOSPlatform("windows")]
    private static byte[] ToPng(byte[] input)
    {
        using var inMs = new MemoryStream(input);
        using var img = System.Drawing.Image.FromStream(inMs);
        using var outMs = new MemoryStream();
        img.Save(outMs, System.Drawing.Imaging.ImageFormat.Png);
        return outMs.ToArray();
    }
}
