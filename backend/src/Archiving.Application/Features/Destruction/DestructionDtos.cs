namespace Archiving.Application.Features.Destruction;

// ---- Legal hold ----
public sealed record LegalHoldDto(
    long Id, string Reason, int Scope, long? DocumentId, long? FolderId, long? OrgUnitId,
    long PlacedByUserId, string? PlacedByName, DateTime PlacedAtUtc,
    long? ReleasedByUserId, DateTime? ReleasedAtUtc, bool IsActive);

public sealed record PlaceLegalHoldRequest(
    string Reason, int Scope, long? DocumentId, long? FolderId, long? OrgUnitId, string? QueryExpression);

// ---- Eligibility ----
public sealed record DestructionEligibilityDto(long DocumentId, bool Eligible, IReadOnlyList<string> Reasons);

// ---- Destruction request ----
public sealed record DestructionItemDto(
    long Id, long DocumentId, string DocumentNumber, string DocumentTitle,
    int Method, string? CustomMethod, string? ChecksumBefore, string? Outcome);

public sealed record DestructionRequestDto(
    long Id, string Status, string Reason, long? RetentionBasisId,
    long RequestedByUserId, string? RequestedByName, DateTime RequestedAtUtc,
    long? ApprovedByUserId, string? ApprovedByName, DateTime? ApprovedAtUtc,
    long? ExecutedByUserId, DateTime? ExecutedAtUtc,
    string? DecisionNote, DateTime? ScheduledForUtc, long? CertificateId,
    IReadOnlyList<DestructionItemDto> Items);

public sealed record CreateDestructionRequest(
    IReadOnlyList<long> DocumentIds, string Reason, int Method, long? RetentionBasisId, DateTime? ScheduledForUtc,
    string? CustomMethod = null);

public sealed record DestructionDecisionRequest(string? Note);

/// <summary>Execution carries an MFA step-up (re-authentication) and optional physical-destruction details.</summary>
public sealed record ExecuteDestructionRequest(string? StepUpPassword, string? PhysicalOfficer, string? PhysicalWitness);

public sealed record DestructionRequestQuery(int? Status, int Page = 1, int PageSize = 20);

// ---- Admin-managed method catalog ----
public sealed record DestructionMethodOptionDto(long Id, string Label, int SortOrder, bool IsActive);
public sealed record MethodOptionRequest(string Label, bool IsActive);
