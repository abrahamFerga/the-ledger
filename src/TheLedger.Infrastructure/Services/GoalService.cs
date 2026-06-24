using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Budgeting;
using TheLedger.Domain.Budgeting;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Services;

public sealed class GoalService(LedgerDbContext db) : IGoalService
{
    public async Task<GoalDto> CreateGoalAsync(CreateGoalRequest request, CancellationToken ct)
    {
        var goal = new Goal
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            TargetAmount = request.TargetAmount,
            TargetDate = request.TargetDate,
            LinkedAccountId = request.LinkedAccountId,
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(goal, ct);
    }

    public async Task<IReadOnlyList<GoalDto>> ListGoalsAsync(CancellationToken ct)
    {
        var goals = await db.Goals.OrderBy(g => g.Name).ToListAsync(ct);
        var dtos = new List<GoalDto>();
        foreach (var goal in goals)
        {
            dtos.Add(await ToDtoAsync(goal, ct));
        }

        return dtos;
    }

    public async Task<GoalDto?> ContributeAsync(Guid goalId, ContributeRequest request, CancellationToken ct)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == goalId, ct);
        if (goal is null)
        {
            return null;
        }

        goal.CurrentAmount += request.Amount;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(goal, ct);
    }

    public async Task<bool> DeleteGoalAsync(Guid goalId, CancellationToken ct)
    {
        var goal = await db.Goals.FirstOrDefaultAsync(g => g.Id == goalId, ct);
        if (goal is null)
        {
            return false;
        }

        db.Goals.Remove(goal);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<GoalDto> ToDtoAsync(Goal goal, CancellationToken ct)
    {
        var current = goal.CurrentAmount;
        if (goal.LinkedAccountId is { } accountId)
        {
            var balance = await db.Accounts
                .Where(a => a.Id == accountId)
                .Select(a => (decimal?)a.CurrentBalance)
                .FirstOrDefaultAsync(ct);
            if (balance is { } b)
            {
                current = b;
            }
        }

        var progress = goal.TargetAmount > 0 ? Math.Clamp(current / goal.TargetAmount, 0m, 1m) : 0m;
        return new GoalDto(goal.Id, goal.Name, goal.TargetAmount, current, progress, goal.TargetDate, goal.LinkedAccountId);
    }
}
