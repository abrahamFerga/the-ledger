using TheLedger.Application.Alerts;
using TheLedger.Application.Authorization;

namespace TheLedger.Api.Endpoints;

public static class AlertEndpoints
{
    /// <summary>Alerts (feature #17): list/dismiss alerts, run a scan, and list detected recurring series.</summary>
    public static void MapAlerts(this IEndpointRouteBuilder app)
    {
        var alerts = app.MapGroup("/api/v1/alerts").WithTags("Alerts");

        alerts.MapGet("/", async (bool? includeResolved, IAlertService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListAlertsAsync(includeResolved ?? false, ct)))
            .RequireAuthorization(Policies.AlertsView);

        alerts.MapPost("/scan", async (IAlertScanner scanner, CancellationToken ct) =>
                Results.Ok(new { raised = await scanner.ScanAsync(ct) }))
            .RequireAuthorization(Policies.AlertsView);

        alerts.MapPost("/{id:guid}/dismiss", async (Guid id, IAlertService svc, CancellationToken ct) =>
                await svc.DismissAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization(Policies.AlertsView);

        app.MapGet("/api/v1/recurring", async (IAlertService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListRecurringAsync(ct)))
            .WithTags("Alerts")
            .RequireAuthorization(Policies.AlertsView);
    }
}
