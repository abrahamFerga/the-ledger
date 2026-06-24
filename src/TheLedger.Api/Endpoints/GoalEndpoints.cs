using TheLedger.Application.Authorization;
using TheLedger.Application.Budgeting;

namespace TheLedger.Api.Endpoints;

public static class GoalEndpoints
{
    /// <summary>Savings goals (feature #15): create, list with progress, contribute, delete.</summary>
    public static void MapGoals(this IEndpointRouteBuilder app)
    {
        var goals = app.MapGroup("/api/v1/goals").WithTags("Goals");

        goals.MapGet("/", async (IGoalService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListGoalsAsync(ct)))
            .RequireAuthorization(Policies.GoalsView);

        goals.MapPost("/", async (CreateGoalRequest req, IGoalService svc, CancellationToken ct) =>
                Results.Ok(await svc.CreateGoalAsync(req, ct)))
            .RequireAuthorization(Policies.GoalsEdit);

        goals.MapPost("/{id:guid}/contribute", async (Guid id, ContributeRequest req, IGoalService svc, CancellationToken ct) =>
                await svc.ContributeAsync(id, req, ct) is { } dto ? Results.Ok(dto) : Results.NotFound())
            .RequireAuthorization(Policies.GoalsEdit);

        goals.MapDelete("/{id:guid}", async (Guid id, IGoalService svc, CancellationToken ct) =>
                await svc.DeleteGoalAsync(id, ct) ? Results.NoContent() : Results.NotFound())
            .RequireAuthorization(Policies.GoalsEdit);
    }
}
