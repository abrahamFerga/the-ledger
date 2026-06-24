using TheLedger.Domain.Common;

namespace TheLedger.Domain.Accounts;

public enum AccountType
{
    Checking,
    Savings,
    Card,
    Cash
}

/// <summary>A financial account within a household (checking, savings, card, or cash).</summary>
public sealed class Account : Entity, ITenantOwned, IAuditable
{
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public AccountType Type { get; set; }
    public string? Institution { get; set; }
    public string Currency { get; set; } = "MXN";

    /// <summary>Masked account/card number — full PANs are never persisted.</summary>
    [Pii]
    public string? MaskedNumber { get; set; }

    public decimal CurrentBalance { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
