using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TheLedger.Domain.Identity;
using TheLedger.Domain.Tenants;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.IntegrationTests;

/// <summary>
/// Proves the multi-tenant isolation guardrail (feature #9): the EF global query filter returns
/// only the current tenant's rows. Uses a shared in-memory SQLite connection so it runs anywhere
/// (no Docker). The Aspire-booted Postgres integration test is added by verify-runtime (follow-up).
/// </summary>
public class TenantIsolationTests
{
    private static LedgerDbContext NewContext(SqliteConnection connection, TenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlite(connection)
            .Options;
        return new LedgerDbContext(options, tenant);
    }

    [Fact]
    public async Task Query_filter_isolates_users_by_tenant()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();

        var seed = new TenantContext();
        seed.Resolve(tenantA, null, "Owner");

        await using (var ctx = NewContext(connection, seed))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.Tenants.Add(new Tenant { Id = tenantA, Name = "A" });
            ctx.Tenants.Add(new Tenant { Id = tenantB, Name = "B" });
            ctx.Users.Add(new User { Id = Guid.CreateVersion7(), TenantId = tenantA, Email = "a@example.com", ExternalAuthId = "a" });
            ctx.Users.Add(new User { Id = Guid.CreateVersion7(), TenantId = tenantB, Email = "b@example.com", ExternalAuthId = "b" });
            await ctx.SaveChangesAsync();
        }

        var scopeA = new TenantContext();
        scopeA.Resolve(tenantA, null, "Owner");
        await using (var ctx = NewContext(connection, scopeA))
        {
            var users = await ctx.Users.ToListAsync();
            Assert.Single(users);
            Assert.Equal("a@example.com", users[0].Email);
        }

        var scopeB = new TenantContext();
        scopeB.Resolve(tenantB, null, "Owner");
        await using (var ctx = NewContext(connection, scopeB))
        {
            var users = await ctx.Users.ToListAsync();
            Assert.Single(users);
            Assert.Equal("b@example.com", users[0].Email);
        }
    }

    [Fact]
    public async Task IgnoreQueryFilters_sees_all_tenants_for_operator_tooling()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var seed = new TenantContext();
        seed.Resolve(tenantA, null, "Operator");

        await using (var ctx = NewContext(connection, seed))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.Users.Add(new User { Id = Guid.CreateVersion7(), TenantId = tenantA, Email = "a@example.com", ExternalAuthId = "a" });
            ctx.Users.Add(new User { Id = Guid.CreateVersion7(), TenantId = tenantB, Email = "b@example.com", ExternalAuthId = "b" });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = NewContext(connection, seed))
        {
            var all = await ctx.Users.IgnoreQueryFilters().ToListAsync();
            Assert.Equal(2, all.Count);
        }
    }
}
