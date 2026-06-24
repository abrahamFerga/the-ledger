using System.Text;
using TheLedger.Application.Authorization;
using TheLedger.Application.Insights;

namespace TheLedger.Api.Endpoints;

public static class InsightsEndpoints
{
    /// <summary>Insights + export (feature #16): net worth, spending breakdowns, monthly totals, CSV export.</summary>
    public static void MapInsights(this IEndpointRouteBuilder app)
    {
        var insights = app.MapGroup("/api/v1/insights").WithTags("Insights");

        insights.MapGet("/net-worth", async (IInsightsService svc, CancellationToken ct) =>
                Results.Ok(await svc.GetNetWorthAsync(ct)))
            .RequireAuthorization(Policies.InsightsView);

        insights.MapGet("/spending", async (int year, int month, IInsightsService svc, CancellationToken ct) =>
                Results.Ok(await svc.GetSpendingByCategoryAsync(year, month, ct)))
            .RequireAuthorization(Policies.InsightsView);

        insights.MapGet("/monthly", async (IInsightsService svc, CancellationToken ct) =>
                Results.Ok(await svc.GetMonthlyTotalsAsync(ct)))
            .RequireAuthorization(Policies.InsightsView);

        app.MapGet("/api/v1/export/transactions.csv", async (IInsightsService svc, CancellationToken ct) =>
            {
                var csv = await svc.ExportTransactionsCsvAsync(ct);
                return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", "transactions.csv");
            })
            .WithTags("Insights")
            .RequireAuthorization(Policies.InsightsView);
    }
}
