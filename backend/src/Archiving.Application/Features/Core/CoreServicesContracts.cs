using Archiving.Application.Common.Models;
using Archiving.Domain.Core;

namespace Archiving.Application.Features.Core;

// ---- Approvals (standards §5.1) ----

/// <summary>
/// A request type's reaction to the end of its approval path. Runs inside the transaction that records
/// the final decision: throwing <see cref="Archiving.Domain.Common.DomainException"/> rolls the decision back
/// and returns the message to the approver.
/// </summary>
public interface IApprovalEffect
{
    string RequestType { get; }
    Task OnApprovedAsync(ApprovalRequest request, CancellationToken ct);
    Task OnRejectedAsync(ApprovalRequest request, string reason, CancellationToken ct);
    Task OnReturnedAsync(ApprovalRequest request, string reason, CancellationToken ct);
}

public sealed record SubmitApprovalRequest(
    string RequestType, string SourceModule, long SourceId, string Title, long WorkflowDefinitionId);

public sealed record ApprovalRequestDto(
    long Id, string Number, string RequestType, string SourceModule, long SourceId, string Title,
    string Status, long? WorkflowInstanceId, DateTime CreatedAt, DateTime? DecidedAt, string? DecisionNote);

public interface IApprovalService
{
    /// <summary>Creates the request and starts its workflow run. Fails when no effect is registered
    /// for the type or the workflow definition is missing/inactive.</summary>
    Task<Result<ApprovalRequestDto>> SubmitAsync(SubmitApprovalRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ApprovalRequestDto>> ListMineAsync(CancellationToken ct = default);
    /// <summary>صندوق الموافقات: the caller's open approval tasks with the request they decide.</summary>
    Task<IReadOnlyList<ApprovalInboxItem>> InboxAsync(CancellationToken ct = default);
}

/// <summary>One open approval task. Decisions go through the workflow task endpoint by <see cref="TaskId"/>.</summary>
public sealed record ApprovalInboxItem(
    long TaskId, long RequestId, string Number, string RequestType, string SourceModule, long SourceId,
    string Title, string? RequesterName, DateTime SubmittedAt, string StageName, DateTime DueAt, bool IsOverdue,
    string AllowedActions);

/// <summary>Called by the workflow engine for runs whose entity is an approval request.</summary>
public interface IApprovalOutcomeHandler
{
    public const string EntityType = "ApprovalRequest";

    /// <summary>Maps the run's new state to the request status and calls the effect.</summary>
    Task HandleAsync(long approvalRequestId, Archiving.Domain.Enums.WorkflowStatus runStatus, string? note, CancellationToken ct);
}

// ---- Reference lists ----

public sealed record LookupDto(long Id, string Category, string Code, string Name, string? NameEn,
    int SortOrder, bool IsActive, string RowVersion);

public sealed record SaveLookupRequest(string Category, string Code, string Name, string? NameEn,
    int SortOrder = 0, bool IsActive = true, string? RowVersion = null);

public interface ILookupService
{
    Task<IReadOnlyList<LookupDto>> ListAsync(string? category, bool activeOnly, CancellationToken ct = default);
    Task<Result<LookupDto>> CreateAsync(SaveLookupRequest request, CancellationToken ct = default);
    Task<Result<LookupDto>> UpdateAsync(long id, SaveLookupRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default);
}
