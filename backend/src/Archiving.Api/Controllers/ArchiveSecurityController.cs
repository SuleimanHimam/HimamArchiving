using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Infrastructure.Security;
using Archiving.Infrastructure.Security.KeyManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>
/// Encryption-facing endpoints: the authorised retrieval path, the KMS health probe, and key rotation.
/// </summary>
[ApiController]
[Route("api/archive")]
[Authorize]
public sealed class ArchiveSecurityController(
    ISecureArchiveReader reader,
    IKeyManagementService kms,
    IKeyRotationService rotation,
    IAuditWriter audit) : ControllerBase
{
    /// <summary>
    /// Streams a decrypted attachment to an authorised caller.
    ///
    /// <para>The permission attribute is the coarse gate; <see cref="ISecureArchiveReader"/> applies row
    /// scope, clearance and destruction state, and only then unwraps the key. Plaintext is produced
    /// frame by frame as the response is written — the document is never assembled in memory or spooled
    /// to a temporary file, so a large archived file costs one 1 MiB buffer rather than its own size.</para>
    /// </summary>
    [HttpGet("documents/{documentId:long}/attachments/{attachmentId:long}")]
    [HasPermission("Documents.View")]
    public async Task<IActionResult> Download(long documentId, long attachmentId, CancellationToken ct)
    {
        var result = await reader.OpenAttachmentAsync(documentId, attachmentId, ct);
        if (!result.Succeeded) return NotFound(new { error = result.Error });

        var doc = result.Value!;

        // No range processing: the container is forward-only, and serving a range would mean decrypting
        // frames the caller did not ask for. Content-Length is the plaintext size from the index.
        Response.Headers.ContentLength = doc.SizeBytes;
        Response.Headers.CacheControl = "no-store";      // archived documents must not sit in a shared cache
        Response.Headers.XContentTypeOptions = "nosniff";

        return File(doc.Content, doc.ContentType, doc.FileName, enableRangeProcessing: false);
    }

    /// <summary>KMS liveness — provider, active KEK version, and whether a wrap/unwrap round trip works.
    /// Administrator-only: the KEK version identifier is operational detail, not public.</summary>
    [HttpGet("encryption/health")]
    [HasPermission("Settings.Edit")]
    public async Task<IActionResult> EncryptionHealth(CancellationToken ct)
    {
        var health = await kms.CheckHealthAsync(ct);
        var usage = await rotation.KeyUsageAsync(ct);

        return Ok(new
        {
            healthy = health.Healthy,
            provider = health.Provider,
            activeKekId = health.ActiveKekId,
            knownKeyVersions = health.KnownKeyVersions,
            objectsPerKek = usage,
            error = health.Error,
        });
    }

    /// <summary>
    /// Rotates the key encryption key on demand, re-wrapping every live data key under the new version.
    ///
    /// <para>Archived bytes are not rewritten, so this is safe to run during business hours on a large
    /// archive. It is still a privileged, audited action: it invalidates nothing, but a failed rotation
    /// leaves key material split across versions and an operator needs to know it happened.</para>
    /// </summary>
    [HttpPost("encryption/rotate")]
    [HasPermission("Settings.Edit")]
    public async Task<IActionResult> Rotate(CancellationToken ct)
    {
        var report = await rotation.RotateAsync(ct);
        await audit.WriteAsync("KekRotated", "Encryption", 0,
            $"kek={report.NewKekId}", newValues: $"rewrapped={report.Rewrapped}; failed={report.Failed}", ct: ct);

        return Ok(new
        {
            newKekId = report.NewKekId,
            rewrapped = report.Rewrapped,
            failed = report.Failed,
            skippedShredded = report.SkippedShredded,
            durationSeconds = Math.Round(report.Duration.TotalSeconds, 1),
        });
    }

    /// <summary>Finishes a rotation that was interrupted, without minting another KEK version.</summary>
    [HttpPost("encryption/rotate/resume")]
    [HasPermission("Settings.Edit")]
    public async Task<IActionResult> ResumeRotation(CancellationToken ct)
    {
        var report = await rotation.ResumeAsync(ct);
        await audit.WriteAsync("KekRotationResumed", "Encryption", 0,
            $"kek={report.NewKekId}", newValues: $"rewrapped={report.Rewrapped}; failed={report.Failed}", ct: ct);

        return Ok(new { report.NewKekId, report.Rewrapped, report.Failed });
    }
}
