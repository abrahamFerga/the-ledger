using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TheLedger.Application.Ledger;
using TheLedger.Domain.Ledger;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Categorization;

/// <summary>
/// The registered <see cref="ICategorizer"/> (ADR-0004): rule fast-path first, then the LLM
/// categorizer for low-confidence cases when an <see cref="IChatClient"/> is configured. Falls back
/// to rules-only when no model is wired, so the system always categorizes deterministically.
/// </summary>
public sealed class CompositeCategorizer(LedgerDbContext db, IServiceProvider serviceProvider) : ICategorizer
{
    private readonly RuleCategorizer _rules = new(db);

    public async Task<CategorizationResult> CategorizeAsync(string description, CancellationToken ct)
    {
        var ruleResult = await _rules.CategorizeAsync(description, ct);
        if (ruleResult.Source != CategorizationSource.None)
        {
            return ruleResult;
        }

        var chat = serviceProvider.GetService<IChatClient>();
        if (chat is null)
        {
            return ruleResult; // no model configured — stay rules-only
        }

        return await new LlmCategorizer(chat, db).CategorizeAsync(description, ct);
    }
}
