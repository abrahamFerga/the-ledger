using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Budgeting;
using TheLedger.Application.Ingestion;
using TheLedger.Infrastructure.Categorization;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Services;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.IntegrationTests;

public class GoalTests
{
    private static LedgerDbContext NewContext(SqliteConnection connection, TenantContext tenant) =>
        new(new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditAndTenantInterceptor(tenant))
            .Options, tenant);

    [Fact]
    public async Task Contributions_advance_goal_progress()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var goals = new GoalService(ctx);
        var goal = await goals.CreateGoalAsync(new CreateGoalRequest("Emergency fund", 10000.00m, null, null), default);
        Assert.Equal(0m, goal.Progress);

        var updated = await goals.ContributeAsync(goal.Id, new ContributeRequest(2500.00m), default);

        Assert.NotNull(updated);
        Assert.Equal(2500.00m, updated!.CurrentAmount);
        Assert.Equal(0.25m, updated.Progress);
    }

    [Fact]
    public async Task Linked_goal_tracks_the_account_balance()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var ingestion = new IngestionService(ctx, tenant, new RuleCategorizer(ctx));
        var account = await ingestion.CreateAccountAsync(new CreateAccountRequest("Savings", "Savings", "BBVA", "MXN", null), default);
        await ingestion.AddManualTransactionAsync(
            new ManualTransactionRequest(account.Id, new DateOnly(2026, 1, 5), "DEPOSITO", 5000.00m, "Credit"), default);

        var goals = new GoalService(ctx);
        await goals.CreateGoalAsync(new CreateGoalRequest("House down payment", 10000.00m, null, account.Id), default);

        var goal = Assert.Single(await goals.ListGoalsAsync(default));
        Assert.Equal(5000.00m, goal.CurrentAmount); // mirrors the linked account balance
        Assert.Equal(0.5m, goal.Progress);
    }
}
