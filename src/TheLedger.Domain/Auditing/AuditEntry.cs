using TheLedger.Domain.Common;

namespace TheLedger.Domain.Auditing;

/// <summary>
/// Append-only record of a domain mutation: who/what/when/tenant/before-after.
/// Not <see cref="ITenantOwned"/> on purpose — it is a cross-cutting log queried only
/// by operators through audited tooling, so the tenant query filter must not hide it.
/// </summary>
public sealed class AuditEntry : Entity
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }

    /// <summary>Created | Updated | Deleted.</summary>
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }

    [Pii]
    public string? Before { get; set; }

    [Pii]
    public string? After { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
