using TheLedger.Domain.Common;

namespace TheLedger.Domain.Receipts;

public enum ReceiptStatus
{
    Uploaded,
    Processing,
    Extracted,
    Confirmed,
    Failed
}

/// <summary>
/// A snapped store ticket/receipt (epic 9, ADR-0009). The image lives encrypted in blob storage
/// (<see cref="FileRef"/>); OCR runs in the worker off the outbox and produces a *staged*
/// <see cref="Domain.Ledger.Transaction"/> in the existing review-and-confirm queue. Low-confidence
/// extraction is flagged via <see cref="NeedsReview"/> so the user double-checks before confirming.
/// </summary>
public sealed class Receipt : Entity, ITenantOwned, IAuditable
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>Blob key of the uploaded image (card PANs are never in an image; OCR text is masked).</summary>
    [Pii]
    public string? FileRef { get; set; }

    public string ContentType { get; set; } = "image/jpeg";
    public ReceiptStatus Status { get; set; } = ReceiptStatus.Uploaded;
    public Guid? UploadedByUserId { get; set; }

    /// <summary>The staged transaction this receipt produced (set after OCR + normalization).</summary>
    public Guid? TransactionId { get; set; }

    /// <summary>Normalized merchant string proposed by the ReceiptNormalizationAgent.</summary>
    [Pii]
    public string? Merchant { get; set; }

    public DateOnly? TransactionDate { get; set; }
    public decimal? Total { get; set; }
    public decimal? Tax { get; set; }
    public string Currency { get; set; } = "MXN";

    /// <summary>Overall OCR confidence (0..1); low values set <see cref="NeedsReview"/>.</summary>
    public double? Confidence { get; set; }

    /// <summary>True when any extracted field was low-confidence and must be reviewed before confirm.</summary>
    public bool NeedsReview { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
