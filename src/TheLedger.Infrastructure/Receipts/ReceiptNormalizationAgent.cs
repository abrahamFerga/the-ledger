using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Ingestion.Receipts;
using TheLedger.Application.Ledger;
using TheLedger.Domain.Ledger;

namespace TheLedger.Infrastructure.Receipts;

/// <summary>
/// Normalized output of a receipt: a cleaned merchant string + a proposed category, derived from the
/// raw <see cref="ReceiptExtractionResult"/> the OCR step produced.
/// </summary>
public sealed record ReceiptNormalization(string Merchant, Guid? CategoryId, CategorizationSource Source, double? Confidence);

/// <summary>
/// The <c>ReceiptNormalizationAgent</c> (ADR-0009): takes the structured fields Document Intelligence
/// read off a ticket and (1) normalizes the messy Mexican merchant string via the existing Azure
/// OpenAI <see cref="IChatClient"/> when one is configured, then (2) proposes a category by reusing
/// the existing <see cref="ICategorizer"/>. Document Intelligence does the OCR — the LLM only cleans
/// the merchant string. PII is redacted before any model call; when no model is wired it falls back
/// to the raw merchant (rules-only categorization), so the worker always produces a result.
/// </summary>
public sealed partial class ReceiptNormalizationAgent(IServiceProvider serviceProvider, ICategorizer categorizer)
{
    [GeneratedRegex(@"\d{10,}")]
    private static partial Regex LongDigitRun();

    public async Task<ReceiptNormalization> NormalizeAsync(ReceiptExtractionResult extraction, CancellationToken ct)
    {
        var rawMerchant = string.IsNullOrWhiteSpace(extraction.MerchantName)
            ? "(unknown merchant)"
            : extraction.MerchantName!;

        var merchant = await NormalizeMerchantAsync(rawMerchant, ct);

        // Reuse the existing categorizer (rules fast-path, then LLM when configured; ADR-0004).
        var category = await categorizer.CategorizeAsync(merchant, ct);

        return new ReceiptNormalization(merchant, category.CategoryId, category.Source, category.Confidence);
    }

    private async Task<string> NormalizeMerchantAsync(string rawMerchant, CancellationToken ct)
    {
        var chat = serviceProvider.GetService<IChatClient>();
        if (chat is null)
        {
            return Clean(rawMerchant); // no model configured — keep the cleaned raw string
        }

        var prompt =
            "You normalize messy Mexican store-receipt merchant names into a clean, human-readable brand name.\n" +
            "Reply with ONLY the normalized name, no punctuation, no explanation.\n" +
            $"Raw merchant: {Redact(rawMerchant)}\n" +
            "Normalized:";

        var response = await chat.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)], cancellationToken: ct);
        var answer = response.Text.Trim();
        return string.IsNullOrWhiteSpace(answer) ? Clean(rawMerchant) : Clean(answer);
    }

    /// <summary>Masks card numbers and long digit runs (account numbers / CLABE) before the model call.</summary>
    private static string Redact(string value)
    {
        var masked = PanMasker.Mask(value);
        return LongDigitRun().Replace(masked, "[redacted]");
    }

    /// <summary>Trims and collapses whitespace; always PAN-masks so nothing card-like is persisted.</summary>
    private static string Clean(string value)
    {
        var collapsed = Regex.Replace(value, @"\s+", " ").Trim();
        var masked = PanMasker.Mask(collapsed);
        return masked.Length > 200 ? masked[..200] : masked;
    }
}
