namespace Archiving.Application.Common.Interfaces;

/// <summary>Result of pulling searchable text out of a stored file.</summary>
/// <param name="Text">Extracted text (may be empty if the file has none).</param>
/// <param name="Source">"Embedded" (PDF text layer), "OCR" (recognized from images), or "None".</param>
public readonly record struct TextExtractionResult(string? Text, string Source);

/// <summary>
/// Extracts searchable text from a file: the embedded text layer of digital PDFs,
/// or OCR (Arabic + English) for scanned PDFs and image files.
/// </summary>
public interface ITextExtractionService
{
    /// <summary>True for file types this service can extract text from (pdf + images).</summary>
    bool CanExtract(string fileExtension);

    Task<TextExtractionResult> ExtractAsync(byte[] fileBytes, string fileExtension, CancellationToken ct = default);
}
