using TheLedger.Domain.Common;

namespace TheLedger.Domain.Tenants;

/// <summary>A household — the multi-tenancy root. Everything tenant-owned hangs off this.</summary>
public sealed class Tenant : Entity, IAuditable
{
    public required string Name { get; set; }
    public string Plan { get; set; } = "free";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
