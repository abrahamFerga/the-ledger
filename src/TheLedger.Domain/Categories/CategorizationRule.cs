using TheLedger.Domain.Common;

namespace TheLedger.Domain.Categories;

/// <summary>
/// A learned categorization rule: when a transaction description contains <see cref="MatchPattern"/>,
/// assign <see cref="CategoryId"/>. Created when a user recategorizes a transaction, so the system
/// learns from corrections. Higher <see cref="Priority"/> wins.
/// </summary>
public sealed class CategorizationRule : Entity, ITenantOwned, IAuditable
{
    public Guid TenantId { get; set; }
    public required string MatchPattern { get; set; }
    public Guid CategoryId { get; set; }
    public int Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
