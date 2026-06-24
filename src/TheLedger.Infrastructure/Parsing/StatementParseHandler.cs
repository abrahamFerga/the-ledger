using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Ingestion.Extraction;
using TheLedger.Domain.Ledger;
using TheLedger.Domain.Statements;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Parsing;

/// <summary>
/// Handles the <c>statement.parse</c> outbox job: loads the uploaded bytes, extracts text, runs the
/// statement extractor, stages the transactions (unconfirmed) and runs the balance-reconciliation
/// pass. Runs in the worker outside any request, so all reads ignore the tenant query filter and
/// writes stamp the statement's tenant explicitly.
/// </summary>
public sealed class StatementParseHandler(
    LedgerDbContext db,
    IPdfTextExtractor pdf,
    IStatementExtractor extractor,
    ILogger<StatementParseHandler> logger)
{
    public async Task HandleAsync(Guid statementId, CancellationToken ct)
    {
        var statement = await db.Statements.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == statementId, ct);
        if (statement is null)
        {
            logger.LogWarning("Statement {Id} not found for parsing", statementId);
            return;
        }

        statement.Status = StatementStatus.Parsing;
        await db.SaveChangesAsync(ct);

        try
        {
            var file = await db.StatementFiles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.StatementId == statementId, ct);
            if (file is null)
            {
                statement.Status = StatementStatus.Failed;
                await db.SaveChangesAsync(ct);
                logger.LogWarning("No stored file for statement {Id}", statementId);
                return;
            }

            var account = await db.Accounts.IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == statement.AccountId, ct);

            var text = pdf.ExtractText(file.Content);
            var result = await extractor.ExtractAsync(text, account?.Institution, ct);
            var reconciliation = StatementReconciler.Reconcile(result);

            foreach (var x in result.Transactions)
            {
                db.Transactions.Add(new Transaction
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = statement.TenantId,
                    AccountId = statement.AccountId,
                    StatementId = statement.Id,
                    Date = x.Date,
                    Description = PanMasker.Mask(x.Description),
                    Amount = x.Amount,
                    Currency = account?.Currency ?? "MXN",
                    Direction = x.Direction,
                    IsConfirmed = false,
                });
            }

            statement.TransactionCount = result.Transactions.Count;
            statement.Bank = result.Bank;
            statement.Reconciliation = reconciliation.ToString();
            statement.Status = StatementStatus.Parsed;
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Parsed statement {Id}: {Count} transactions, bank {Bank}, reconciliation {Reconciliation}",
                statement.Id, result.Transactions.Count, result.Bank, reconciliation);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse statement {Id}", statementId);
            statement.Status = StatementStatus.Failed;
            await db.SaveChangesAsync(ct);
        }
    }
}
