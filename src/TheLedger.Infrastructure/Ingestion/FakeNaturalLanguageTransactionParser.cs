using System.Globalization;
using System.Text.RegularExpressions;
using TheLedger.Application.Ingestion.QuickAdd;
using TheLedger.Application.Ledger;
using TheLedger.Domain.Ledger;

namespace TheLedger.Infrastructure.Ingestion;

/// <summary>
/// Deterministic, offline natural-language parser (ADR-0011). It is the default when no
/// <c>IChatClient</c> is configured (mirroring how <c>CompositeCategorizer</c> stays rules-only without a
/// model), so dev/CI need no Azure dependency, and it also serves as the cheap pre-pass for trivial
/// "&lt;amount&gt; &lt;merchant&gt; &lt;when&gt;" phrasing. Amounts, relative Spanish dates, and income-vs-expense
/// direction are parsed deterministically; the merchant is run through <see cref="ICategorizer"/> for the
/// proposed category (never invented). The result is a draft — it is not persisted.
/// </summary>
public sealed partial class FakeNaturalLanguageTransactionParser(ICategorizer categorizer, TimeProvider clock)
    : INaturalLanguageTransactionParser
{
    // A contiguous money token: 350 | 1250.50 | 1,250.50 | 1.250,50. Grabs the whole run of digits and
    // separators, then NormalizeAmount() decides which separator is decimal vs thousands.
    [GeneratedRegex(@"\d[\d.,]*\d|\d")]
    private static partial Regex AmountToken();

    // Income verbs/markers in Mexican Spanish: cobré, me pagaron, ingreso, depósito, nómina, recibí.
    private static readonly string[] IncomeMarkers =
        ["cobre", "cobré", "me pagaron", "ingreso", "deposito", "depósito", "nomina", "nómina", "recibi", "recibí", "abono"];

    // "en el Oxxo", "en restaurante", "a Juan" — capture the merchant phrase after the preposition.
    [GeneratedRegex(@"\b(?:en|a|al|para|de)\s+(?:el|la|los|las)?\s*(?<merchant>[\p{L}][\p{L}\s'&.\-]*?)\s*(?:ayer|antier|anteayer|hoy|ma[nñ]ana|el\s+\w+|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MerchantPhrase();

    public async Task<TransactionDraft> ParseAsync(QuickAddRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var text = request.Text ?? string.Empty;
        var lowered = text.ToLowerInvariant();

        var amount = ExtractAmount(text);
        var direction = IncomeMarkers.Any(lowered.Contains)
            ? TransactionDirection.Credit
            : TransactionDirection.Debit;

        var date = MexicoCityClock.ResolveRelative(text, MexicoCityClock.Today(clock));
        var merchant = ExtractMerchant(text);

        Guid? categoryId = null;
        double categoryConfidence = 0;
        if (!string.IsNullOrWhiteSpace(merchant))
        {
            var categorization = await categorizer.CategorizeAsync(merchant!, ct);
            categoryId = categorization.CategoryId;
            categoryConfidence = categorization.Confidence ?? 0;
        }

        // Confidence is a blend: we are confident when we found a clean amount and a merchant we could
        // categorize. A missing amount sharply lowers it so the UI pre-fills the form rather than asserting.
        var confidence = amount > 0 ? 0.6 : 0.2;
        if (categoryId is not null)
        {
            confidence = Math.Min(1.0, confidence + (categoryConfidence * 0.3));
        }

        return new TransactionDraft(
            Amount: amount,
            Currency: "MXN",
            Date: date,
            Direction: direction,
            Merchant: merchant,
            ProposedCategoryId: categoryId,
            Confidence: Math.Round(confidence, 2));
    }

    private static decimal ExtractAmount(string text)
    {
        var match = AmountToken().Match(text);
        return match.Success ? NormalizeAmount(match.Value) : 0m;
    }

    /// <summary>
    /// Turns a raw money token (350, 1250.50, 1,250.50, 1.250,50) into an invariant decimal. The last
    /// '.' or ',' that is followed by 1-2 digits is treated as the decimal separator; all other separators
    /// are thousands grouping and stripped.
    /// </summary>
    private static decimal NormalizeAmount(string raw)
    {
        var lastDot = raw.LastIndexOf('.');
        var lastComma = raw.LastIndexOf(',');
        var decimalSepIndex = Math.Max(lastDot, lastComma);

        string normalized;
        if (decimalSepIndex >= 0 && raw.Length - decimalSepIndex - 1 is >= 1 and <= 2)
        {
            // Strip every separator except the decimal one, then standardize it to '.'.
            var integerPart = raw[..decimalSepIndex].Replace(",", string.Empty).Replace(".", string.Empty);
            var fractionPart = raw[(decimalSepIndex + 1)..];
            normalized = $"{integerPart}.{fractionPart}";
        }
        else
        {
            normalized = raw.Replace(",", string.Empty).Replace(".", string.Empty);
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? Math.Abs(value)
            : 0m;
    }

    private static string? ExtractMerchant(string text)
    {
        var match = MerchantPhrase().Match(text);
        if (!match.Success)
        {
            return null;
        }

        var merchant = match.Groups["merchant"].Value.Trim();
        return string.IsNullOrWhiteSpace(merchant) ? null : merchant;
    }
}
