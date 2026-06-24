using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Ledger;
using TheLedger.Infrastructure.Categorization;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Services;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.IntegrationTests;

public class LedgerTests
{
    private static LedgerDbContext NewContext(SqliteConnection connection, TenantContext tenant) =>
        new(new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditAndTenantInterceptor(tenant))
            .Options, tenant);

    private static IngestionService Ingestion(LedgerDbContext ctx, TenantContext tenant) =>
        new(ctx, tenant, new RuleCategorizer(ctx));

    [Fact]
    public async Task System_categories_are_seeded_and_listed()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var categories = await new LedgerService(ctx).ListCategoriesAsync(default);

        Assert.True(categories.Count >= 10);
        Assert.Contains(categories, c => c.Name == "Groceries" && c.IsSystem);
    }

    [Fact]
    public async Task Csv_import_auto_categorizes_known_merchants()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var ingestion = Ingestion(ctx, tenant);
        var account = await ingestion.CreateAccountAsync(new CreateAccountRequest("BBVA", "Checking", "BBVA", "MXN", null), default);
        await ingestion.ImportCsvAsync(new ImportCsvRequest(account.Id, "e.csv", "Fecha,Concepto,Cargo,Abono\n2026-01-05,OXXO TIENDA,150.00,\n"), default);

        var feed = await new LedgerService(ctx).GetFeedAsync(new TransactionFeedQuery(null, null, ConfirmedOnly: false), default);

        var oxxo = Assert.Single(feed);
        Assert.Equal(SystemCategories.Groceries, oxxo.CategoryId);
        Assert.Equal("Groceries", oxxo.CategoryName);
    }

    [Fact]
    public async Task Recategorizing_learns_a_rule_for_future_transactions()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var account = await Ingestion(ctx, tenant).CreateAccountAsync(new CreateAccountRequest("BBVA", "Checking", "BBVA", "MXN", null), default);
        await Ingestion(ctx, tenant).ImportCsvAsync(new ImportCsvRequest(account.Id, "e.csv", "Fecha,Concepto,Cargo,Abono\n2026-01-05,TIENDA LOCAL XYZ,200.00,\n"), default);

        var ledger = new LedgerService(ctx);
        var txn = Assert.Single(await ledger.GetFeedAsync(new TransactionFeedQuery(null, null, ConfirmedOnly: false), default));
        Assert.Null(txn.CategoryId); // unknown merchant — uncategorized

        await ledger.UpdateTransactionAsync(txn.Id, new UpdateTransactionRequest(null, SystemCategories.Dining), default);

        // A fresh categorizer (new scope) now matches the learned rule.
        var result = await new RuleCategorizer(ctx).CategorizeAsync("TIENDA LOCAL XYZ compra", default);
        Assert.Equal(SystemCategories.Dining, result.CategoryId);
    }

    [Fact]
    public async Task Splitting_replaces_the_transaction_with_parts_that_sum_to_the_original()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var ingestion = Ingestion(ctx, tenant);
        var account = await ingestion.CreateAccountAsync(new CreateAccountRequest("BBVA", "Checking", "BBVA", "MXN", null), default);
        var manual = await ingestion.AddManualTransactionAsync(
            new ManualTransactionRequest(account.Id, new DateOnly(2026, 1, 5), "WALMART", 300.00m, "Debit"), default);

        var ledger = new LedgerService(ctx);
        var parts = await ledger.SplitTransactionAsync(manual.Id, new SplitTransactionRequest(
        [
            new SplitPart("WALMART groceries", 200.00m, SystemCategories.Groceries),
            new SplitPart("WALMART home", 100.00m, SystemCategories.Shopping),
        ]), default);

        Assert.Equal(2, parts.Count);
        var feed = await ledger.GetFeedAsync(new TransactionFeedQuery(account.Id, null, ConfirmedOnly: false), default);
        Assert.Equal(2, feed.Count);
        Assert.DoesNotContain(feed, t => t.Id == manual.Id);
    }
}
