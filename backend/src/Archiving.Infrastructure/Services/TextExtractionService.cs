using System.Text;
using Archiving.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using SkiaSharp;
using Tesseract;
using UglyToad.PdfPig;

namespace Archiving.Infrastructure.Services;

/// <summary>
/// Pulls searchable text out of files. Digital PDFs are read directly (PdfPig, managed and safe);
/// scanned PDFs without a text layer, and image files, are OCR'd with Tesseract (Arabic + English)
/// after rasterizing pages with PDFtoImage. OCR is best-effort and isolated so text PDFs always work.
/// </summary>
public sealed class TextExtractionService : ITextExtractionService
{
    private static readonly HashSet<string> ImageExts = new(StringComparer.OrdinalIgnoreCase)
        { "jpg", "jpeg", "png", "tif", "tiff", "bmp" };

    private readonly ILogger<TextExtractionService> _log;
    private readonly string _tessData;
    private readonly string _languages;
    private readonly bool _ocrEnabled;
    private readonly int _ocrMaxPages;
    private readonly int _ocrDpi;
    private readonly int _embeddedMinChars;

    public TextExtractionService(IConfiguration config, ILogger<TextExtractionService> log)
    {
        _log = log;
        var s = config.GetSection("Search");
        _tessData = s["TessDataPath"] is { Length: > 0 } p ? p : Path.Combine(AppContext.BaseDirectory, "tessdata");
        _languages = s["OcrLanguages"] is { Length: > 0 } l ? l : "ara+eng";
        _ocrEnabled = s.GetValue("OcrEnabled", true);
        _ocrMaxPages = s.GetValue("OcrMaxPages", 30);
        _ocrDpi = s.GetValue("OcrDpi", 200);
        _embeddedMinChars = s.GetValue("EmbeddedTextMinChars", 12);
    }

    public bool CanExtract(string fileExtension)
    {
        var ext = fileExtension.TrimStart('.');
        return ext.Equals("pdf", StringComparison.OrdinalIgnoreCase) || ImageExts.Contains(ext);
    }

    public Task<TextExtractionResult> ExtractAsync(byte[] fileBytes, string fileExtension, CancellationToken ct = default)
    {
        var ext = fileExtension.TrimStart('.').ToLowerInvariant();

        if (ext == "pdf")
        {
            var embedded = ExtractPdfText(fileBytes);
            if (Meaningful(embedded))
                return Task.FromResult(new TextExtractionResult(embedded.Trim(), "Embedded"));
            // No usable text layer → it's a scan; OCR the rendered pages.
            return Task.FromResult(OcrPdf(fileBytes, ct));
        }

        if (ImageExts.Contains(ext))
            return Task.FromResult(OcrImage(fileBytes));

        return Task.FromResult(new TextExtractionResult(null, "None"));
    }

    private bool Meaningful(string? text) =>
        !string.IsNullOrWhiteSpace(text) && text.Count(c => !char.IsWhiteSpace(c)) >= _embeddedMinChars;

    private static string ExtractPdfText(byte[] pdf)
    {
        try
        {
            using var doc = PdfDocument.Open(pdf);
            var sb = new StringBuilder();
            foreach (var page in doc.GetPages())
                sb.AppendLine(page.Text);
            return sb.ToString();
        }
        catch
        {
            return string.Empty; // unreadable/encrypted text layer → fall back to OCR
        }
    }

    private TextExtractionResult OcrPdf(byte[] pdf, CancellationToken ct)
    {
        if (!_ocrEnabled) return new TextExtractionResult(string.Empty, "OCR");

        using var engine = NewEngine();
        var sb = new StringBuilder();
        var page = 0;
        foreach (var bitmap in Conversion.ToImages(pdf, options: new RenderOptions(Dpi: _ocrDpi)))
        {
            ct.ThrowIfCancellationRequested();
            using (bitmap)
            {
                if (page++ >= _ocrMaxPages) break;
                using var data = bitmap.Encode(SKEncodedImageFormat.Png, 90);
                sb.AppendLine(OcrPng(engine, data.ToArray()));
            }
        }
        return new TextExtractionResult(sb.ToString().Trim(), "OCR");
    }

    private TextExtractionResult OcrImage(byte[] image)
    {
        if (!_ocrEnabled) return new TextExtractionResult(string.Empty, "OCR");
        using var engine = NewEngine();
        using var pix = Pix.LoadFromMemory(image);
        using var page = engine.Process(pix);
        return new TextExtractionResult(page.GetText()?.Trim() ?? string.Empty, "OCR");
    }

    private static string OcrPng(TesseractEngine engine, byte[] png)
    {
        using var pix = Pix.LoadFromMemory(png);
        using var page = engine.Process(pix);
        return page.GetText() ?? string.Empty;
    }

    private TesseractEngine NewEngine()
    {
        if (!Directory.Exists(_tessData))
            throw new DirectoryNotFoundException($"Tesseract data path not found: {_tessData}");
        return new TesseractEngine(_tessData, _languages, EngineMode.Default);
    }
}
