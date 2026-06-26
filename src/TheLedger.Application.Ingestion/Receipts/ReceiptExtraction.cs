using TheLedger.Domain.Ledger;

namespace TheLedger.Application.Ingestion.Receipts;

/// <summary>A single line item read off a receipt/ticket (description + amount).</summary>
public sealed record ReceiptLineItem(string Description, decimal? Amount, double? Confidence);

/// <summary>
/// The structured result of OCR over a receipt/ticket photo (Azure Document Intelligence
/// <c>prebuilt-receipt</c>, ADR-0009). Every field carries an optional confidence so the
/// normalization step can flag low-confidence fields for the review queue. The merchant string is
/// raw (messy Mexican ticket text) — the <c>ReceiptNormalizationAgent</c> normalizes it later.
/// </summary>
public sealed record ReceiptExtractionResult(
    string? MerchantName,
    DateOnly? TransactionDate,
    decimal? Total,
    decimal? Tax,
    string? Currency,
    IReadOnlyList<ReceiptLineItem> LineItems,
    double? OverallConfidence)
{
    /// <summary>Empty result for a receipt the model could not read at all.</summary>
    public static ReceiptExtractionResult Empty { get; } =
        new(null, null, null, null, null, [], 0.0);
}

/// <summary>
/// Turns receipt/ticket image bytes into the structured <see cref="ReceiptExtractionResult"/> above.
/// The production implementation is Azure Document Intelligence's <c>prebuilt-receipt</c> model
/// (<c>Infrastructure.Azure</c>, ADR-0009); a deterministic fake backs dev/tests so CI needs no Azure
/// dependency. OCR runs in the Worker off the outbox, never inline in the request.
/// </summary>
public interface IReceiptExtractor
{
    Task<ReceiptExtractionResult> ExtractAsync(byte[] image, string? contentType, CancellationToken ct);
}
