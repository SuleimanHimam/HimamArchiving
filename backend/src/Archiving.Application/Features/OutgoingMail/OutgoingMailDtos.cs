using Archiving.Domain.Enums;

namespace Archiving.Application.Features.OutgoingMail;

public sealed record OutgoingMailListItem(
    long Id,
    string LetterNumber,
    string RecipientEntity,
    string Subject,
    string Confidentiality,
    string Priority,
    string Status,
    DateTime? SentDate,
    DateTime CreatedAt);

public sealed record OutgoingMailDetail(
    long Id,
    string LetterNumber,
    string RecipientEntity,
    string? RecipientName,
    string Subject,
    string? Body,
    long? LetterTemplateId,
    long? SignatoryPositionId,
    string Confidentiality,
    string Priority,
    string Status,
    DateTime? SentDate,
    long? InReplyToIncomingMailId,
    long? ApprovedBy,
    DateTime? ApprovedAt,
    DateTime CreatedAt,
    IReadOnlyList<OutgoingMailTimelineEntry> Timeline);

public sealed record OutgoingMailTimelineEntry(long Id, string Action, long? UserId, DateTime At, string? Note);

public sealed record CreateOutgoingMailRequest(
    string RecipientEntity,
    string? RecipientName,
    string Subject,
    string? Body,
    long? LetterTemplateId,
    long? SignatoryPositionId,
    ConfidentialityLevel Confidentiality,
    Priority Priority,
    long? InReplyToIncomingMailId);

public sealed record UpdateOutgoingMailRequest(
    string RecipientEntity,
    string? RecipientName,
    string Subject,
    string? Body,
    long? LetterTemplateId,
    long? SignatoryPositionId,
    ConfidentialityLevel Confidentiality,
    Priority Priority);

/// <summary>Lifecycle actions: submit a draft for approval, approve it, dispatch (auto-archives), or archive.</summary>
public enum OutgoingMailActionType { SubmitForApproval, Approve, Send, Archive }

public sealed record OutgoingMailActionRequest(OutgoingMailActionType Action, string? Note);

public sealed record OutgoingMailQuery(
    string? Search,
    OutgoingMailStatus? Status,
    int Page = 1,
    int PageSize = 20);
