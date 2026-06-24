using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Ledger;
using TheLedger.Domain.Ledger;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Categorization;

/// <summary>
/// LLM-forward categorizer (ADR-0004): classifies a PII-redacted merchant description into one of the
/// available categories via an MEAI <see cref="IChatClient"/>. Used for transactions the rule
/// fast-path could not categorize. The concrete client (Azure OpenAI) is wired by configuration.
/// </summary>
public sealed partial class LlmCategorizer(IChatClient chat, LedgerDbContext db)
{
    [GeneratedRegex(@"\d{10,}")]
    private static partial Regex LongDigitRun();

    public async Task<CategorizationResult> CategorizeAsync(string description, CancellationToken ct)
    {
        var categories = await db.Categories.Select(c => new { c.Id, c.Name }).ToListAsync(ct);
        if (categories.Count == 0)
        {
            return new CategorizationResult(null, CategorizationSource.None, null);
        }

        var names = string.Join(", ", categories.Select(c => c.Name).Distinct());
        var prompt =
            "You categorize Mexican bank transactions. Reply with EXACTLY ONE category name from the list, nothing else.\n" +
            $"Categories: {names}\n" +
            $"Description: {Redact(description)}\n" +
            "Category:";

        var response = await chat.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)], cancellationToken: ct);
        var answer = response.Text.Trim();

        var match = categories.FirstOrDefault(c => string.Equals(c.Name, answer, StringComparison.OrdinalIgnoreCase))
                    ?? categories.FirstOrDefault(c => answer.Contains(c.Name, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? new CategorizationResult(null, CategorizationSource.None, null)
            : new CategorizationResult(match.Id, CategorizationSource.Llm, 0.7);
    }

    /// <summary>Strips card numbers and long digit runs (account numbers / CLABE) before the model call.</summary>
    private static string Redact(string description)
    {
        var masked = PanMasker.Mask(description);
        return LongDigitRun().Replace(masked, "[redacted]");
    }
}
