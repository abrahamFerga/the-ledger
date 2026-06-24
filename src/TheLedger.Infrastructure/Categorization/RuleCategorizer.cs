using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Ledger;
using TheLedger.Domain.Categories;
using TheLedger.Domain.Ledger;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Categorization;

/// <summary>
/// Rule-based categorizer (ADR-0004 fast-path): learned tenant rules first, then Mexican-merchant
/// defaults. Rules are loaded once per scope and reused across a batch import. The LLM-forward
/// categorizer slots in behind <see cref="ICategorizer"/> for low-confidence cases.
/// </summary>
public sealed class RuleCategorizer(LedgerDbContext db) : ICategorizer
{
    private List<CategorizationRule>? _rules;

    public async Task<CategorizationResult> CategorizeAsync(string description, CancellationToken ct)
    {
        _rules ??= await db.CategorizationRules.OrderByDescending(r => r.Priority).ToListAsync(ct);
        var upper = description.ToUpperInvariant();

        foreach (var rule in _rules)
        {
            if (upper.Contains(rule.MatchPattern.ToUpperInvariant()))
            {
                return new CategorizationResult(rule.CategoryId, CategorizationSource.Rule, 0.95);
            }
        }

        foreach (var (keyword, categoryId) in DefaultCategoryRules.Map)
        {
            if (upper.Contains(keyword))
            {
                return new CategorizationResult(categoryId, CategorizationSource.Rule, 0.80);
            }
        }

        return new CategorizationResult(null, CategorizationSource.None, null);
    }
}
