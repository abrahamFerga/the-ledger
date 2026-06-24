using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Budgeting;
using TheLedger.Domain.Budgeting;
using TheLedger.Domain.Ledger;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Services;

public sealed class BudgetService(LedgerDbContext db) : IBudgetService
{
    public async Task<BudgetStatusDto> SetBudgetAsync(SetBudgetRequest request, CancellationToken ct)
    {
        var period = new DateOnly(request.Year, request.Month, 1);
        var budget = await db.Budgets.FirstOrDefaultAsync(b => b.CategoryId == request.CategoryId && b.PeriodMonth == period, ct);
        if (budget is null)
        {
            budget = new Budget { Id = Guid.CreateVersion7(), CategoryId = request.CategoryId, PeriodMonth = period };
            db.Budgets.Add(budget);
        }

        budget.TargetAmount = request.TargetAmount;
        budget.Rollover = request.Rollover;
        await db.SaveChangesAsync(ct);

        return await ToStatusAsync(budget, ct);
    }

    public async Task<IReadOnlyList<BudgetStatusDto>> GetBudgetsAsync(int year, int month, CancellationToken ct)
    {
        var period = new DateOnly(year, month, 1);
        var budgets = await db.Budgets.Where(b => b.PeriodMonth == period).ToListAsync(ct);

        var statuses = new List<BudgetStatusDto>();
        foreach (var budget in budgets)
        {
            statuses.Add(await ToStatusAsync(budget, ct));
        }

        return statuses;
    }

    private async Task<BudgetStatusDto> ToStatusAsync(Budget budget, CancellationToken ct)
    {
        var spent = await SpentAsync(budget.CategoryId, budget.PeriodMonth, ct);

        decimal rolledOver = 0m;
        if (budget.Rollover)
        {
            var previous = budget.PeriodMonth.AddMonths(-1);
            var previousBudget = await db.Budgets
                .FirstOrDefaultAsync(b => b.CategoryId == budget.CategoryId && b.PeriodMonth == previous, ct);
            if (previousBudget is not null)
            {
                var previousSpent = await SpentAsync(budget.CategoryId, previous, ct);
                rolledOver = Math.Max(0m, previousBudget.TargetAmount - previousSpent);
            }
        }

        var categoryName = await db.Categories
            .Where(c => c.Id == budget.CategoryId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct);

        var remaining = budget.TargetAmount + rolledOver - spent;
        return new BudgetStatusDto(
            budget.CategoryId, categoryName, budget.PeriodMonth.Year, budget.PeriodMonth.Month,
            budget.TargetAmount, rolledOver, spent, remaining, budget.Rollover);
    }

    private async Task<decimal> SpentAsync(Guid categoryId, DateOnly period, CancellationToken ct)
    {
        var start = period;
        var end = period.AddMonths(1);
        var debits = await db.Transactions
            .Where(t => t.IsConfirmed
                        && t.CategoryId == categoryId
                        && t.Direction == TransactionDirection.Debit
                        && t.Date >= start && t.Date < end)
            .Select(t => t.Amount)
            .ToListAsync(ct);
        return debits.Sum();
    }
}
