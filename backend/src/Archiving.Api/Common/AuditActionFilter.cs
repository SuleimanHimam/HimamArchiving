using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Common;

/// <summary>
/// Global action filter: writes a tamper-evident audit entry after every successful
/// non-GET request decorated with [HasPermission].
///
/// DELETE operations return 204 (no body), so the entity name is snapshotted from the
/// database BEFORE the action runs — while the row still exists.
/// </summary>
public sealed class AuditActionFilter(IAuditWriter audit, AppDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Skip reads — execute the action but don't log it.
        if (context.HttpContext.Request.Method == HttpMethods.Get) { await next(); return; }

        // Require a [HasPermission] attribute.
        var permAttr = context.ActionDescriptor.EndpointMetadata
            .OfType<HasPermissionAttribute>()
            .FirstOrDefault();
        if (permAttr?.Policy is not { } policy) { await next(); return; }

        var permCode = policy[HasPermissionAttribute.PolicyPrefix.Length..]; // "IncomingMail.Delete"
        var dot = permCode.IndexOf('.');
        if (dot < 0) { await next(); return; }

        var entityType = permCode[..dot]; // "IncomingMail"
        // Derive action from HTTP method for standard CRUD; keep permission suffix for semantic actions
        // (Approve, Close, Archive, Forward, …) which come through PUT with distinct permission codes.
        var action = context.HttpContext.Request.Method switch
        {
            "POST"   => "Create",
            "DELETE" => "Delete",
            _        => permCode[(dot + 1)..], // "Edit", "Approve", "Close", "Archive", …
        };

        // Entity ID from route, e.g. /api/incoming-mail/{id}
        long? entityId = null;
        if (context.RouteData.Values.TryGetValue("id", out var idVal)
            && long.TryParse(idVal?.ToString(), out var routeId))
            entityId = routeId;

        // ── PRE-EXECUTION SNAPSHOT ──────────────────────────────────────────────
        // For DELETE (and other 204 responses) the entity is gone after execution.
        // Snapshot its display name NOW, while it still exists in the database.
        string? preTitle = null;
        bool isDelete = context.HttpContext.Request.Method == HttpMethods.Delete;
        if (isDelete && entityId.HasValue)
            preTitle = await SnapshotTitleAsync(entityType, entityId.Value, context.HttpContext.RequestAborted);

        // ── EXECUTE THE ACTION ──────────────────────────────────────────────────
        var executed = await next();

        // Only record successful responses.
        if (executed.Exception is not null || executed.Canceled) return;

        var statusCode =
            (executed.Result as ObjectResult)?.StatusCode
            ?? (executed.Result as StatusCodeResult)?.StatusCode
            ?? 200;
        if (statusCode is < 200 or >= 300) return;

        // ── POST-EXECUTION TITLE (for create / update that return a body) ───────
        string? entityTitle = preTitle; // start with pre-snapshot (non-null for deletes)
        if (entityTitle is null && (executed.Result as ObjectResult)?.Value is { } body)
        {
            var t = body.GetType();
            entityTitle =
                TryGet(t, body, "Title")
                ?? TryGet(t, body, "NameAr")
                ?? TryGet(t, body, "Subject")
                ?? TryGet(t, body, "Name")
                ?? TryGet(t, body, "TransactionNumber")
                ?? TryGet(t, body, "LetterNumber")
                ?? TryGet(t, body, "DocumentNumber");

            if (entityId is null && long.TryParse(TryGet(t, body, "Id"), out var bodyId))
                entityId = bodyId;
        }

        try
        {
            await audit.WriteAsync(
                action, entityType, entityId ?? 0, entityTitle,
                ct: context.HttpContext.RequestAborted);
        }
        catch
        {
            // Never let audit failure break the normal request flow.
        }
    }

    // ── Lookup entity display name by resource type ─────────────────────────────
    private async Task<string?> SnapshotTitleAsync(string entityType, long id, CancellationToken ct)
    {
        try
        {
            return entityType switch
            {
                "Documents"    => await db.Documents.IgnoreQueryFilters()
                                    .Where(d => d.Id == id).Select(d => d.Title).FirstOrDefaultAsync(ct),

                "IncomingMail" => await db.IncomingMails.IgnoreQueryFilters()
                                    .Where(m => m.Id == id).Select(m => m.Subject).FirstOrDefaultAsync(ct),

                "OutgoingMail" => await db.OutgoingMails.IgnoreQueryFilters()
                                    .Where(m => m.Id == id).Select(m => m.Subject).FirstOrDefaultAsync(ct),

                "Users"        => await db.Users.IgnoreQueryFilters()
                                    .Where(u => u.Id == id).Select(u => u.FullName).FirstOrDefaultAsync(ct),

                "Settings"     => await db.ClassificationTypes
                                    .Where(c => c.Id == id).Select(c => c.NameAr).FirstOrDefaultAsync(ct),

                "Organization" => await db.OrgUnits
                                    .Where(o => o.Id == id).Select(o => o.Name).FirstOrDefaultAsync(ct)
                                   ?? await db.Institutions
                                    .Where(i => i.Id == id).Select(i => i.Name).FirstOrDefaultAsync(ct),

                "Archive"      => await db.PhysicalLocations
                                    .Where(l => l.Id == id).Select(l => l.Name).FirstOrDefaultAsync(ct),

                "Workflow"     => await db.WorkflowDefinitions
                                    .Where(w => w.Id == id).Select(w => w.Name).FirstOrDefaultAsync(ct),

                _ => null,
            };
        }
        catch { return null; }
    }

    private static string? TryGet(Type type, object obj, string propName)
        => type.GetProperty(propName)?.GetValue(obj)?.ToString();
}
