namespace TheLedger.Domain.Common;

/// <summary>Base type for all entities. Sequential GUID key assigned by the data layer.</summary>
public abstract class Entity
{
    public Guid Id { get; set; }
}

/// <summary>
/// Marks an entity as owned by a tenant (household). A global EF query filter on
/// <see cref="TenantId"/> enforces isolation; writes stamp it automatically.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; set; }
}

/// <summary>Carries created/updated timestamps maintained by the audit interceptor.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// Tags a property as personally identifiable information. Flows through audit
/// logging and the GDPR/ARCO data-export so PII handling is explicit, not incidental.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class PiiAttribute : Attribute;
