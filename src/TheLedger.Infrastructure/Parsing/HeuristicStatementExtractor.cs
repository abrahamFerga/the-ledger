using System.Globalization;
using System.Text.RegularExpressions;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Ingestion.Extraction;
using TheLedger.Domain.Ledger;

namespace TheLedger.Infrastructure.Parsing;

/// <summary>
/// Deterministic, offline statement-text extractor and the fast-path of ADR-0004. Detects
/// <c>dd/MMM/yyyy</c> transaction lines, opening/closing balances, and credit keywords. The
/// LLM-forward extractor (MAF + Azure OpenAI) is the production primary and swaps in behind
/// <see cref="IStatementExtractor"/>; this keeps a working, testable path with no external calls.
/// </summary>
public sealed partial class HeuristicStatementExtractor : IStatementExtractor
{
    [GeneratedRegex(@"\b(\d{1,2})/([A-Za-z]{3,4})/(\d{4})\b")]
    private static partial Regex DateToken();

    [GeneratedRegex(@"-?\d{1,3}(?:,\d{3})*\.\d{2}")]
    private static partial Regex MoneyToken();

    private static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ENE"] = 1, ["FEB"] = 2, ["MAR"] = 3, ["ABR"] = 4, ["MAY"] = 5, ["JUN"] = 6,
        ["JUL"] = 7, ["AGO"] = 8, ["SEP"] = 9, ["SET"] = 9, ["OCT"] = 10, ["NOV"] = 11, ["DIC"] = 12,
    };

    private static readonly string[] CreditKeywords =
    [
        "ABONO", "DEPOSITO", "DEPÓSITO", "NOMINA", "NÓMINA", "SPEI RECIBIDO",
        "PAGO RECIBIDO", "REEMBOLSO", "INTERES", "INTERÉS", "TRANSFERENCIA RECIBIDA",
    ];

    public Task<ExtractionResult> ExtractAsync(string statementText, string? bankHint, CancellationToken ct)
    {
        var bank = string.IsNullOrWhiteSpace(bankHint) ? DetectBank(statementText) : bankHint!;
        var opening = FindBalance(statementText, "ANTERIOR", "INICIAL");
        var closing = FindBalance(statementText, "FINAL", "AL CORTE", "ACTUAL", "NUEVO");

        var transactions = new List<ExtractedTransaction>();
        foreach (var raw in statementText.Split('\n'))
        {
            var line = raw.Trim();
            var dateMatch = DateToken().Match(line);
            if (!dateMatch.Success || !Months.TryGetValue(dateMatch.Groups[2].Value[..3], out var month))
            {
                continue;
            }

            var day = int.Parse(dateMatch.Groups[1].Value);
            var year = int.Parse(dateMatch.Groups[3].Value);
            DateOnly date;
            try
            {
                date = new DateOnly(year, month, day);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }

            var money = MoneyToken().Matches(line);
            if (money.Count == 0)
            {
                continue;
            }

            var amountToken = money[^1];
            if (!decimal.TryParse(amountToken.Value.Replace(",", string.Empty),
                    NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                continue;
            }

            var descStart = dateMatch.Index + dateMatch.Length;
            var description = (amountToken.Index > descStart ? line[descStart..amountToken.Index] : line[descStart..]).Trim();
            if (description.Length == 0)
            {
                description = "(sin descripción)";
            }

            var direction = IsCredit(line) ? TransactionDirection.Credit : TransactionDirection.Debit;
            transactions.Add(new ExtractedTransaction(date, PanMasker.Mask(description), Math.Abs(amount), direction));
        }

        return Task.FromResult(new ExtractionResult(transactions, opening, closing, bank));
    }

    private static bool IsCredit(string line) =>
        CreditKeywords.Any(k => line.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static string DetectBank(string text)
    {
        if (text.Contains("BBVA", StringComparison.OrdinalIgnoreCase)) return "BBVA";
        if (text.Contains("Santander", StringComparison.OrdinalIgnoreCase)) return "Santander";
        if (text.Contains("Banorte", StringComparison.OrdinalIgnoreCase)) return "Banorte";
        if (text.Contains("Klar", StringComparison.OrdinalIgnoreCase)) return "Klar";
        if (text.Contains("Hey Banco", StringComparison.OrdinalIgnoreCase)) return "Hey";
        if (text.Contains("nu.com", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(text, @"\bNu\b")) return "Nu";
        return "Unknown";
    }

    private static decimal? FindBalance(string text, params string[] keywords)
    {
        foreach (var line in text.Split('\n'))
        {
            if (!line.Contains("SALDO", StringComparison.OrdinalIgnoreCase) ||
                !keywords.Any(k => line.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var money = MoneyToken().Matches(line);
            if (money.Count > 0 &&
                decimal.TryParse(money[^1].Value.Replace(",", string.Empty),
                    NumberStyles.Number, CultureInfo.InvariantCulture, out var balance))
            {
                return balance;
            }
        }

        return null;
    }
}
