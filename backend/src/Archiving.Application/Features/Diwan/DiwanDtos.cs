using Archiving.Application.Features.InternalMail;
using Archiving.Domain.Enums;

namespace Archiving.Application.Features.Diwan;

public sealed record DiwanAttachmentDto(
    long Id, string StorageKey, string FileName, string? ContentType, long SizeBytes, int Version, string OcrStatus);

/// <summary>تصنيف البريد lookup row.</summary>
public sealed record MailCategoryDto(long Id, string Name, string? NameEn, int SortOrder, string Scope, bool IsActive);

/// <summary>Create/update payload for a mail type. <paramref name="Scope"/> is
/// 0 External · 1 Internal · 2 Both.</summary>
public sealed record SaveMailCategoryRequest(string Name, string? NameEn, int Scope, int SortOrder, bool IsActive = true);

public sealed record DiwanListItemDto(
    long Id,
    string RegisterNumber,
    string? ReferenceNumber,
    string Direction,               // Incoming | Outgoing
    string Subject,
    string? ExternalOrgName,
    /// <summary>The person the letter is addressed to. A صادر letter is read by who it went to, so
    /// the register names them beside the body they belong to.</summary>
    string? AddresseeName,
    string Classification,
    string Priority,
    string Status,
    string? ActionType,
    string? CategoryName,
    DateTime MailDate,
    DateTime RegistrationDate,
    DateTime? DueDate,
    DateTime? ResponseDeadline,
    bool IsArchived,
    bool HasAttachments,
    /// <summary>تصنيف البريد, so a row can open the designer scoped to its own letterhead.</summary>
    long? CategoryId = null);

public sealed record DiwanDetailDto(
    long Id,
    string RegisterNumber,
    string? ReferenceNumber,
    string Direction,
    long? ExternalOrgId,
    string? ExternalOrgName,
    string? AddresseeName,
    string? AddresseeTitle,
    string Subject,
    string? Description,
    string? Body,
    string? Keywords,
    string Classification,
    string Priority,
    string Status,
    string? ActionType,
    int? ResponsePeriodDays,
    bool RemindOnNoReply,
    string? FileNumber,
    bool IsArchived,
    long? ArchivedDocumentId,
    DateTime MailDate,
    DateTime RegistrationDate,
    DateTime? IssueDate,
    DateTime? SentDate,
    DateTime? DueDate,
    DateTime? ResponseDeadline,
    long? CategoryId,
    string? CategoryName,
    long? ReferenceToId,
    string? ReferenceToNumber,
    IReadOnlyList<OrgUnitRefDto> ToDiwanOrgUnits,
    IReadOnlyList<DiwanAttachmentDto> Attachments,
    /// <summary>Who registered/wrote the letter, and the department they belong to. Printing resolves
    /// the letterhead from these — a letter keeps its author's and department's design no matter who
    /// later sends it to the printer.</summary>
    long? CreatedByUserId = null,
    long? CreatedByOrgUnitId = null);

/// <summary>A referenced org unit (الى ديوان routing target).</summary>
public sealed record OrgUnitRefDto(long Id, string Name);

/// <summary>One person who can open a letter, and what entitles them to.
/// <paramref name="Reason"/> is <c>Granted</c> — named by the head, and revocable — or
/// <c>Registrar</c> / <c>Head</c>, which come with the letter itself and cannot be taken away
/// here. Showing the second kind is the point: a head choosing who to let in has to see who is
/// already in, or they will grant access that was never theirs to withhold.</summary>
public sealed record DiwanAccessDto(
    long UserId, string FullName, string? OrgUnitName, string Reason, string? Note);

/// <summary>A member of staff the caller may let in — those in the departments they head.</summary>
public sealed record DiwanAccessCandidateDto(long UserId, string FullName, string? OrgUnitName);

/// <summary>من يطّلع على البريد — the access panel for one external letter, in one round trip.</summary>
public sealed record DiwanAccessViewDto(
    bool CanManage,
    IReadOnlyList<DiwanAccessDto> Entries,
    IReadOnlyList<DiwanAccessCandidateDto> Candidates);

/// <summary>Replaces the letter's grant list wholesale — whoever is not named loses access.
/// Access that comes with the letter (its registrar, the heads) is untouched by this.</summary>
public sealed record SetDiwanAccessRequest(IReadOnlyList<long> UserIds, string? Note);

/// <summary>Register or compose a Diwan letter. Direction distinguishes وارد (incoming, from an external
/// party) from صادر (outgoing, to an external party).</summary>
public sealed record SaveDiwanRequest(
    DiwanDirection Direction,
    string Subject,
    string? ReferenceNumber,
    long? ExternalOrgId,
    string? ExternalOrgName,
    string? AddresseeName,
    string? AddresseeTitle,
    ConfidentialityLevel Classification,
    Priority Priority,
    DateTime MailDate,
    DateTime? IssueDate,
    DateTime? SentDate,
    DateTime? DueDate,
    DateTime? ResponseDeadline,
    int? ResponsePeriodDays,
    bool RemindOnNoReply,
    CorrespondenceActionType? ActionType,
    long? CategoryId,
    long? ReferenceToId,
    /// <summary>الى ديوان — one or more receiving bureaus.</summary>
    IReadOnlyList<long>? ToDiwanOrgUnitIds,
    string? Description,
    string? Body,
    string? Keywords,
    string? FileNumber);

/// <summary>Reply to a register entry. The reply is filed in the opposite direction and is linked to the
/// original through ReplyToId / ParentId / ReferenceToId, so المرجع resolves to the letter being answered.</summary>
public sealed record ReplyDiwanRequest(
    string Subject,
    string? Body,
    ConfidentialityLevel Classification,
    Priority Priority,
    DateTime? MailDate,
    DateTime? DueDate,
    DateTime? ResponseDeadline,
    IReadOnlyList<long>? ToDiwanOrgUnitIds);

/// <summary>Convert a Diwan register entry into an internal message (مراسلة داخلية) addressed to staff,
/// departments or groups. The register entry stays in place; the internal copy cites it as its reference.</summary>
public sealed record ConvertToInternalRequest(
    string? Subject,
    string? Body,
    /// <summary>ملاحظات — a short note carried onto the internal message's description.</summary>
    string? Notes,
    ConfidentialityLevel? Classification,
    Priority? Priority,
    CorrespondenceActionType? ActionType,
    DateTime? DueDate,
    DateTime? ResponseDeadline,
    /// <summary>When true the register entry's files are copied onto the internal message.</summary>
    bool CopyAttachments,
    IReadOnlyList<RecipientInput> Recipients);

/// <summary>حالة البريد — move a register entry to another status, with an optional note kept in the audit trail.</summary>
public sealed record ChangeDiwanStatusRequest(
    CorrespondenceStatus Status,
    string? Note,
    /// <summary>Date the change takes effect; recorded in the audit entry (defaults to now).</summary>
    DateTime? EffectiveDate);

/// <summary>One letter in a register chain — the original plus every reply and referencing entry.</summary>
public sealed record DiwanThreadNodeDto(
    long Id,
    string RegisterNumber,
    string? ReferenceNumber,
    string Subject,
    string Direction,
    string Status,
    /// <summary>Root | Reply | Reference — how this entry attaches to the chain.</summary>
    string Relation,
    DateTime MailDate,
    string? ExternalOrgName,
    string? Body,
    bool HasAttachments);

public sealed record DiwanListFilter(
    string? Folder,                 // all | incoming | outgoing | drafts | deleted
    DiwanDirection? Direction,
    CorrespondenceStatus? Status,
    ConfidentialityLevel? Classification,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 25);
