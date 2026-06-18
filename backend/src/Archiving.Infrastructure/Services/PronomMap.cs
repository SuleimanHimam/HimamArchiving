namespace Archiving.Infrastructure.Services;

/// <summary>Maps a stored file to PRONOM Representation Information (format id + name + rendering note)
/// — the "how to render it later" metadata OAIS requires.</summary>
public static class PronomMap
{
    public static (string? Puid, string? Format, string? Note) For(string fileExtension, string? pdfAConformance, string contentType)
    {
        if (!string.IsNullOrWhiteSpace(pdfAConformance))
        {
            var puid = pdfAConformance.ToUpperInvariant() switch
            {
                "PDF/A-1A" => "fmt/95", "PDF/A-1B" => "fmt/354",
                "PDF/A-2A" => "fmt/476", "PDF/A-2B" => "fmt/477", "PDF/A-2U" => "fmt/478",
                "PDF/A-3A" => "fmt/479", "PDF/A-3B" => "fmt/480", "PDF/A-3U" => "fmt/481",
                _ => "fmt/477",
            };
            return (puid, pdfAConformance, "يُفتح ببرامج قراءة PDF/A");
        }

        return fileExtension.TrimStart('.').ToLowerInvariant() switch
        {
            "pdf" => ("fmt/276", "PDF", "يُفتح ببرامج قراءة PDF"),
            "jpg" or "jpeg" => ("fmt/44", "JPEG", "صورة JPEG"),
            "png" => ("fmt/13", "PNG", "صورة PNG"),
            "bmp" => ("fmt/116", "Windows Bitmap", "صورة BMP"),
            "tif" or "tiff" => ("fmt/353", "TIFF", "صورة TIFF"),
            "docx" => ("fmt/412", "Word (OOXML)", "Microsoft Word"),
            "xlsx" => ("fmt/214", "Excel (OOXML)", "Microsoft Excel"),
            "zip" => ("x-fmt/263", "ZIP", "أرشيف مضغوط"),
            _ => (null, contentType, null),
        };
    }
}
