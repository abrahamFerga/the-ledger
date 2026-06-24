using TheLedger.Domain.Common;

namespace TheLedger.Domain.Budgeting;

/// <summary>A monthly spending target for a category. One per (category, month) within a household.</summary>
public sealed class Budget : Entity, ITenantOwned, IAuditable
{
    public Guid TenantId { get; set; }
    public Guid CategoryId { get; set; }

    /// <summary>First day of the budget month.</summary>
    public DateOnly PeriodMonth { get; set; }

    public decimal TargetAmount { get; set; }

    /// <summary>When true, the previous month's leftover carries into this month's available amount.</summary>
    public bool Rollover { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
