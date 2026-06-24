using TheLedger.Domain.Common;

namespace TheLedger.Domain.Statements;

public enum StatementSource
{
    Pdf,
    Csv,
    Manual
}

public enum StatementStatus
{
    Uploaded,
    Parsing,
    Parsed,
    Confirmed,
    Failed
}

/// <summary>An ingested bank statement. The raw file lives encrypted in blob storage (FileRef).</summary>
public sealed class Statement : Entity, ITenantOwned, IAuditable
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public StatementSource Source { get; set; }

    [Pii]
    public string? FileRef { get; set; }

    public string? Period { get; set; }
    public StatementStatus Status { get; set; } = StatementStatus.Uploaded;
    public Guid? UploadedByUserId { get; set; }
    public int TransactionCount { get; set; }

    /// <summary>Result of the balance-reconciliation pass after parsing (Matched / Unverified / Mismatch).</summary>
    public string? Reconciliation { get; set; }
    public string? Bank { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
