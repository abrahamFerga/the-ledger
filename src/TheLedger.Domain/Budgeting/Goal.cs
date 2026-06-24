using TheLedger.Domain.Common;

namespace TheLedger.Domain.Budgeting;

/// <summary>
/// A named savings goal. Progress is the contributed <see cref="CurrentAmount"/>, or — when
/// <see cref="LinkedAccountId"/> is set — the linked account's balance.
/// </summary>
public sealed class Goal : Entity, ITenantOwned, IAuditable
{
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateOnly? TargetDate { get; set; }
    public Guid? LinkedAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
