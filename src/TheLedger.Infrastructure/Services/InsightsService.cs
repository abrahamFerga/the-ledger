using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Insights;
using TheLedger.Domain.Ledger;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Services;

public sealed class InsightsService(LedgerDbContext db) : IInsightsService
{
    public async Task<NetWorthDto> GetNetWorthAsync(CancellationToken ct)
    {
        var accounts = await db.Accounts.OrderBy(a => a.Name).ToListAsync(ct);
        var balances = accounts
            .Select(a => new AccountBalanceDto(a.Id, a.Name, a.Type.ToString(), a.CurrentBalance, a.Currency))
            .ToList();
        return new NetWorthDto(balances.Sum(b => b.Balance), balances);
    }

    public async Task<IReadOnlyList<CategorySpendDto>> GetSpendingByCategoryAsync(int year, int month, CancellationToken ct)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var transactions = await db.Transactions
            .Where(t => t.IsConfirmed && t.Direction == TransactionDirection.Debit && t.Date >= start && t.Date < end)
            .ToListAsync(ct);
        var categories = await db.Categories.ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return transactions
            .GroupBy(t => t.CategoryId)
            .Select(g => new CategorySpendDto(
                g.Key,
                g.Key is { } cid && categories.TryGetValue(cid, out var name) ? name : "Uncategorized",
                g.Sum(t => t.Amount)))
            .OrderByDescending(c => c.Total)
            .ToList();
    }

    public async Task<IReadOnlyList<MonthlyTotalDto>> GetMonthlyTotalsAsync(CancellationToken ct)
    {
        var transactions = await db.Transactions.Where(t => t.IsConfirmed).ToListAsync(ct);
        return transactions
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .Select(g =>
            {
                var income = g.Where(t => t.Direction == TransactionDirection.Credit).Sum(t => t.Amount);
                var expense = g.Where(t => t.Direction == TransactionDirection.Debit).Sum(t => t.Amount);
                return new MonthlyTotalDto(g.Key.Year, g.Key.Month, income, expense, income - expense);
            })
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();
    }

    public async Task<IReadOnlyList<MemberSpendDto>> GetSpendingByMemberAsync(int year, int month, CancellationToken ct)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var transactions = await db.Transactions
            .Where(t => t.IsConfirmed && t.Direction == TransactionDirection.Debit && t.Date >= start && t.Date < end)
            .ToListAsync(ct);
        var members = await db.Users.ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? u.Email, ct);

        return transactions
            .GroupBy(t => t.AttributedUserId)
            .Select(g => new MemberSpendDto(
                g.Key,
                g.Key is { } uid && members.TryGetValue(uid, out var name) ? name : "Unattributed",
                g.Sum(t => t.Amount)))
            .OrderByDescending(m => m.Total)
            .ToList();
    }

    public async Task<string> ExportTransactionsCsvAsync(CancellationToken ct)
    {
        var transactions = await db.Transactions.Where(t => t.IsConfirmed).OrderBy(t => t.Date).ToListAsync(ct);
        var categories = await db.Categories.ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Date,Description,Amount,Currency,Direction,Category");
        foreach (var t in transactions)
        {
            var category = t.CategoryId is { } cid && categories.TryGetValue(cid, out var name) ? name : string.Empty;
            sb.AppendLine(string.Join(',',
                t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Escape(t.Description),
                t.Amount.ToString(CultureInfo.InvariantCulture),
                t.Currency,
                t.Direction.ToString(),
                Escape(category)));
        }

        return sb.ToString();
    }

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
