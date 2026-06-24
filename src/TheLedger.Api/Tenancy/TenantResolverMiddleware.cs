using System.Security.Claims;
using TheLedger.Application.Abstractions;

namespace TheLedger.Api.Tenancy;

/// <summary>
/// Resolves the per-request tenant/user from the authenticated principal's claims into the scoped
/// <see cref="ITenantContext"/>. Runs after authentication, before authorization. If no tenant claim
/// is present (e.g. a tenantless user mid-signup), the context simply stays unresolved.
/// </summary>
public sealed class TenantResolverMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true
            && Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var tenantId))
        {
            var subject = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
            Guid? userId = Guid.TryParse(subject, out var uid) ? uid : null;
            var role = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
            tenantContext.Resolve(tenantId, userId, role);
        }

        await next(context);
    }
}
