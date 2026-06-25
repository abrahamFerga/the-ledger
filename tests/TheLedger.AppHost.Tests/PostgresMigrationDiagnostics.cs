using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.AppHost.Tests;

/// <summary>
/// Reproduces #45 against real Postgres: applies migrations and queries the (filtered) categories,
/// surfacing the exact exception that the swallowed startup migration hides in the booted app.
/// </summary>
public class PostgresMigrationDiagnostics
{
    [Fact]
    public async Task Migrate_then_query_categories_succeeds_on_postgres()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        var tenant = new TenantContext();
        await using var db = new LedgerDbContext(options, tenant);

        await db.Database.MigrateAsync();
        var categories = await db.Categories.ToListAsync();

        Assert.True(categories.Count >= 10, $"expected seeded system categories, got {categories.Count}");
    }
}
