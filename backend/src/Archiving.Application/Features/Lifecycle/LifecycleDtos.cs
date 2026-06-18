using Archiving.Domain.Enums;

namespace Archiving.Application.Features.Lifecycle;

public sealed record RetentionPolicyDto(
    long Id,
    long DocumentTypeId,
    string DocumentTypeName,
    int RetentionMonths,
    string DisposalAction,
    bool RequiresApproval,
    string? Description);

public sealed record CreateRetentionPolicyRequest(
    long DocumentTypeId,
    int RetentionMonths,
    DisposalAction DisposalAction,
    bool RequiresApproval,
    string? Description);

public sealed record DisposalRequestDto(
    long Id,
    long DocumentId,
    string DocumentNumber,
    string DocumentTitle,
    string Action,
    string Status,
    long RequestedByUserId,
    DateTime RequestedAt,
    long? ApprovedByUserId,
    DateTime? ApprovedAt,
    DateTime? ExecutedAt,
    string? Justification);

public sealed record CreateDisposalRequestRequest(long DocumentId, DisposalAction Action, string? Justification);

public sealed record DisposalDecisionRequest(bool Approve, string? Note);

/// <summary>A document approaching or past its retention expiry — drives the lifecycle dashboard.</summary>
public sealed record ExpiringDocumentDto(
    long DocumentId,
    string DocumentNumber,
    string Title,
    DateOnly ExpiryDate,
    int DaysRemaining);
