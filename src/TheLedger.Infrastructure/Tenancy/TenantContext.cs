using TheLedger.Application.Abstractions;

namespace TheLedger.Infrastructure.Tenancy;

/// <summary>Scoped, request-lifetime tenant context. Set once by the tenant-resolver middleware.</summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Role { get; private set; }
    public bool IsResolved { get; private set; }

    public void Resolve(Guid tenantId, Guid? userId, string? role)
    {
        TenantId = tenantId;
        UserId = userId;
        Role = role;
        IsResolved = true;
    }
}
