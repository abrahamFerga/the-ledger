using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Ledger;
using TheLedger.Domain.Categories;
using TheLedger.Domain.Ledger;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Services;

public sealed class LedgerService(LedgerDbContext db) : ILedgerService
{
    public async Task<IReadOnlyList<TransactionListItem>> GetFeedAsync(TransactionFeedQuery query, CancellationToken ct)
    {
        var q = db.Transactions.AsQueryable();
        if (query.ConfirmedOnly)
        {
            q = q.Where(t => t.IsConfirmed);
        }

        if (query.AccountId is { } accountId)
        {
            q = q.Where(t => t.AccountId == accountId);
        }

        if (query.CategoryId is { } categoryId)
        {
            q = q.Where(t => t.CategoryId == categoryId);
        }

        var transactions = await q.OrderByDescending(t => t.Date).ToListAsync(ct);
        var categories = await CategoryNamesAsync(ct);
        return transactions.Select(t => Map(t, categories)).ToList();
    }

    public async Task<TransactionListItem?> UpdateTransactionAsync(Guid id, UpdateTransactionRequest request, CancellationToken ct)
    {
        var transaction = await db.Transactions.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (transaction is null)
        {
            return null;
        }

        if (request.Description is { } description)
        {
            transaction.Description = PanMasker.Mask(description);
        }

        if (request.CategoryId is { } newCategory && newCategory != transaction.CategoryId)
        {
            transaction.CategoryId = newCategory;
            transaction.CategorizationSource = CategorizationSource.Manual;
            transaction.Confidence = 1.0;
            await LearnRuleAsync(transaction.Description, newCategory, ct); // learn from the correction
        }

        await db.SaveChangesAsync(ct);
        var categories = await CategoryNamesAsync(ct);
        return Map(transaction, categories);
    }

    public async Task<IReadOnlyList<TransactionListItem>> SplitTransactionAsync(Guid id, SplitTransactionRequest request, CancellationToken ct)
    {
        var original = await db.Transactions.FirstOrDefaultAsync(t => t.Id == id, ct)
                       ?? throw new KeyNotFoundException($"Transaction {id} not found.");

        var partsSum = request.Parts.Sum(p => p.Amount);
        if (Math.Abs(partsSum - original.Amount) > 0.01m)
        {
            throw new InvalidOperationException(
                $"Split parts ({partsSum}) must equal the original amount ({original.Amount}).");
        }

        var created = new List<Transaction>();
        foreach (var part in request.Parts)
        {
            var split = new Transaction
            {
                Id = Guid.CreateVersion7(),
                TenantId = original.TenantId,
                AccountId = original.AccountId,
                StatementId = original.StatementId,
                Date = original.Date,
                Description = PanMasker.Mask(part.Description),
                Amount = part.Amount,
                Currency = original.Currency,
                Direction = original.Direction,
                CategoryId = part.CategoryId,
                IsConfirmed = original.IsConfirmed,
                CategorizationSource = part.CategoryId is null ? CategorizationSource.None : CategorizationSource.Manual,
            };
            db.Transactions.Add(split);
            created.Add(split);
        }

        db.Transactions.Remove(original);
        await db.SaveChangesAsync(ct);

        var categories = await CategoryNamesAsync(ct);
        return created.Select(t => Map(t, categories)).ToList();
    }

    public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken ct)
    {
        var categories = await db.Categories.OrderBy(c => c.Name).ToListAsync(ct);
        return categories.Select(c => new CategoryDto(c.Id, c.Name, c.Kind.ToString(), c.IsSystem)).ToList();
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        var kind = Enum.TryParse<CategoryKind>(request.Kind, ignoreCase: true, out var k) ? k : CategoryKind.Expense;
        var category = new Category
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Kind = kind,
            IsSystem = false,
            // TenantId stamped by the interceptor.
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.Kind.ToString(), category.IsSystem);
    }

    private async Task LearnRuleAsync(string description, Guid categoryId, CancellationToken ct)
    {
        var pattern = description.Trim();
        if (pattern.Length == 0)
        {
            return;
        }

        if (pattern.Length > 60)
        {
            pattern = pattern[..60];
        }

        if (await db.CategorizationRules.AnyAsync(r => r.MatchPattern == pattern, ct))
        {
            return;
        }

        db.CategorizationRules.Add(new CategorizationRule
        {
            Id = Guid.CreateVersion7(),
            MatchPattern = pattern,
            CategoryId = categoryId,
            Priority = 100,
        });
    }

    private async Task<Dictionary<Guid, string>> CategoryNamesAsync(CancellationToken ct) =>
        await db.Categories.ToDictionaryAsync(c => c.Id, c => c.Name, ct);

    private static TransactionListItem Map(Transaction t, IReadOnlyDictionary<Guid, string> categories) =>
        new(t.Id, t.AccountId, t.Date, t.Description, t.Amount, t.Currency, t.Direction.ToString(),
            t.CategoryId,
            t.CategoryId is { } cid && categories.TryGetValue(cid, out var name) ? name : null,
            t.IsConfirmed);
}
