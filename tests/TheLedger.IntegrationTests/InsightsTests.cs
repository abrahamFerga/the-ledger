using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Insights;
using TheLedger.Application.Ledger;
using TheLedger.Infrastructure.Categorization;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Services;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.IntegrationTests;

public class InsightsTests
{
    private static LedgerDbContext NewContext(SqliteConnection connection, TenantContext tenant) =>
        new(new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditAndTenantInterceptor(tenant))
            .Options, tenant);

    private static IngestionService Ingestion(LedgerDbContext ctx, TenantContext tenant) =>
        new(ctx, tenant, new RuleCategorizer(ctx));

    [Fact]
    public async Task Net_worth_sums_account_balances()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var ingestion = Ingestion(ctx, tenant);
        var checking = await ingestion.CreateAccountAsync(new CreateAccountRequest("Checking", "Checking", "BBVA", "MXN", null), default);
        var card = await ingestion.CreateAccountAsync(new CreateAccountRequest("Card", "Card", "BBVA", "MXN", null), default);
        await ingestion.AddManualTransactionAsync(new ManualTransactionRequest(checking.Id, new DateOnly(2026, 1, 5), "DEPOSITO", 5000m, "Credit"), default);
        await ingestion.AddManualTransactionAsync(new ManualTransactionRequest(card.Id, new DateOnly(2026, 1, 6), "COMPRA", 1200m, "Debit"), default);

        var netWorth = await new InsightsService(ctx).GetNetWorthAsync(default);

        Assert.Equal(3800m, netWorth.Total); // 5000 - 1200
        Assert.Equal(2, netWorth.Accounts.Count);
    }

    [Fact]
    public async Task Spending_breaks_down_by_category()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var ingestion = Ingestion(ctx, tenant);
        var account = await ingestion.CreateAccountAsync(new CreateAccountRequest("Checking", "Checking", "BBVA", "MXN", null), default);
        await ingestion.AddManualTransactionAsync(new ManualTransactionRequest(account.Id, new DateOnly(2026, 1, 5), "OXXO", 150m, "Debit"), default);
        await ingestion.AddManualTransactionAsync(new ManualTransactionRequest(account.Id, new DateOnly(2026, 1, 7), "UBER", 80m, "Debit"), default);

        var insights = new InsightsService(ctx);
        var spending = await insights.GetSpendingByCategoryAsync(2026, 1, default);
        Assert.Equal(2, spending.Count);
        Assert.Equal(150m, spending.Single(s => s.CategoryId == SystemCategories.Groceries).Total);
        Assert.Equal(80m, spending.Single(s => s.CategoryId == SystemCategories.Transport).Total);

        var csv = await insights.ExportTransactionsCsvAsync(default);
        Assert.Contains("Date,Description,Amount,Currency,Direction,Category", csv);
        Assert.Contains("OXXO", csv);
    }
}
