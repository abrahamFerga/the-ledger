namespace TheLedger.Application.Abstractions;

/// <summary>
/// The resolved tenant/user for the current request, set once by the tenant-resolver
/// middleware from the OIDC token. EF query filters read <see cref="TenantId"/> to
/// enforce isolation; handlers read it to stamp writes and audit.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? UserId { get; }
    string? Role { get; }
    bool IsResolved { get; }

    void Resolve(Guid tenantId, Guid? userId, string? role);
}
