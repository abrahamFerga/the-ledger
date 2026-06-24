using TheLedger.Domain.Ledger;

namespace TheLedger.Application.Ledger;

public sealed record TransactionListItem(
    Guid Id, Guid AccountId, DateOnly Date, string Description, decimal Amount, string Currency,
    string Direction, Guid? CategoryId, string? CategoryName, bool IsConfirmed);

public sealed record TransactionFeedQuery(Guid? AccountId, Guid? CategoryId, bool ConfirmedOnly = true);

public sealed record UpdateTransactionRequest(string? Description, Guid? CategoryId);

public sealed record SplitPart(string Description, decimal Amount, Guid? CategoryId);

public sealed record SplitTransactionRequest(IReadOnlyList<SplitPart> Parts);

public sealed record CategoryDto(Guid Id, string Name, string Kind, bool IsSystem);

public sealed record CreateCategoryRequest(string Name, string Kind);

/// <summary>The unified ledger: a combined, filterable transaction feed with edit/split/recategorize.</summary>
public interface ILedgerService
{
    Task<IReadOnlyList<TransactionListItem>> GetFeedAsync(TransactionFeedQuery query, CancellationToken ct);
    Task<TransactionListItem?> UpdateTransactionAsync(Guid id, UpdateTransactionRequest request, CancellationToken ct);
    Task<IReadOnlyList<TransactionListItem>> SplitTransactionAsync(Guid id, SplitTransactionRequest request, CancellationToken ct);
    Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken ct);
    Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct);
}

public sealed record CategorizationResult(Guid? CategoryId, CategorizationSource Source, double? Confidence);

/// <summary>
/// Assigns a category to a transaction description. The rule-based implementation checks learned
/// rules then Mexican-merchant defaults; the LLM-forward implementation (ADR-0004) slots in here.
/// </summary>
public interface ICategorizer
{
    Task<CategorizationResult> CategorizeAsync(string description, CancellationToken ct);
}
