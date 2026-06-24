using TheLedger.Application.Abstractions;
using TheLedger.Application.Authorization;
using TheLedger.Application.Foundations.DataSubject;
using TheLedger.Application.Foundations.Households;

namespace TheLedger.Api.Endpoints;

public static class FoundationsEndpoints
{
    /// <summary>Foundations API surface: households, members, consent-driven data rights. All under /api/v1.</summary>
    public static void MapFoundations(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        // --- Households ---
        var households = v1.MapGroup("/households").WithTags("Households");

        // Signup provisioning: the user is authenticated by the IdP but has no household yet.
        // Anonymous for the bootstrap; production gates this behind an authenticated-tenantless user.
        households.MapPost("/", async (ProvisionHouseholdRequest req, IHouseholdService svc, CancellationToken ct) =>
                Results.Ok(await svc.ProvisionAsync(req, ct)))
            .AllowAnonymous();

        households.MapGet("/current", async (IHouseholdService svc, CancellationToken ct) =>
                await svc.GetCurrentAsync(ct) is { } h ? Results.Ok(h) : Results.NotFound())
            .RequireAuthorization();

        households.MapGet("/current/export", async (ITenantContext tenant, IDataSubjectService svc, CancellationToken ct) =>
                tenant.TenantId is { } tid ? Results.Ok(await svc.ExportAsync(tid, ct)) : Results.NotFound())
            .RequireAuthorization(Policies.DataExport);

        households.MapDelete("/current", async (ITenantContext tenant, IDataSubjectService svc, CancellationToken ct) =>
            {
                if (tenant.TenantId is not { } tid)
                {
                    return Results.NotFound();
                }

                await svc.DeleteAsync(tid, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(Policies.DataDelete);

        // --- Members ---
        var members = v1.MapGroup("/members").WithTags("Members");

        members.MapGet("/", async (IHouseholdService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListMembersAsync(ct)))
            .RequireAuthorization(Policies.AccountsView);

        members.MapPost("/", async (InviteMemberRequest req, IHouseholdService svc, CancellationToken ct) =>
                Results.Ok(await svc.InviteMemberAsync(req, ct)))
            .RequireAuthorization(Policies.MembersInvite);

        // --- Operator data-subject endpoints (any tenant) ---
        var data = v1.MapGroup("/data").WithTags("DataSubject");

        data.MapGet("/{tenantId:guid}/export", async (Guid tenantId, IDataSubjectService svc, CancellationToken ct) =>
                Results.Ok(await svc.ExportAsync(tenantId, ct)))
            .RequireAuthorization(Policies.DataSubjectExecute);

        data.MapDelete("/{tenantId:guid}", async (Guid tenantId, IDataSubjectService svc, CancellationToken ct) =>
            {
                await svc.DeleteAsync(tenantId, ct);
                return Results.NoContent();
            })
            .RequireAuthorization(Policies.DataSubjectExecute);
    }
}
