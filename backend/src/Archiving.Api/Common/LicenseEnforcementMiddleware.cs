using System.Text.Json;
using Archiving.Application.Common.Interfaces;
using Archiving.Infrastructure.Licensing;
using Microsoft.Extensions.Options;

namespace Archiving.Api.Common;

/// <summary>Applies the licence to every API call. Signing in, reading the licence status and
/// installing a new licence always pass, otherwise an expired instance could not be rescued from its
/// own admin UI.</summary>
public sealed class LicenseEnforcementMiddleware(RequestDelegate next, IOptions<LicenseOptions> options)
{
    private readonly LicenseEnforcementMode _mode = options.Value.Enforcement;

    /// <summary>Paths that stay open whatever the licence says: authentication (so an admin can get in),
    /// the licence endpoints themselves, branding (the login page renders before anyone is signed in)
    /// and health.</summary>
    private static readonly string[] AlwaysAllowed =
    [
        "/api/auth",
        "/api/license",
        "/api/branding",
        "/health",
    ];

    public async Task InvokeAsync(HttpContext ctx, ILicenseService license)
    {
        // No signing key configured — licensing is not switched on for this build. Costs nothing and
        // blocks nothing, so development and pre-licensing deployments are unaffected.
        if (!license.IsEnforced)
        {
            await next(ctx);
            return;
        }

        var status = license.GetStatus();

        // Every response carries the state so the SPA can show a banner without polling a second endpoint.
        ctx.Response.Headers["X-License-State"] = status.State.ToString();
        if (status.State is LicenseState.Grace && status.DaysRemaining is { } d)
            ctx.Response.Headers["X-License-Days-Remaining"] = d.ToString();

        if (status.IsUsable || _mode == LicenseEnforcementMode.WarnOnly || !IsGoverned(ctx))
        {
            await next(ctx);
            return;
        }

        // ReadOnly: let people keep reading their own archive; refuse anything that changes it.
        var isRead = HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method);
        if (_mode == LicenseEnforcementMode.ReadOnly && isRead)
        {
            await next(ctx);
            return;
        }

        ctx.Response.StatusCode  = StatusCodes.Status403Forbidden;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            error     = "license_required",
            state     = status.State.ToString(),
            machineId = status.MachineId,
            message   = status.Message,
        }));
    }

    private static bool IsGoverned(HttpContext ctx)
    {
        var path = ctx.Request.Path;
        // Only the API is gated — static SPA files must still load, or there is no UI to fix it from.
        if (!path.StartsWithSegments("/api")) return false;
        return !AlwaysAllowed.Any(p => path.StartsWithSegments(p));
    }
}
