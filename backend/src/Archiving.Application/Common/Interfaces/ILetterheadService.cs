using Archiving.Application.Common.Models;
using Archiving.Application.Features.Letterhead;

namespace Archiving.Application.Common.Interfaces;

/// <summary>
/// Slot-based letterhead for outgoing Diwan correspondence.
///
/// Templates no longer own the page. <see cref="ILetterheadRenderer"/> owns one fixed master
/// layout; this service decides which slot values are allowed to fill it, by resolving the three
/// levels (Organization → Department → User) for the letter's <em>author</em> — not for whoever
/// happens to be at the printer.
/// </summary>
public interface ILetterheadService
{
    /// <summary>Merge the three levels for an author. Only Active department slots are used, so a
    /// pending edit never reaches an external recipient.</summary>
    Task<ResolvedLetterhead> ResolveAsync(long? userId, long? orgUnitId, CancellationToken ct = default);

    /// <summary>Read the slots stored at one level (for the designer). Missing rows come back as
    /// empty slots rather than an error — an unconfigured department simply inherits.</summary>
    Task<OrgSlots> GetOrgSlotsAsync(CancellationToken ct = default);
    Task<DepartmentSlots> GetDepartmentSlotsAsync(long orgUnitId, CancellationToken ct = default);
    Task<SignerSlots> GetSignerSlotsAsync(long userId, CancellationToken ct = default);

    /// <summary>Organization Admin only. Applies immediately — org slots are the locked baseline,
    /// there is nobody above them to approve.</summary>
    Task<Result<OrgSlots>> SaveOrgSlotsAsync(OrgSlots slots, CancellationToken ct = default);

    /// <summary>Saves as PendingApproval and starts the approval workflow. The live letterhead is
    /// unchanged until the workflow completes.</summary>
    Task<Result<DepartmentSlots>> SaveDepartmentSlotsAsync(
        long orgUnitId, DepartmentSlots slots, CancellationToken ct = default);

    /// <summary>A user's own signature block. No approval — it changes nothing but their name.</summary>
    Task<Result<SignerSlots>> SaveSignerSlotsAsync(
        long userId, SignerSlots slots, CancellationToken ct = default);

    /// <summary>Render a real outgoing Diwan letter to PDF.</summary>
    Task<Result<byte[]>> RenderDiwanLetterAsync(long diwanMailId, CancellationToken ct = default);

    /// <summary>Render a preview from unsaved slots and/or unsaved letter content — the same
    /// renderer as the real thing, which is what makes the on-screen preview trustworthy.</summary>
    Task<Result<byte[]>> RenderPreviewAsync(PreviewLetterheadRequest request, CancellationToken ct = default);

    /// <summary>Legacy designs the migration could not map onto slots, for manual admin review.</summary>
    Task<IReadOnlyList<LetterheadReviewItem>> ListNeedsReviewAsync(CancellationToken ct = default);
}

/// <summary>The one fixed master layout. Kept separate from the service so it can be unit-tested
/// on slot values alone, with no database in the way.</summary>
public interface ILetterheadRenderer
{
    byte[] Render(ResolvedLetterhead letterhead, LetterContent content, string institutionName);
}
