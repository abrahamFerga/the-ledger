using TheLedger.Domain.Ledger;

namespace TheLedger.Application.Ingestion.Extraction;

public sealed record ExtractedTransaction(DateOnly Date, string Description, decimal Amount, TransactionDirection Direction);

public sealed record ExtractionResult(
    IReadOnlyList<ExtractedTransaction> Transactions,
    decimal? OpeningBalance,
    decimal? ClosingBalance,
    string Bank);

public enum ReconciliationStatus
{
    Matched,
    Unverified,
    Mismatch
}

/// <summary>Turns raw statement bytes into plain text. Production: AI / Document Intelligence.</summary>
public interface IPdfTextExtractor
{
    string ExtractText(byte[] content);
}

/// <summary>
/// Extracts transactions from statement text. The production primary is an LLM-forward extractor
/// (MAF + Azure OpenAI, ADR-0004) swapped behind this interface; the heuristic implementation is the
/// offline default + deterministic fast-path.
/// </summary>
public interface IStatementExtractor
{
    Task<ExtractionResult> ExtractAsync(string statementText, string? bankHint, CancellationToken ct);
}

/// <summary>Validates extracted transactions against the statement's opening/closing balances.</summary>
public static class StatementReconciler
{
    public static ReconciliationStatus Reconcile(ExtractionResult result, decimal epsilon = 0.01m)
    {
        if (result.ClosingBalance is not { } closing || result.OpeningBalance is not { } opening)
        {
            return ReconciliationStatus.Unverified;
        }

        var net = result.Transactions.Sum(t =>
            t.Direction == TransactionDirection.Credit ? t.Amount : -t.Amount);

        return Math.Abs(opening + net - closing) <= epsilon
            ? ReconciliationStatus.Matched
            : ReconciliationStatus.Mismatch;
    }
}
