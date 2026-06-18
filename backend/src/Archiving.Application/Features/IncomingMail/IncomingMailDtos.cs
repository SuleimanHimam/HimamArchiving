using Archiving.Domain.Enums;

namespace Archiving.Application.Features.IncomingMail;

public sealed record IncomingMailListItem(
    long Id,
    string TransactionNumber,
    string SenderEntity,
    string Subject,
    string Confidentiality,
    string Priority,
    string Status,
    DateOnly ReceivedDate,
    long? AssignedToPositionId,
    long? AssignedToOrgUnitId,
    DateTime CreatedAt);

public sealed record IncomingMailDetail(
    long Id,
    string TransactionNumber,
    string SenderEntity,
    string? SenderName,
    string? SenderReference,
    string Subject,
    string? Body,
    DateOnly? IssueDate,
    DateOnly ReceivedDate,
    string Confidentiality,
    string Priority,
    string Status,
    string? Keywords,
    long? AssignedToPositionId,
    long? AssignedToOrgUnitId,
    long? AssignedToUserId,
    long? ParentMailId,
    DateTime CreatedAt,
    IReadOnlyList<MailTimelineEntry> Timeline);

public sealed record MailTimelineEntry(
    long Id,
    string Action,
    long? UserId,
    DateTime At,
    string? Note);

public sealed record CreateIncomingMailRequest(
    string SenderEntity,
    string? SenderName,
    string? SenderReference,
    string Subject,
    string? Body,
    DateOnly? IssueDate,
    DateOnly ReceivedDate,
    ConfidentialityLevel Confidentiality,
    Priority Priority,
    string? Keywords,
    long? DocumentTypeId,
    long? CategoryId,
    long? ParentMailId);

/// <summary>Routing/lifecycle actions on a transaction.</summary>
public enum IncomingMailActionType { Forward, Approve, Hold, Close, Archive }

public sealed record IncomingMailActionRequest(
    IncomingMailActionType Action,
    long? ToPositionId,
    long? ToOrgUnitId,
    long? ToUserId,
    string? Note);

public sealed record IncomingMailQuery(
    string? Search,
    IncomingMailStatus? Status,
    int Page = 1,
    int PageSize = 20);
