using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Alerts;
using TheLedger.Domain.Accounts;
using TheLedger.Domain.Ledger;
using TheLedger.Infrastructure.Alerts;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Services;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.IntegrationTests;

public class AlertTests
{
    private static LedgerDbContext NewContext(SqliteConnection connection, TenantContext tenant) =>
        new(new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditAndTenantInterceptor(tenant))
            .Options, tenant);

    private static Transaction Txn(Guid tenantId, Guid accountId, DateOnly date, string description, decimal amount) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            AccountId = accountId,
            Date = date,
            Description = description,
            Amount = amount,
            Currency = "MXN",
            Direction = TransactionDirection.Debit,
            IsConfirmed = true,
        };

    [Fact]
    public async Task Detects_a_monthly_recurring_series()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        var tenantId = tenant.TenantId!.Value;
        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var account = Guid.CreateVersion7();
        ctx.Transactions.Add(Txn(tenantId, account, new DateOnly(2026, 1, 10), "NETFLIX", 199m));
        ctx.Transactions.Add(Txn(tenantId, account, new DateOnly(2026, 2, 10), "NETFLIX", 199m));
        ctx.Transactions.Add(Txn(tenantId, account, new DateOnly(2026, 3, 10), "NETFLIX", 199m));
        await ctx.SaveChangesAsync();

        await new AlertScanner(ctx, tenant).ScanAsync(default);

        var recurring = Assert.Single(await new AlertService(ctx).ListRecurringAsync(default));
        Assert.Equal("NETFLIX", recurring.Merchant);
        Assert.Equal(3, recurring.OccurrenceCount);
        Assert.Equal("Monthly", recurring.Cadence);
    }

    [Fact]
    public async Task Raises_a_duplicate_charge_alert()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        var tenantId = tenant.TenantId!.Value;
        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var account = Guid.CreateVersion7();
        ctx.Transactions.Add(Txn(tenantId, account, new DateOnly(2026, 1, 15), "AMAZON MX", 500m));
        ctx.Transactions.Add(Txn(tenantId, account, new DateOnly(2026, 1, 15), "AMAZON MX", 500m));
        await ctx.SaveChangesAsync();

        await new AlertScanner(ctx, tenant).ScanAsync(default);

        var alerts = await new AlertService(ctx).ListAlertsAsync(includeResolved: false, default);
        Assert.Contains(alerts, a => a.Type == "DuplicateCharge");
    }

    [Fact]
    public async Task Raises_a_low_balance_alert()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        var tenantId = tenant.TenantId!.Value;
        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        ctx.Accounts.Add(new Account
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Name = "Checking",
            Type = AccountType.Checking,
            Currency = "MXN",
            CurrentBalance = 50m,
        });
        await ctx.SaveChangesAsync();

        await new AlertScanner(ctx, tenant).ScanAsync(default);

        var alerts = await new AlertService(ctx).ListAlertsAsync(includeResolved: false, default);
        Assert.Contains(alerts, a => a.Type == "LowBalance");
    }
}
