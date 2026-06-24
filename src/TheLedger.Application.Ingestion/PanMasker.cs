using System.Text.RegularExpressions;

namespace TheLedger.Application.Ingestion;

/// <summary>
/// Masks card numbers (PANs) in free text before persistence — statements and descriptions can
/// contain them, and a full PAN must never be stored (PCI scope). Keeps the last 4 digits.
/// </summary>
public static partial class PanMasker
{
    [GeneratedRegex(@"\b(?:\d[ -]?){13,19}\b")]
    private static partial Regex CardLike();

    public static string Mask(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return CardLike().Replace(input, match =>
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            if (digits.Length is < 13 or > 19)
            {
                return match.Value;
            }

            return $"****{digits[^4..]}";
        });
    }

    /// <summary>Masks an account/card number down to its last 4 digits for display.</summary>
    public static string? MaskNumber(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        var digits = new string(number.Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? $"****{digits[^4..]}" : "****";
    }
}
