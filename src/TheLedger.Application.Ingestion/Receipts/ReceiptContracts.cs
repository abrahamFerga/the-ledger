namespace TheLedger.Application.Ingestion.Receipts;

/// <summary>The accepted receipt after upload: queued for OCR in the worker, not yet a transaction.</summary>
public sealed record ReceiptDto(
    Guid Id, Guid AccountId, string Status, string? Merchant, DateOnly? TransactionDate,
    decimal? Total, string Currency, double? Confidence, bool NeedsReview, Guid? TransactionId);

/// <summary>
/// Ingests a snapped store ticket/receipt (epic 9, ADR-0009): stores the image via the existing
/// <c>IFileStore</c>, raises an outbox message, and returns the queued receipt. The worker then runs
/// OCR → normalization → a staged transaction in the existing review-and-confirm queue.
/// </summary>
public interface IReceiptIngestionService
{
    Task<ReceiptDto> UploadAsync(Guid accountId, string fileName, string contentType, byte[] image, CancellationToken ct);
    Task<IReadOnlyList<ReceiptDto>> ListAsync(CancellationToken ct);
}
