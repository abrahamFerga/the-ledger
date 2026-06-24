namespace TheLedger.Application.Insights;

public sealed record AccountBalanceDto(Guid AccountId, string Name, string Type, decimal Balance, string Currency);

public sealed record NetWorthDto(decimal Total, IReadOnlyList<AccountBalanceDto> Accounts);

public sealed record CategorySpendDto(Guid? CategoryId, string CategoryName, decimal Total);

public sealed record MonthlyTotalDto(int Year, int Month, decimal Income, decimal Expense, decimal Net);

public sealed record MemberSpendDto(Guid? UserId, string MemberName, decimal Total);

/// <summary>Net worth, spending insights, and a CSV export of the transaction history.</summary>
public interface IInsightsService
{
    Task<NetWorthDto> GetNetWorthAsync(CancellationToken ct);
    Task<IReadOnlyList<CategorySpendDto>> GetSpendingByCategoryAsync(int year, int month, CancellationToken ct);
    Task<IReadOnlyList<MonthlyTotalDto>> GetMonthlyTotalsAsync(CancellationToken ct);
    Task<IReadOnlyList<MemberSpendDto>> GetSpendingByMemberAsync(int year, int month, CancellationToken ct);
    Task<string> ExportTransactionsCsvAsync(CancellationToken ct);
}
