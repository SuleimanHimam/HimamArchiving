using Archiving.Domain.Enums;

namespace Archiving.Application.Features.InternalMail;

public sealed record InternalRecipientDto(
    long Id, string Addressing, string TargetKind, long? TargetId, string? TargetName);

public sealed record InternalAttachmentDto(
    long Id, string StorageKey, string FileName, string? ContentType, long SizeBytes, int Version, string OcrStatus);

public sealed record InternalReadDto(
    long UserId, string? UserName, string ReadStatus, DateTime? DeliveredAt, DateTime? FirstReadAt, DateTime? LastReadAt);

public sealed record InternalThreadNodeDto(
    long Id, string MailNumber, string Subject, string Status, string LinkType,
    DateTime MailDate, string? SenderName, string? Body);

public sealed record InternalListItemDto(
    long Id,
    string MailNumber,
    string? ReferenceNumber,
    string Subject,
    string? SenderName,
    string Classification,
    string Priority,
    string Status,
    string? ActionType,
    long? CategoryId,
    string? CategoryName,
    DateTime MailDate,
    DateTime RegistrationDate,
    DateTime? DueDate,
    DateTime? ResponseDeadline,
    bool IsRead,
    bool HasAttachments,
    /// <summary>Direction as this reader sees it: صادر when they sent it, وارد otherwise. It is a
    /// property of the pair (message, reader), not of the message — the same message is outgoing to
    /// its author and incoming to everyone it reached, which is why it is computed per query and
    /// not stored.</summary>
    bool IsOutgoing,
    /// <summary>Who it was addressed to (the To line; Cc and Bcc are not summarised here).
    /// The sent folder is listed by recipient — "from" there is always the person reading it.</summary>
    IReadOnlyList<string>? RecipientNames = null,
    /// <summary>Set when this row is part of a workflow run — the inbox is the workflow inbox, so the
    /// route, the step and «بانتظار إجرائك» are columns here rather than a separate screen.</summary>
    Archiving.Application.Features.Workflow.WorkflowMailBadge? Workflow = null);

public sealed record InternalDetailDto(
    long Id,
    string MailNumber,
    string? ReferenceNumber,
    long SenderUserId,
    string? SenderName,
    string Subject,
    string? Description,
    string? Body,
    string? Keywords,
    string Classification,
    string Priority,
    string Status,
    string? ActionType,
    long? CategoryId,
    string? CategoryName,
    int? ResponsePeriodDays,
    bool RemindOnNoReply,
    string? FileNumber,
    bool IsArchived,
    long? ArchivedDocumentId,
    DateTime MailDate,
    DateTime RegistrationDate,
    DateTime? DueDate,
    DateTime? ResponseDeadline,
    long? ReplyToId,
    long? ParentId,
    IReadOnlyList<InternalRecipientDto> Recipients,
    IReadOnlyList<InternalAttachmentDto> Attachments);

public sealed record RecipientInput(
    CorrespondenceAddressing Addressing,
    RecipientKind TargetKind,           // Employee | Department | Group
    long? TargetId,
    string? TargetName);

public sealed record ComposeInternalRequest(
    string Subject,
    string? ReferenceNumber,
    ConfidentialityLevel Classification,
    Priority Priority,
    DateTime? DueDate,
    DateTime? ResponseDeadline,
    int? ResponsePeriodDays,
    bool RemindOnNoReply,
    CorrespondenceActionType? ActionType,
    long? CategoryId,                   // تصنيف البريد — قسيمة راتب / سند قبض / سند دفع …
    string? Description,
    string? Body,
    string? Keywords,
    string? FileNumber,
    IReadOnlyList<RecipientInput> Recipients);

public sealed record ReplyInternalRequest(
    string Subject,
    string? Body,
    ConfidentialityLevel Classification,
    Priority Priority,
    bool ReplyAll,
    IReadOnlyList<RecipientInput>? AdditionalRecipients);

public sealed record ForwardInternalRequest(
    string? Body,
    IReadOnlyList<RecipientInput> Recipients);

public sealed record InternalListFilter(
    string? Folder,                 // all | inbox | sent | drafts | deleted
    CorrespondenceStatus? Status,
    ConfidentialityLevel? Classification,
    DateTime? From,
    DateTime? To,
    int Page = 1,
    int PageSize = 25);
