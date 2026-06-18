using Archiving.Application.Features.Preservation;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Validates that a document conforms to PDF/A (ISO 19005). Backed by veraPDF when configured.</summary>
public interface IPdfaValidator
{
    Task<PdfaValidationResult> ValidateAsync(byte[] pdfBytes, string flavour = "2b", CancellationToken ct = default);
}
