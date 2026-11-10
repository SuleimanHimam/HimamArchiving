using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace Archiving.Api.Controllers;

/// <summary>What a person holding the paper is told about it.</summary>
public sealed record VerificationResultDto(
    bool Valid,
    string? DocumentNumber,
    string? TypeName,
    string? OrgUnitName,
    DateOnly? DocumentDate,
    DateTime? RegisteredAt,
    string? Status,
    /// <summary>Only for a document classified public; everything else is verified without being read out.</summary>
    string? Title,
    DateTime CheckedAt);

/// <summary>
/// «هل هذه الورقة من هذا النظام؟» — the public side of the QR printed on a document.
///
/// Anonymous by necessity: whoever is holding the paper is usually not a user of the system, and
/// the whole point of the code is that they can check it without one. That shapes what is said
/// back. The answer confirms the paper against the register — its number, its kind, the department
/// that issued it, its date and whether it is still current — and stops there. The title is read
/// out only when the document is classified public; for anything else, confirming existence is the
/// service being offered, not disclosing content.
///
/// The code itself is the protection: ten characters from an unambiguous alphabet, drawn from a
/// cryptographic RNG and unrelated to the document's id, so the endpoint cannot be walked by
/// counting and a guess is worth 32^10.
/// </summary>
[ApiController]
[Route("api/public/verify")]
[AllowAnonymous]
public sealed class VerificationController(AppDbContext db, IConfiguration config) : ControllerBase
{
    [HttpGet("{code}")]
    public async Task<IActionResult> Verify(string code, CancellationToken ct)
    {
        var normalized = DocumentVerification.Normalize(code);
        if (normalized is null) return Ok(NotValid());

        var doc = await db.Documents.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.VerificationCode == normalized)
            .Select(d => new
            {
                d.DocumentNumber,
                TypeName = d.DocumentType.Name,
                OrgUnitName = d.OwningOrgUnit.Name,
                d.DocumentDate,
                d.CreatedAt,
                d.Status,
                d.Confidentiality,
                d.Title,
                d.IsDeleted,
            })
            .FirstOrDefaultAsync(ct);

        // A deleted document is not a forged one: the paper did come from here, and saying so while
        // reporting the status is more honest than a bare "unknown code".
        if (doc is null) return Ok(NotValid());

        return Ok(new VerificationResultDto(
            Valid: true,
            DocumentNumber: doc.DocumentNumber,
            TypeName: doc.TypeName,
            OrgUnitName: doc.OrgUnitName,
            DocumentDate: doc.DocumentDate,
            RegisteredAt: doc.CreatedAt,
            Status: doc.IsDeleted ? "Deleted" : doc.Status.ToString(),
            Title: doc.Confidentiality == ConfidentialityLevel.Public ? doc.Title : null,
            CheckedAt: DateTime.UtcNow));
    }

    /// <summary>
    /// The QR itself, rendered where the code lives rather than in the browser.
    ///
    /// Server-side so that every surface prints the same square: the screen, a browser print, and a
    /// PDF built by the server all embed one image URL. It encodes the verification page's address
    /// on the host the request actually arrived on, so a system reached at a LAN address and one
    /// reached through a proxy each produce a QR that resolves for the person scanning it.
    /// </summary>
    [HttpGet("{code}/qr.png")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public IActionResult Qr(string code, [FromQuery] int size = 8)
    {
        var normalized = DocumentVerification.Normalize(code);
        if (normalized is null) return NotFound();

        // 4..16 pixels per module: below that a phone camera struggles, above it the PNG is pointless.
        var pixelsPerModule = Math.Clamp(size, 4, 16);
        var target = $"{PublicBaseUrl()}/verify/{normalized}";

        using var generator = new QRCodeGenerator();
        // Q correction: the square is printed on paper that gets stamped, folded and photocopied.
        using var data = generator.CreateQrCode(target, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(pixelsPerModule);
        return File(png, "image/png");
    }

    private static VerificationResultDto NotValid() =>
        new(false, null, null, null, null, null, null, null, DateTime.UtcNow);

    /// <summary>
    /// The address the QR must point at, which is not always the one the request carries: behind the
    /// IIS reverse proxy the Host header is rewritten to loopback, and a QR encoding 127.0.0.1
    /// works on the server and nowhere else. Same reasoning as the desktop-shortcut download.
    /// </summary>
    private string PublicBaseUrl()
    {
        // An explicit setting wins over anything derived from the request. The packaged deployment
        // serves the SPA and the API from one origin, where the request host is right — but a split
        // deployment (and every dev machine, where the SPA is on 5173 and the API on 5105) would
        // otherwise print a QR pointing at the API port, which serves no page to land on.
        var configured = config["App:PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured)) return configured.TrimEnd('/');

        var host = First(Request.Headers["X-Forwarded-Host"]) ?? First(Request.Headers["X-Original-Host"]);
        var proto = First(Request.Headers["X-Forwarded-Proto"]);

        if (host is null || !IsSafeHost(host))
        {
            host = Request.Host.Value;
            proto = Request.Scheme;
        }
        if (proto is not ("http" or "https")) proto = Request.Scheme;
        return $"{proto}://{host}";
    }

    private static string? First(Microsoft.Extensions.Primitives.StringValues v)
    {
        var s = v.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(s)) return null;
        var head = s.Split(',')[0].Trim();
        return head.Length == 0 ? null : head;
    }

    /// <summary>A host header is caller-controlled, so only a plain host[:port] is trusted into a URL.</summary>
    private static bool IsSafeHost(string host) =>
        host.Length <= 255 && System.Text.RegularExpressions.Regex.IsMatch(host, @"^[A-Za-z0-9._\-\[\]:]+$");
}
