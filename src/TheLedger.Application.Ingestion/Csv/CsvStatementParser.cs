using System.Globalization;
using TheLedger.Domain.Ledger;

namespace TheLedger.Application.Ingestion.Csv;

public sealed record ParsedTransaction(DateOnly Date, string Description, decimal Amount, TransactionDirection Direction);

/// <summary>
/// Header-driven CSV parser for Mexican statement exports. Detects common Spanish/English column
/// names (fecha/date, descripción/concepto, monto/importe, cargo/abono) and maps rows to
/// transactions. Unparseable rows are skipped. Robust quoted-field CSV (CsvHelper) is a follow-up.
/// </summary>
public static class CsvStatementParser
{
    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "dd/MM/yy", "yyyy/MM/dd"];

    public static IReadOnlyList<ParsedTransaction> Parse(string content)
    {
        var results = new List<ParsedTransaction>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return results;
        }

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            return results;
        }

        var header = SplitLine(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToArray();
        int dateCol = IndexOfAny(header, "fecha", "date");
        int descCol = IndexOfAny(header, "descripcion", "descripción", "concepto", "detalle", "description");
        int amountCol = IndexOfAny(header, "monto", "importe", "amount");
        int chargeCol = IndexOfAny(header, "cargo", "cargos", "retiro", "debito", "débito", "charge");
        int creditCol = IndexOfAny(header, "abono", "abonos", "deposito", "depósito", "credito", "crédito", "deposit");

        for (var i = 1; i < lines.Length; i++)
        {
            var cells = SplitLine(lines[i]);
            if (!TryParseDate(Get(cells, dateCol), out var date))
            {
                continue;
            }

            var description = Get(cells, descCol).Trim();
            if (description.Length == 0)
            {
                description = "(no description)";
            }

            decimal amount;
            TransactionDirection direction;

            if (chargeCol >= 0 || creditCol >= 0)
            {
                var charge = TryParseAmount(Get(cells, chargeCol), out var c) ? c : 0m;
                var credit = TryParseAmount(Get(cells, creditCol), out var cr) ? cr : 0m;
                if (credit > 0)
                {
                    amount = credit;
                    direction = TransactionDirection.Credit;
                }
                else if (charge > 0)
                {
                    amount = charge;
                    direction = TransactionDirection.Debit;
                }
                else
                {
                    continue;
                }
            }
            else if (TryParseAmount(Get(cells, amountCol), out var signed))
            {
                direction = signed < 0 ? TransactionDirection.Debit : TransactionDirection.Credit;
                amount = Math.Abs(signed);
            }
            else
            {
                continue;
            }

            results.Add(new ParsedTransaction(date, PanMasker.Mask(description), amount, direction));
        }

        return results;
    }

    private static int IndexOfAny(string[] header, params string[] names)
    {
        for (var i = 0; i < header.Length; i++)
        {
            if (names.Contains(header[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static string Get(string[] cells, int index) =>
        index >= 0 && index < cells.Length ? cells[index] : string.Empty;

    private static bool TryParseDate(string value, out DateOnly date)
    {
        value = value.Trim();
        if (DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseAmount(string value, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var cleaned = value.Replace("$", string.Empty).Replace("MXN", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out amount);
    }

    private static string[] SplitLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in line)
        {
            switch (ch)
            {
                case '"':
                    inQuotes = !inQuotes;
                    break;
                case ',' when !inQuotes:
                    fields.Add(current.ToString());
                    current.Clear();
                    break;
                default:
                    current.Append(ch);
                    break;
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
