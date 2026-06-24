using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Budgeting;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Ledger;
using TheLedger.Infrastructure.Categorization;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Services;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.IntegrationTests;

public class BudgetTests
{
    private static LedgerDbContext NewContext(SqliteConnection connection, TenantContext tenant) =>
        new(new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditAndTenantInterceptor(tenant))
            .Options, tenant);

    private static IngestionService Ingestion(LedgerDbContext ctx, TenantContext tenant) =>
        new(ctx, tenant, new RuleCategorizer(ctx));

    [Fact]
    public async Task Budget_tracks_spent_against_target()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var ingestion = Ingestion(ctx, tenant);
        var account = await ingestion.CreateAccountAsync(new CreateAccountRequest("BBVA", "Checking", "BBVA", "MXN", null), default);
        // OXXO auto-categorizes to Groceries; confirmed manual transaction counts as spent.
        await ingestion.AddManualTransactionAsync(
            new ManualTransactionRequest(account.Id, new DateOnly(2026, 1, 12), "OXXO", 150.00m, "Debit"), default);

        var budgets = new BudgetService(ctx);
        await budgets.SetBudgetAsync(new SetBudgetRequest(SystemCategories.Groceries, 2026, 1, 1000.00m, Rollover: false), default);

        var status = Assert.Single(await budgets.GetBudgetsAsync(2026, 1, default));
        Assert.Equal(1000.00m, status.Target);
        Assert.Equal(150.00m, status.Spent);
        Assert.Equal(850.00m, status.Remaining);
    }

    [Fact]
    public async Task Rollover_carries_previous_month_leftover()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var ingestion = Ingestion(ctx, tenant);
        var account = await ingestion.CreateAccountAsync(new CreateAccountRequest("BBVA", "Checking", "BBVA", "MXN", null), default);
        // Spend 200 in December against a 1000 budget → 800 leftover.
        await ingestion.AddManualTransactionAsync(
            new ManualTransactionRequest(account.Id, new DateOnly(2025, 12, 10), "OXXO", 200.00m, "Debit"), default);

        var budgets = new BudgetService(ctx);
        await budgets.SetBudgetAsync(new SetBudgetRequest(SystemCategories.Groceries, 2025, 12, 1000.00m, Rollover: false), default);
        await budgets.SetBudgetAsync(new SetBudgetRequest(SystemCategories.Groceries, 2026, 1, 1000.00m, Rollover: true), default);

        var january = Assert.Single(await budgets.GetBudgetsAsync(2026, 1, default));
        Assert.Equal(800.00m, january.RolledOver);
        Assert.Equal(0.00m, january.Spent);
        Assert.Equal(1800.00m, january.Remaining);
    }
}
