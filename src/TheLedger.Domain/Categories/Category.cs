using TheLedger.Domain.Common;

namespace TheLedger.Domain.Categories;

public enum CategoryKind
{
    Income,
    Expense,
    Transfer
}

/// <summary>
/// A spending/income category. System defaults have <see cref="ITenantOwned.TenantId"/> = empty and
/// are visible to every tenant; custom categories belong to one household. The DbContext query
/// filter admits both (current tenant OR system).
/// </summary>
public sealed class Category : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public Guid? ParentId { get; set; }
    public CategoryKind Kind { get; set; }
    public bool IsSystem { get; set; }
}
