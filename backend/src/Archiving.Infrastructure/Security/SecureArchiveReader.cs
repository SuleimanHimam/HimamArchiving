using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Security;

/// <summary>A decrypted document ready to stream to the caller. The stream owns the file handle and the
/// key material; dispose it to close and zero both.</summary>
public sealed record DecryptedDocument(Stream Content, string FileName, string ContentType, long SizeBytes, string ChecksumSha256);

public interface ISecureArchiveReader
{
    Task<Result<DecryptedDocument>> OpenAttachmentAsync(long documentId, long attachmentId, CancellationToken ct = default);
}

/// <summary>
/// The retrieval gate: <b>every</b> authorisation decision happens here, before a data key is unwrapped.
///
/// <para>Order matters and is deliberate — authenticated, then permission (RBAC), then row visibility and
/// clearance (ABAC), then destruction state, and only then the KMS call. Unwrapping first and checking
/// afterwards would put plaintext in memory for a request that was never entitled to it, and would leave a
/// KMS audit trail of key access for documents nobody was allowed to read.</para>
///
/// <para>Refusals are deliberately uniform. A caller who may not see a document and a caller asking for one
/// that does not exist get the same answer, because distinguishing them turns this endpoint into an oracle
/// for enumerating the archive — and a crypto-shredded document must be indistinguishable from one that was
/// never there.</para>
/// </summary>
public sealed class SecureArchiveReader(
    AppDbContext db,
    IFileStorage storage,
    ICurrentUser currentUser,
    IAccessScope scope,
    IAuditWriter audit,
    ILogger<SecureArchiveReader> logger) : ISecureArchiveReader
{
    private const string NotFound = "الوثيقة غير موجودة أو لا تملك صلاحية الوصول إليها";

    public async Task<Result<DecryptedDocument>> OpenAttachmentAsync(long documentId, long attachmentId, CancellationToken ct = default)
    {
        // 1 ── Authenticated?
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
            return Result<DecryptedDocument>.Fail(NotFound);

        // 2 ── RBAC: does this role read documents at all?
        if (!await scope.HasPermissionAsync("Documents.View", ct).ConfigureAwait(false))
            return await DenyAsync(documentId, "permission", ct).ConfigureAwait(false);

        var attachment = await db.DocumentAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.DocumentId == documentId, ct).ConfigureAwait(false);
        if (attachment is null) return Result<DecryptedDocument>.Fail(NotFound);

        var document = await db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, ct).ConfigureAwait(false);
        if (document is null) return Result<DecryptedDocument>.Fail(NotFound);

        // 3 ── ABAC: is this particular row within the caller's scope?
        if (!await IsVisibleAsync(document, ct).ConfigureAwait(false))
            return await DenyAsync(documentId, "scope", ct).ConfigureAwait(false);

        // 4 ── ABAC: does the caller's clearance meet the document's confidentiality?
        if (currentUser.Clearance < document.Confidentiality)
            return await DenyAsync(documentId, "clearance", ct).ConfigureAwait(false);

        // 5 ── Destroyed content is gone, and says so no more loudly than a missing one.
        if (attachment.ContentDestroyed || document.IsTombstone)
            return Result<DecryptedDocument>.Fail(NotFound);

        // 6 ── Only now is a key unwrapped and plaintext produced.
        Stream? content;
        try
        {
            content = await storage.OpenAsync(attachment.StorageKey, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Tampering, a malformed container, an unreachable KMS — the caller learns none of it. The
            // detail goes to the log, where it is a fixity incident for an operator to act on.
            logger.LogError(ex, "Decryption failed for attachment {AttachmentId} of document {DocumentId}",
                attachmentId, documentId);
            await audit.WriteAsync("DecryptionFailed", "Documents", documentId, attachment.FileName, ct: ct).ConfigureAwait(false);
            return Result<DecryptedDocument>.Fail("تعذّر فتح الملف. تم إبلاغ مسؤول النظام.");
        }

        if (content is null) return Result<DecryptedDocument>.Fail(NotFound);

        await audit.WriteAsync("Download", "Documents", documentId, attachment.FileName, ct: ct).ConfigureAwait(false);

        return Result<DecryptedDocument>.Ok(new DecryptedDocument(
            content,
            StorageKeys.SafeDisplayName(attachment.FileName),
            string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType,
            attachment.SizeBytes,
            attachment.Checksum ?? string.Empty));
    }

    private async Task<bool> IsVisibleAsync(Document document, CancellationToken ct)
    {
        if (await scope.SeesAllDocumentsAsync(ct).ConfigureAwait(false)) return true;
        if (document.CreatedBy == currentUser.UserId) return true;

        var units = await scope.OrgUnitIdsAsync(ct).ConfigureAwait(false);
        if (document.OwningOrgUnitId is { } owner && units.Contains(owner)) return true;

        return await db.DocumentShares
            .AnyAsync(s => s.DocumentId == document.Id && s.SharedWithUserId == currentUser.UserId, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Records the refusal with its real reason and returns the uniform message. Denials are worth
    /// more in the audit log than grants — this is where credential misuse shows up first.</summary>
    private async Task<Result<DecryptedDocument>> DenyAsync(long documentId, string reason, CancellationToken ct)
    {
        logger.LogWarning("Access denied ({Reason}) to document {DocumentId} for user {UserId}",
            reason, documentId, currentUser.UserId);
        await audit.WriteAsync("AccessDenied", "Documents", documentId, reason, ct: ct).ConfigureAwait(false);
        return Result<DecryptedDocument>.Fail(NotFound);
    }
}
