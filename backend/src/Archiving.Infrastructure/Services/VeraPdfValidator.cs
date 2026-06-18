using System.Diagnostics;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Preservation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Services;

/// <summary>Validates PDF/A conformance by invoking the veraPDF CLI (the industry-standard,
/// open-source PDF/A validator). Path via <c>Preservation:VeraPdfPath</c>. If veraPDF isn't
/// configured/installed, validation is reported as not performed (the copy is still generated).</summary>
public sealed class VeraPdfValidator(IConfiguration config, ILogger<VeraPdfValidator> logger) : IPdfaValidator
{
    public async Task<PdfaValidationResult> ValidateAsync(byte[] pdfBytes, string flavour = "2b", CancellationToken ct = default)
    {
        var exe = config["Preservation:VeraPdfPath"];
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            return new PdfaValidationResult(false, "أداة التحقق veraPDF غير مُهيّأة (Preservation:VeraPdfPath)");

        var tmp = Path.Combine(Path.GetTempPath(), $"pdfa-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(tmp, pdfBytes, ct);
        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--flavour");
            psi.ArgumentList.Add(flavour);
            psi.ArgumentList.Add(tmp);

            using var p = Process.Start(psi)!;
            var stdout = await p.StandardOutput.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);

            // veraPDF: exit 0 = all compliant, 1 = non-compliant, >1 = tool error.
            var compliant = p.ExitCode == 0
                || stdout.Contains("isCompliant=\"true\"", StringComparison.OrdinalIgnoreCase);
            return new PdfaValidationResult(compliant,
                compliant ? "مطابق لـ PDF/A (veraPDF)" : "غير مطابق لـ PDF/A (veraPDF)");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "veraPDF validation could not run");
            return new PdfaValidationResult(false, "تعذّر تشغيل veraPDF");
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* best effort */ }
        }
    }
}
