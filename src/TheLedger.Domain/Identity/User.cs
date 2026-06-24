using TheLedger.Domain.Common;

namespace TheLedger.Domain.Identity;

/// <summary>Roles bind to authorization policies, not to UI screens.</summary>
public enum UserRole
{
    Owner,
    Member,
    Viewer,
    Operator
}

/// <summary>A person within a household. Authenticated via an external OIDC identity.</summary>
public sealed class User : Entity, ITenantOwned, IAuditable
{
    public Guid TenantId { get; set; }

    [Pii]
    public required string Email { get; set; }

    [Pii]
    public string? DisplayName { get; set; }

    public UserRole Role { get; set; } = UserRole.Owner;

    /// <summary>Subject id from the OIDC provider (Entra External ID). Never a password.</summary>
    public required string ExternalAuthId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
