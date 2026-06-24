namespace TheLedger.Application.Budgeting;

public sealed record SetBudgetRequest(Guid CategoryId, int Year, int Month, decimal TargetAmount, bool Rollover);

public sealed record BudgetStatusDto(
    Guid CategoryId,
    string? CategoryName,
    int Year,
    int Month,
    decimal Target,
    decimal RolledOver,
    decimal Spent,
    decimal Remaining,
    bool Rollover);

/// <summary>Flexible category-target budgeting: set a monthly target, track spent-vs-target, roll over leftovers.</summary>
public interface IBudgetService
{
    Task<BudgetStatusDto> SetBudgetAsync(SetBudgetRequest request, CancellationToken ct);
    Task<IReadOnlyList<BudgetStatusDto>> GetBudgetsAsync(int year, int month, CancellationToken ct);
}
