using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TheLedger.Application.Ingestion.Extraction;
using TheLedger.Domain.Accounts;
using TheLedger.Domain.Ledger;
using TheLedger.Domain.Statements;
using TheLedger.Infrastructure.Parsing;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.IntegrationTests;

public class StatementParsingTests
{
    private const string SampleBbva =
        "BBVA MEXICO\n" +
        "ESTADO DE CUENTA\n" +
        "SALDO ANTERIOR 1,000.00\n" +
        "05/ENE/2026 OXXO TIENDA 150.00\n" +
        "06/ENE/2026 DEPOSITO NOMINA 12,000.00\n" +
        "07/ENE/2026 CFE PAGO SERVICIO 450.50\n" +
        "SALDO FINAL 12,399.50\n";

    [Fact]
    public async Task Heuristic_extractor_parses_transactions_balances_and_reconciles()
    {
        var result = await new HeuristicStatementExtractor().ExtractAsync(SampleBbva, null, default);

        Assert.Equal("BBVA", result.Bank);
        Assert.Equal(1000.00m, result.OpeningBalance);
        Assert.Equal(12399.50m, result.ClosingBalance);
        Assert.Equal(3, result.Transactions.Count);

        var nomina = result.Transactions.Single(t => t.Description.Contains("NOMINA"));
        Assert.Equal(TransactionDirection.Credit, nomina.Direction);
        Assert.Equal(12000.00m, nomina.Amount);

        var oxxo = result.Transactions.Single(t => t.Description.Contains("OXXO"));
        Assert.Equal(TransactionDirection.Debit, oxxo.Direction);

        Assert.Equal(ReconciliationStatus.Matched, StatementReconciler.Reconcile(result));
    }

    [Fact]
    public async Task Parse_handler_stages_transactions_and_records_reconciliation()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        var tenantId = tenant.TenantId!.Value;

        await using var ctx = new LedgerDbContext(
            new DbContextOptionsBuilder<LedgerDbContext>().UseSqlite(connection).Options, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var account = new Account { Id = Guid.CreateVersion7(), TenantId = tenantId, Name = "BBVA", Institution = "BBVA", Currency = "MXN" };
        var statement = new Statement { Id = Guid.CreateVersion7(), TenantId = tenantId, AccountId = account.Id, Source = StatementSource.Pdf };
        var file = new StatementFile { Id = Guid.CreateVersion7(), TenantId = tenantId, StatementId = statement.Id, Content = Encoding.UTF8.GetBytes(SampleBbva) };
        ctx.Accounts.Add(account);
        ctx.Statements.Add(statement);
        ctx.StatementFiles.Add(file);
        await ctx.SaveChangesAsync();

        var handler = new StatementParseHandler(
            ctx, new Utf8TextExtractor(), new HeuristicStatementExtractor(), NullLogger<StatementParseHandler>.Instance);
        await handler.HandleAsync(statement.Id, default);

        var reloaded = await ctx.Statements.IgnoreQueryFilters().FirstAsync(s => s.Id == statement.Id);
        Assert.Equal(StatementStatus.Parsed, reloaded.Status);
        Assert.Equal(3, reloaded.TransactionCount);
        Assert.Equal("Matched", reloaded.Reconciliation);

        var staged = await ctx.Transactions.IgnoreQueryFilters().Where(t => t.StatementId == statement.Id).ToListAsync();
        Assert.Equal(3, staged.Count);
        Assert.All(staged, t => Assert.False(t.IsConfirmed));
    }
}
