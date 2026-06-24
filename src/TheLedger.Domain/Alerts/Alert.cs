using TheLedger.Domain.Common;

namespace TheLedger.Domain.Alerts;

public enum AlertType
{
    DuplicateCharge,
    NewFee,
    LowBalance,
    UnusualSpend,
    BillDue
}

public enum AlertStatus
{
    New,
    Seen,
    Dismissed
}

/// <summary>An actionable notice surfaced to the user (in-app, and by email via the outbox).</summary>
public sealed class Alert : Entity, ITenantOwned, IAuditable
{
    public Guid TenantId { get; set; }
    public AlertType Type { get; set; }
    public Guid? TransactionId { get; set; }
    public Guid? AccountId { get; set; }
    public required string Message { get; set; }

    /// <summary>Stable dedupe key so a re-scan does not raise the same alert twice.</summary>
    public required string DedupeKey { get; set; }

    public AlertStatus Status { get; set; } = AlertStatus.New;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
