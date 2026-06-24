using TheLedger.Application.Authorization;
using TheLedger.Application.Budgeting;

namespace TheLedger.Api.Endpoints;

public static class BudgetEndpoints
{
    /// <summary>Budgeting (feature #14): set a monthly category target and read spent-vs-target.</summary>
    public static void MapBudgets(this IEndpointRouteBuilder app)
    {
        var budgets = app.MapGroup("/api/v1/budgets").WithTags("Budgets");

        budgets.MapGet("/", async (int year, int month, IBudgetService svc, CancellationToken ct) =>
                Results.Ok(await svc.GetBudgetsAsync(year, month, ct)))
            .RequireAuthorization(Policies.BudgetsView);

        budgets.MapPost("/", async (SetBudgetRequest req, IBudgetService svc, CancellationToken ct) =>
                Results.Ok(await svc.SetBudgetAsync(req, ct)))
            .RequireAuthorization(Policies.BudgetsEdit);
    }
}
