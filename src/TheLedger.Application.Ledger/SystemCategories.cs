using TheLedger.Domain.Categories;

namespace TheLedger.Application.Ledger;

/// <summary>The built-in categories (TenantId empty), seeded once and shared across all households.</summary>
public static class SystemCategories
{
    public static readonly Guid Income = new("11111111-0000-0000-0000-000000000001");
    public static readonly Guid Groceries = new("11111111-0000-0000-0000-000000000002");
    public static readonly Guid Dining = new("11111111-0000-0000-0000-000000000003");
    public static readonly Guid Transport = new("11111111-0000-0000-0000-000000000004");
    public static readonly Guid Utilities = new("11111111-0000-0000-0000-000000000005");
    public static readonly Guid Shopping = new("11111111-0000-0000-0000-000000000006");
    public static readonly Guid Health = new("11111111-0000-0000-0000-000000000007");
    public static readonly Guid Entertainment = new("11111111-0000-0000-0000-000000000008");
    public static readonly Guid Transfers = new("11111111-0000-0000-0000-000000000009");
    public static readonly Guid Other = new("11111111-0000-0000-0000-000000000010");

    public static readonly IReadOnlyList<(Guid Id, string Name, CategoryKind Kind)> All =
    [
        (Income, "Income", CategoryKind.Income),
        (Groceries, "Groceries", CategoryKind.Expense),
        (Dining, "Dining", CategoryKind.Expense),
        (Transport, "Transport", CategoryKind.Expense),
        (Utilities, "Utilities", CategoryKind.Expense),
        (Shopping, "Shopping", CategoryKind.Expense),
        (Health, "Health", CategoryKind.Expense),
        (Entertainment, "Entertainment", CategoryKind.Expense),
        (Transfers, "Transfers", CategoryKind.Transfer),
        (Other, "Other", CategoryKind.Expense),
    ];
}

/// <summary>
/// Keyword → system-category fallback rules for Mexican merchants, applied when no learned rule
/// matches. The LLM categorizer (ADR-0004) supersedes these for low-confidence cases.
/// </summary>
public static class DefaultCategoryRules
{
    public static readonly IReadOnlyList<(string Keyword, Guid CategoryId)> Map =
    [
        ("OXXO", SystemCategories.Groceries),
        ("WALMART", SystemCategories.Groceries),
        ("SORIANA", SystemCategories.Groceries),
        ("CHEDRAUI", SystemCategories.Groceries),
        ("SUPER", SystemCategories.Groceries),
        ("CFE", SystemCategories.Utilities),
        ("TELMEX", SystemCategories.Utilities),
        ("IZZI", SystemCategories.Utilities),
        ("TOTALPLAY", SystemCategories.Utilities),
        ("AGUA", SystemCategories.Utilities),
        ("UBER", SystemCategories.Transport),
        ("DIDI", SystemCategories.Transport),
        ("PEMEX", SystemCategories.Transport),
        ("GASOLIN", SystemCategories.Transport),
        ("NOMINA", SystemCategories.Income),
        ("DEPOSITO", SystemCategories.Income),
        ("SPEI RECIBIDO", SystemCategories.Income),
        ("STARBUCKS", SystemCategories.Dining),
        ("RESTAURANTE", SystemCategories.Dining),
        ("MCDONALD", SystemCategories.Dining),
        ("AMAZON", SystemCategories.Shopping),
        ("MERPAGO", SystemCategories.Shopping),
        ("MERCADO", SystemCategories.Shopping),
        ("LIVERPOOL", SystemCategories.Shopping),
        ("SPOTIFY", SystemCategories.Entertainment),
        ("NETFLIX", SystemCategories.Entertainment),
        ("CINEPOLIS", SystemCategories.Entertainment),
        ("FARMACIA", SystemCategories.Health),
        ("HOSPITAL", SystemCategories.Health),
    ];
}
