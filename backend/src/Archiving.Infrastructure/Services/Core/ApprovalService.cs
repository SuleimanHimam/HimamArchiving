using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Core;
using Archiving.Application.Features.Workflow;
using Archiving.Domain.Common;
using Archiving.Domain.Core;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Core;

/// <summary>
/// The approval layer (standards §5.1) on top of the existing workflow engine. A module submits a
/// request; the engine routes it through the definition the administrator built; the request type's
/// <see cref="IApprovalEffect"/> runs when the path ends.
/// </summary>
public sealed class ApprovalService(
    AppDbContext db,
    IWorkflowService workflow,
    INumberingService numbering,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IEnumerable<IApprovalEffect> effects) : IApprovalService
{
    public const string NumberingEntity = "ApprovalRequest";

    public async Task<Result<ApprovalRequestDto>> SubmitAsync(SubmitApprovalRequest r, CancellationToken ct = default)
    {
        if (!effects.Any(e => e.RequestType == r.RequestType))
            return Result<ApprovalRequestDto>.Fail("نوع الطلب غير معرّف في محرك الموافقات");
        if (!await db.WorkflowDefinitions.AnyAsync(d => d.Id == r.WorkflowDefinitionId && d.IsActive, ct))
            return Result<ApprovalRequestDto>.Fail("مسار الموافقة غير موجود أو غير مفعّل");

        return await db.InTransactionAsync(async () =>
        {
            // Inside the transaction: a failed start rolls the sequence back, so numbers stay gap-free.
            var number = await numbering.GenerateNumberAsync(NumberingEntity, ct: ct);
            if (number is null) return Result<ApprovalRequestDto>.Fail("قالب ترقيم الطلبات غير معرّف");

            var request = new ApprovalRequest
            {
                Number = number,
                RequestType = r.RequestType,
                SourceModule = r.SourceModule,
                SourceId = r.SourceId,
                Title = r.Title.Length > 300 ? r.Title[..300] : r.Title,
                RequesterUserId = currentUser.UserId,
            };
            db.ApprovalRequests.Add(request);
            await db.SaveChangesAsync(ct);

            var started = await workflow.StartAsync(
                new StartWorkflowRequest(r.WorkflowDefinitionId, IApprovalOutcomeHandler.EntityType, request.Id), ct);
            if (!started.Succeeded) return Result<ApprovalRequestDto>.Fail(started.Error ?? "تعذّر بدء مسار الموافقة");

            request.WorkflowInstanceId = started.Value!.Id;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Submitted", "ApprovalRequest", request.Id, request.Number, ct: ct);
            return Result<ApprovalRequestDto>.Ok(ToDto(request));
        }, result => result.Succeeded, ct);
    }

    public async Task<IReadOnlyList<ApprovalRequestDto>> ListMineAsync(CancellationToken ct = default)
    {
        if (currentUser.UserId is not { } me) return [];
        var rows = await db.ApprovalRequests.AsNoTracking().Where(a => a.RequesterUserId == me)
            .OrderByDescending(a => a.Id).Take(200).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<ApprovalInboxItem>> InboxAsync(CancellationToken ct = default)
    {
        // Task ownership (seat or named user) is decided by the engine's worklist; this only adds the request.
        var tasks = (await workflow.MyWorklistAsync(ct))
            .Where(t => t.EntityType == IApprovalOutcomeHandler.EntityType).ToList();
        if (tasks.Count == 0) return [];

        var ids = tasks.Select(t => t.EntityId).Distinct().ToList();
        var requests = await db.ApprovalRequests.AsNoTracking()
            .Where(a => ids.Contains(a.Id) && a.Status == ApprovalRequestStatus.Pending)
            .ToDictionaryAsync(a => a.Id, ct);
        var requesterIds = requests.Values.Where(a => a.RequesterUserId != null)
            .Select(a => a.RequesterUserId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => requesterIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return tasks.Where(t => requests.ContainsKey(t.EntityId)).Select(t =>
        {
            var a = requests[t.EntityId];
            return new ApprovalInboxItem(t.TaskId, a.Id, a.Number, a.RequestType, a.SourceModule, a.SourceId, a.Title,
                a.RequesterUserId is { } u ? names.GetValueOrDefault(u) : null, a.CreatedAt,
                t.StageName, t.DueAt, t.IsOverdue, t.AllowedActions);
        }).ToList();
    }

    internal static ApprovalRequestDto ToDto(ApprovalRequest a) => new(a.Id, a.Number, a.RequestType, a.SourceModule,
        a.SourceId, a.Title, a.Status.ToString(), a.WorkflowInstanceId, a.CreatedAt, a.DecidedAt, a.DecisionNote);
}

/// <summary>
/// Turns the end of a run into the request's outcome. Called by <c>WorkflowService.ActOnTaskAsync</c>
/// before it saves the decision, inside the same transaction, so an effect that fails undoes the decision.
/// </summary>
public sealed class ApprovalOutcomeHandler(AppDbContext db, IEnumerable<IApprovalEffect> effects) : IApprovalOutcomeHandler
{
    public async Task HandleAsync(long approvalRequestId, WorkflowStatus runStatus, string? note, CancellationToken ct)
    {
        var outcome = runStatus switch
        {
            WorkflowStatus.Completed => ApprovalRequestStatus.Approved,
            WorkflowStatus.Rejected => ApprovalRequestStatus.Rejected,
            WorkflowStatus.ChangesRequested => ApprovalRequestStatus.Returned,
            _ => (ApprovalRequestStatus?)null,   // still in the path: an intermediate level acted
        };
        if (outcome is not { } status) return;

        var request = await db.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == approvalRequestId, ct)
            ?? throw new DomainException("طلب الموافقة غير موجود");
        var effect = effects.FirstOrDefault(e => e.RequestType == request.RequestType)
            ?? throw new DomainException("نوع الطلب غير معرّف في محرك الموافقات");

        request.Status = status;
        request.DecidedAt = status == ApprovalRequestStatus.Returned ? null : DateTime.UtcNow;
        request.DecisionNote = note;

        // The engine already refuses a rejection or a return without a reason; this is the second gate.
        var reason = note?.Trim() ?? string.Empty;
        switch (status)
        {
            case ApprovalRequestStatus.Approved:
                await effect.OnApprovedAsync(request, ct);
                break;
            case ApprovalRequestStatus.Rejected:
                if (reason.Length == 0) throw new DomainException("سبب الرفض مطلوب");
                await effect.OnRejectedAsync(request, reason, ct);
                break;
            case ApprovalRequestStatus.Returned:
                if (reason.Length == 0) throw new DomainException("سبب الإعادة مطلوب");
                await effect.OnReturnedAsync(request, reason, ct);
                break;
        }
    }
}
