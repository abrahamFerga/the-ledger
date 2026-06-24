namespace TheLedger.Application.Budgeting;

public sealed record CreateGoalRequest(string Name, decimal TargetAmount, DateOnly? TargetDate, Guid? LinkedAccountId);

public sealed record ContributeRequest(decimal Amount);

public sealed record GoalDto(
    Guid Id, string Name, decimal TargetAmount, decimal CurrentAmount, decimal Progress,
    DateOnly? TargetDate, Guid? LinkedAccountId);

/// <summary>Named savings goals with progress (manual contributions, or a linked account balance).</summary>
public interface IGoalService
{
    Task<GoalDto> CreateGoalAsync(CreateGoalRequest request, CancellationToken ct);
    Task<IReadOnlyList<GoalDto>> ListGoalsAsync(CancellationToken ct);
    Task<GoalDto?> ContributeAsync(Guid goalId, ContributeRequest request, CancellationToken ct);
    Task<bool> DeleteGoalAsync(Guid goalId, CancellationToken ct);
}
