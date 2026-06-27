using System.Text.RegularExpressions;
using TheLedger.Application.Ingestion;

namespace TheLedger.Infrastructure.Categorization;

/// <summary>
/// Strips PII from free text before any external model call: card numbers (via <see cref="PanMasker"/>)
/// and long digit runs (account numbers / CLABE). Shared by the LLM categorizer and the NL quick-add
/// parser so every path that ships text to Azure OpenAI redacts identically (ADR-0004, ADR-0011).
/// </summary>
public static partial class MerchantRedactor
{
    [GeneratedRegex(@"\d{10,}")]
    private static partial Regex LongDigitRun();

    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var masked = PanMasker.Mask(text);
        return LongDigitRun().Replace(masked, "[redacted]");
    }
}
