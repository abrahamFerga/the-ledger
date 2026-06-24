using TheLedger.Domain.Common;

namespace TheLedger.Domain.Ledger;

public enum TransactionDirection
{
    Debit,
    Credit
}

public enum CategorizationSource
{
    None,
    Rule,
    Llm,
    Manual
}

/// <summary>
/// A single transaction. Lands in the review queue (<see cref="IsConfirmed"/> = false) from
/// ingestion, then is confirmed into the ledger. Amounts are stored as decimal with a currency.
/// </summary>
public sealed class Transaction : Entity, ITenantOwned, IAuditable
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? StatementId { get; set; }

    public DateOnly Date { get; set; }

    [Pii]
    public required string Description { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "MXN";
    public TransactionDirection Direction { get; set; }

    public Guid? CategoryId { get; set; }
    public Guid? AttributedUserId { get; set; }

    /// <summary>False while in the review queue; true once the user confirms the statement.</summary>
    public bool IsConfirmed { get; set; }
    public bool IsRecurring { get; set; }
    public CategorizationSource CategorizationSource { get; set; } = CategorizationSource.None;
    public double? Confidence { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
