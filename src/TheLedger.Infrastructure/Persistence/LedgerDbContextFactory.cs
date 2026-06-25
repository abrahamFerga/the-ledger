using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TheLedger.Infrastructure.Tenancy;

namespace TheLedger.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <c>dotnet ef</c> tooling. Uses Npgsql with a placeholder connection
/// string (no database is contacted to scaffold a migration) and an unresolved tenant context —
/// the query filters only affect runtime queries, not the migration schema.
/// </summary>
public sealed class LedgerDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=ledgerdb;Username=postgres;Password=postgres")
            .Options;

        return new LedgerDbContext(options, new TenantContext());
    }
}
