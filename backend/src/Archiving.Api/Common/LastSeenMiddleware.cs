using System.Security.Claims;

namespace Archiving.Api.Common;

public sealed class LastSeenMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, IOnlineUserTracker tracker)
    {
        await next(ctx);
        if (ctx.User.Identity?.IsAuthenticated != true) return;
        if (!long.TryParse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return;

        var name = ctx.User.FindFirstValue(ClaimTypes.Name) ?? "";
        var role = ctx.User.FindFirstValue(ClaimTypes.Role);
        tracker.Touch(userId, name, role);
    }
}
