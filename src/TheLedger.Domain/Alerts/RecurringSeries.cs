using TheLedger.Domain.Common;

namespace TheLedger.Domain.Alerts;

public enum RecurringCadence
{
    Unknown,
    Weekly,
    Monthly
}

/// <summary>A detected recurring transaction (salary, subscription, bill) used to forecast upcoming bills.</summary>
public sealed class RecurringSeries : Entity, ITenantOwned, IAuditable
{
    public Guid TenantId { get; set; }
    public required string Merchant { get; set; }
    public RecurringCadence Cadence { get; set; }
    public decimal ExpectedAmount { get; set; }
    public DateOnly LastSeen { get; set; }
    public DateOnly NextExpectedDate { get; set; }
    public int OccurrenceCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
