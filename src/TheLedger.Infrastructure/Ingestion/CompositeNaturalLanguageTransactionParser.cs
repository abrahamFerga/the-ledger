using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TheLedger.Application.Abstractions;
using TheLedger.Application.Ingestion.QuickAdd;
using TheLedger.Application.Ledger;
using TheLedger.Domain.Consent;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Ingestion;

/// <summary>
/// The registered <see cref="INaturalLanguageTransactionParser"/> (ADR-0011). It uses the LLM-forward
/// parser only when (a) an <see cref="IChatClient"/> is configured and (b) the current user has granted the
/// existing LLM opt-in consent (<see cref="ConsentType.LlmCategorization"/>) — the same gate the categorizer
/// path relies on. Otherwise it falls back to the deterministic fake, so quick-add always works (rules-only,
/// no external call) and dev/CI need no Azure dependency. Mirrors how <c>CompositeCategorizer</c> degrades.
/// </summary>
public sealed class CompositeNaturalLanguageTransactionParser(
    LedgerDbContext db,
    ITenantContext tenant,
    ICategorizer categorizer,
    TimeProvider clock,
    IServiceProvider serviceProvider) : INaturalLanguageTransactionParser
{
    public async Task<TransactionDraft> ParseAsync(QuickAddRequest request, CancellationToken ct)
    {
        var chat = serviceProvider.GetService<IChatClient>();
        if (chat is not null && await HasLlmConsentAsync(ct))
        {
            return await new LlmNaturalLanguageTransactionParser(chat, categorizer, clock).ParseAsync(request, ct);
        }

        // No model wired, or the user has not opted in → deterministic, no external call.
        return await new FakeNaturalLanguageTransactionParser(categorizer, clock).ParseAsync(request, ct);
    }

    private async Task<bool> HasLlmConsentAsync(CancellationToken ct)
    {
        if (tenant.UserId is not { } userId)
        {
            return false;
        }

        return await db.Consents.AnyAsync(
            c => c.UserId == userId && c.Type == ConsentType.LlmCategorization, ct);
    }
}
