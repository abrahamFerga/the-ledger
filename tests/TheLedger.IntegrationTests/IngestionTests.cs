using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Ingestion.Csv;
using TheLedger.Domain.Ledger;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Services;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.IntegrationTests;

public class IngestionTests
{
    private static LedgerDbContext NewContext(SqliteConnection connection, TenantContext tenant) =>
        new(new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditAndTenantInterceptor(tenant)) // stamps TenantId, as AddInfrastructure does in prod
            .Options, tenant);

    [Fact]
    public void Csv_parser_reads_cargo_abono_columns()
    {
        var csv = "Fecha,Concepto,Cargo,Abono\n" +
                  "2026-01-05,OXXO TIENDA,150.00,\n" +
                  "2026-01-06,DEPOSITO NOMINA,,12000.00\n";

        var rows = CsvStatementParser.Parse(csv);

        Assert.Equal(2, rows.Count);
        Assert.Equal(TransactionDirection.Debit, rows[0].Direction);
        Assert.Equal(150.00m, rows[0].Amount);
        Assert.Equal(TransactionDirection.Credit, rows[1].Direction);
        Assert.Equal(12000.00m, rows[1].Amount);
    }

    [Fact]
    public void Pan_masker_masks_card_numbers_in_descriptions()
    {
        var masked = PanMasker.Mask("PAGO TARJETA 4111 1111 1111 1111 OXXO");

        Assert.Contains("****1111", masked);
        Assert.DoesNotContain("4111 1111 1111 1111", masked);
    }

    [Fact]
    public async Task Import_then_confirm_moves_transactions_into_the_ledger_and_updates_balance()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");

        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();
        var svc = new IngestionService(ctx, tenant);

        var account = await svc.CreateAccountAsync(
            new CreateAccountRequest("BBVA Debito", "Checking", "BBVA", "MXN", "1234567812345678"), default);
        Assert.Equal("****5678", account.MaskedNumber);

        var csv = "Fecha,Concepto,Cargo,Abono\n2026-01-05,OXXO,150.00,\n2026-01-06,NOMINA,,12000.00\n";
        var statement = await svc.ImportCsvAsync(new ImportCsvRequest(account.Id, "enero.csv", csv), default);
        Assert.Equal(2, statement.TransactionCount);

        var review = await svc.ListReviewQueueAsync(statement.Id, default);
        Assert.Equal(2, review.Count);
        Assert.All(review, t => Assert.False(t.IsConfirmed));

        await svc.ConfirmStatementAsync(statement.Id, default);

        Assert.Empty(await svc.ListReviewQueueAsync(statement.Id, default));

        var accounts = await svc.ListAccountsAsync(default);
        // 12000 credit - 150 debit = 11850
        Assert.Equal(11850.00m, accounts.Single().CurrentBalance);
    }
}
