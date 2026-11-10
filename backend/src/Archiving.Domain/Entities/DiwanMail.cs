using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>الديوان — the official register of EXTERNAL correspondence: letters coming from, or going to,
/// bodies outside the institution. Addressed to/from an external organization (not internal staff).
/// Soft-deleted to a recoverable folder; can be filed into the Documents archive.</summary>
public class DiwanMail : SoftDeleteEntity
{
    public long InstitutionId { get; set; }

    // ── Identity & routing ──
    public string RegisterNumber { get; set; } = string.Empty;         // auto: IN-{year}-{seq} | OUT-{year}-{seq}
    public string? ReferenceNumber { get; set; }                       // the external party's reference
    public DiwanDirection Direction { get; set; }

    // ── External party (from the external directory) ──
    public long? ExternalOrgId { get; set; }                           // ExternalOrg.Id
    public string? ExternalOrgName { get; set; }                       // snapshot for display/print
    public string? AddresseeName { get; set; }                         // اسم الموظف المخاطَب (outgoing)
    public string? AddresseeTitle { get; set; }                        // المسمى الوظيفي للمخاطَب

    // ── Content ──
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Body { get; set; }                                  // rich HTML
    public string? Keywords { get; set; }

    // ── Dates (UTC) ──
    public DateTime MailDate { get; set; } = DateTime.UtcNow;          // التاريخ (registered on)
    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
    public DateTime? IssueDate { get; set; }                           // تاريخ إصدار البريد
    public DateTime? SentDate { get; set; }                            // تاريخ الإرسال
    public DateTime? DueDate { get; set; }
    public DateTime? ResponseDeadline { get; set; }                    // تاريخ الرد على البريد
    public int? ResponsePeriodDays { get; set; }
    public bool RemindOnNoReply { get; set; }
    public DateTime? NoReplyNotifiedAt { get; set; }

    // ── Routing & references ──
    public long? CategoryId { get; set; }                              // تصنيف البريد (lookup)
    public long? ReferenceToId { get; set; }                           // مرجع الى — an earlier register entry
    // الى ديوان — receiving bureaus/departments. A letter may be routed to several at once,
    // so this is a collection rather than a single column (see DiwanBureau).

    // ── Classification & directed action ──
    public CorrespondenceActionType? ActionType { get; set; }
    public ConfidentialityLevel Classification { get; set; } = ConfidentialityLevel.Internal;
    public Priority Priority { get; set; } = Priority.Normal;
    public CorrespondenceStatus Status { get; set; } = CorrespondenceStatus.Registered;

    // ── Archive linkage ──
    public string? FileNumber { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public long? ArchivedDocumentId { get; set; }                      // the Document created on filing

    // ── Linking (reply/reference between register entries) ──
    public long? ReplyToId { get; set; }
    public long? ParentId { get; set; }

    public MailCategory? Category { get; set; }
    /// <summary>The linked directory entry. Null when the party was typed free-hand
    /// (<see cref="ExternalOrgName"/> still holds the snapshot in that case).</summary>
    public ExternalOrganization? ExternalOrg { get; set; }
    public ICollection<DiwanAttachment> Attachments { get; set; } = new List<DiwanAttachment>();
    /// <summary>الى ديوان — every bureau this letter is routed to.</summary>
    public ICollection<DiwanBureau> ToDiwanOrgUnits { get; set; } = new List<DiwanBureau>();
}

/// <summary>الى ديوان — one receiving bureau on a register entry. A letter can be routed to
/// several departments at once, so the link is many-to-many via this row.</summary>
public class DiwanBureau : BaseEntity
{
    public long DiwanMailId { get; set; }
    public long OrgUnitId { get; set; }

    public DiwanMail DiwanMail { get; set; } = null!;
    public OrgUnit OrgUnit { get; set; } = null!;
}

/// <summary>
/// One member of staff let in on one external letter.
///
/// Routing a letter الى ديوان addresses it to a *department*, not to everybody in it. The head
/// answers for the department's external correspondence and decides who handles which letter, so
/// arrival grants the head sight of it and nothing more; each colleague who needs to see it is
/// named here. Without this row an employee reads only the letters they registered themselves.
///
/// This is deliberately narrower than converting the letter into an internal message: a grant
/// lets someone read the register entry in place, leaving one authoritative copy of it.
/// </summary>
public class DiwanAccess : BaseEntity
{
    public long DiwanMailId { get; set; }
    public long UserId { get; set; }

    /// <summary>The head who let them in, kept on the row the scope actually reads so the
    /// authority for the access survives beside it.</summary>
    public long? GrantedByUserId { get; set; }
    public string? Note { get; set; }

    public DiwanMail DiwanMail { get; set; } = null!;
    public User User { get; set; } = null!;
}

/// <summary>تصنيف البريد — admin-managed category of a letter (عام، إبداء رأي، تدريب، شكوى…).
/// Distinct from درجة السرية (ConfidentialityLevel), which is the security classification.</summary>
/// <summary>Which register a mail type belongs to. External types describe letters exchanged with
/// outside bodies (مطالبة مالية); internal types describe what staff send each other
/// (قسيمة راتب، سند قبض، سند دفع). A type marked Both is offered on either side.</summary>
public enum MailCategoryScope
{
    External = 0,
    Internal = 1,
    Both = 2,
}

/// <summary>تصنيف البريد — the kind of letter, used both to classify a register entry and to pick
/// the letterhead it prints on (see <see cref="ReportTemplate.MailCategoryId"/>).</summary>
public class MailCategory : BaseEntity
{
    public long InstitutionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public MailCategoryScope Scope { get; set; } = MailCategoryScope.External;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    /// <summary>Whether this type may be used on the given register.</summary>
    public bool AppliesTo(MailCategoryScope side) => Scope == MailCategoryScope.Both || Scope == side;
}

/// <summary>A file attached to a Diwan register entry (stored via IFileStorage).</summary>
public class DiwanAttachment : BaseEntity
{
    public long DiwanMailId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string? Checksum { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
    public int Version { get; set; } = 1;
    public string? OcrText { get; set; }
    public CorrespondenceOcrStatus OcrStatus { get; set; } = CorrespondenceOcrStatus.Pending;

    public DiwanMail DiwanMail { get; set; } = null!;
}
