using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Insights;
using TheLedger.Domain.Identity;
using TheLedger.Infrastructure.Categorization;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Services;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.IntegrationTests;

public class HouseholdSharingTests
{
    [Fact]
    public async Task Spending_can_be_attributed_to_members_and_broken_down_by_member()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        var tenantId = tenant.TenantId!.Value;

        await using var ctx = new LedgerDbContext(
            new DbContextOptionsBuilder<LedgerDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(new AuditAndTenantInterceptor(tenant))
                .Options, tenant);
        await ctx.Database.EnsureCreatedAsync();

        // Two members of the same household (tenant) — finances are shared by tenancy.
        var partnerId = Guid.CreateVersion7();
        ctx.Users.Add(new User { Id = Guid.CreateVersion7(), TenantId = tenantId, Email = "owner@x.com", DisplayName = "Owner", ExternalAuthId = "o", Role = UserRole.Owner });
        ctx.Users.Add(new User { Id = partnerId, TenantId = tenantId, Email = "partner@x.com", DisplayName = "Partner", ExternalAuthId = "p", Role = UserRole.Member });
        await ctx.SaveChangesAsync();

        var ingestion = new IngestionService(ctx, tenant, new RuleCategorizer(ctx));
        var account = await ingestion.CreateAccountAsync(new CreateAccountRequest("Checking", "Checking", "BBVA", "MXN", null), default);
        var oxxo = await ingestion.AddManualTransactionAsync(new ManualTransactionRequest(account.Id, new DateOnly(2026, 1, 5), "OXXO", 150m, "Debit"), default);
        await ingestion.AddManualTransactionAsync(new ManualTransactionRequest(account.Id, new DateOnly(2026, 1, 7), "UBER", 80m, "Debit"), default);

        // Attribute the OXXO charge to the partner.
        var attributed = await new LedgerService(ctx).AttributeTransactionAsync(oxxo.Id, partnerId, default);
        Assert.NotNull(attributed);
        Assert.Equal(partnerId, attributed!.AttributedUserId);

        var byMember = await new InsightsService(ctx).GetSpendingByMemberAsync(2026, 1, default);
        Assert.Equal(150m, byMember.Single(m => m.UserId == partnerId).Total);
        Assert.Equal("Partner", byMember.Single(m => m.UserId == partnerId).MemberName);
        Assert.Equal(80m, byMember.Single(m => m.UserId == null).Total); // UBER left unattributed
    }
}
