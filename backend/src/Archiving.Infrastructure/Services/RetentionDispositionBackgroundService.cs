using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Services;

/// <summary>Daily retention sweep. (1) Raises a two-step DispositionRequest for every document whose
/// retention has expired, is still active, and is under no legal hold. (2) Sends advance warnings at
/// 90/60/30/7 days before expiry. Cadence via <c>Retention:IntervalSeconds</c> (default 86400 = daily).</summary>
public sealed class RetentionDispositionBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<RetentionDispositionBackgroundService> logger) : BackgroundService
{
    private static readonly (int Days, RetentionAlertStage Stage)[] Checkpoints =
        [(90, RetentionAlertStage.Days90), (60, RetentionAlertStage.Days60), (30, RetentionAlertStage.Days30), (7, RetentionAlertStage.Days7)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = config.GetValue<int?>("Retention:IntervalSeconds") ?? 86_400;
        var interval = TimeSpan.FromSeconds(Math.Max(60, seconds));
        try { await Task.Delay(TimeSpan.FromSeconds(25), stoppingToken); } catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(interval);
        do
        {
            try { await SweepAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Retention sweep failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditWriter>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var officerIds = await RecipientsAsync(db, "Disposition.Edit", ct);

        // ── (1) Auto-generate disposition requests for expired documents ──
        var expired = await db.Documents
            .Where(d => !d.IsTombstone && d.Status != DocumentStatus.Disposed
                && d.ExpiryDate != null && d.ExpiryDate <= today
                && !db.DispositionRequests.Any(x => x.DocumentId == d.Id &&
                    (x.Status == DispositionStatus.PendingVerification || x.Status == DispositionStatus.PendingFinalApproval))
                && !db.LegalHolds.Any(h => h.ReleasedAtUtc == null && (
                    (h.Scope == LegalHoldScope.Document && h.DocumentId == d.Id)
                    || (h.Scope == LegalHoldScope.Folder && h.FolderId != null && h.FolderId == d.FolderId)
                    || (h.Scope == LegalHoldScope.OrgUnit && h.OrgUnitId == d.OwningOrgUnitId))))
            .Select(d => new { d.Id, d.DocumentNumber, d.DocumentTypeId })
            .Take(500).ToListAsync(ct);

        var generated = 0;
        foreach (var d in expired)
        {
            // Honour the category's default action; default is RequireDecision → a Destroy request.
            var policy = await db.RetentionPolicies.Where(p => p.DocumentTypeId == d.DocumentTypeId)
                .Select(p => new { p.DefaultAction }).FirstOrDefaultAsync(ct);
            var action = policy?.DefaultAction == RetentionDefaultAction.AutoRenew
                ? DispositionAction.Renew : DispositionAction.Destroy;

            db.DispositionRequests.Add(new DispositionRequest
            {
                DocumentId = d.Id,
                RequestedAction = action,
                Reason = "تم بلوغ نهاية مدة الحفظ (إنشاء تلقائي)",
                RequestedByUserId = 0,        // system
                Status = DispositionStatus.PendingVerification,
            });

            // Track retention state (lazily create the row if missing).
            var ret = await db.DocumentRetentions.FirstOrDefaultAsync(x => x.DocumentId == d.Id, ct);
            if (ret is null)
                db.DocumentRetentions.Add(new DocumentRetention
                {
                    DocumentId = d.Id, TriggerDate = today, ExpiryDate = today,
                    Status = DocumentRetentionStatus.PendingReview,
                });
            else ret.Status = DocumentRetentionStatus.PendingReview;

            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("DispositionAutoCreated", "Document", d.Id, d.DocumentNumber, ct: ct);
            foreach (var uid in officerIds)
                db.Notifications.Add(Note(uid, "طلب تصرّف تلقائي بانتظار التحقق", d.DocumentNumber, d.Id));
            generated++;
        }
        if (generated > 0) await db.SaveChangesAsync(ct);

        // ── (2) Advance expiry warnings at 90/60/30/7 days ──
        var warned = 0;
        foreach (var (days, stage) in Checkpoints)
        {
            var horizon = today.AddDays(days);
            var due = await db.Documents
                .Where(d => !d.IsTombstone && d.Status != DocumentStatus.Disposed
                    && d.ExpiryDate != null && d.ExpiryDate > today && d.ExpiryDate <= horizon
                    && !db.RetentionAlerts.Any(a => a.DocumentId == d.Id && a.Stage == stage))
                .Select(d => new { d.Id, d.DocumentNumber, d.ExpiryDate })
                .Take(500).ToListAsync(ct);

            foreach (var d in due)
            {
                db.RetentionAlerts.Add(new RetentionAlert
                {
                    DocumentId = d.Id, Stage = stage, DueDate = d.ExpiryDate!.Value, NotifiedAt = DateTime.UtcNow,
                });
                foreach (var uid in officerIds)
                    db.Notifications.Add(Note(uid, $"تنبيه: تنتهي مدة الحفظ خلال {days} يومًا", d.DocumentNumber, d.Id, NotificationType.Warning));
                warned++;
            }
        }
        if (warned > 0) await db.SaveChangesAsync(ct);

        if (generated > 0 || warned > 0)
            logger.LogInformation("Retention sweep: {Gen} request(s) generated, {Warn} warning(s) sent", generated, warned);
    }

    private static Notification Note(long uid, string title, string? body, long docId, NotificationType type = NotificationType.Task) =>
        new()
        {
            RecipientUserId = uid, Title = title, Body = body,
            Type = type, Channels = NotificationChannel.InApp,
            EntityType = "Disposition", EntityId = docId,
        };

    private static Task<List<long>> RecipientsAsync(AppDbContext db, string permCode, CancellationToken ct) =>
        db.Users.Where(u => u.IsActive && db.UserRoles.Any(ur => ur.UserId == u.Id && (
                db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "System Administrator") ||
                db.RolePermissions.Any(rp => rp.RoleId == ur.RoleId &&
                    db.Permissions.Any(p => p.Id == rp.PermissionId && p.Code == permCode)))))
            .Select(u => u.Id).ToListAsync(ct);
}
