using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TheLedger.Application.Storage;
using TheLedger.Infrastructure.Azure;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Storage;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.IntegrationTests;

public class StorageTests
{
    [Fact]
    public async Task Db_file_store_round_trips_bytes_by_statement_id()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        await using var ctx = new LedgerDbContext(
            new DbContextOptionsBuilder<LedgerDbContext>()
                .UseSqlite(connection)
                .AddInterceptors(new AuditAndTenantInterceptor(tenant))
                .Options, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var store = new DbFileStore(ctx);
        var key = Guid.CreateVersion7().ToString();
        var bytes = new byte[] { 1, 2, 3, 4 };

        await store.SaveAsync(key, bytes, default);

        Assert.Equal(bytes, await store.GetAsync(key, default));
        Assert.Null(await store.GetAsync(Guid.CreateVersion7().ToString(), default));
    }

    [Fact]
    public void Azure_blob_store_is_registered_only_when_configured()
    {
        var services = new ServiceCollection();
        services.AddAzureBlobStorage(new ConfigurationBuilder().Build());
        Assert.Null(services.BuildServiceProvider().GetService<IFileStore>());
    }
}
