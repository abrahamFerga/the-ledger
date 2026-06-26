using System.Globalization;

namespace TheLedger.Application.Ingestion.Receipts;

/// <summary>
/// Deterministic, offline <see cref="IReceiptExtractor"/> for dev and tests (ADR-0009): no Azure
/// dependency, so CI runs without a Document Intelligence endpoint. It treats the image bytes as a
/// UTF-8 "receipt text" with simple <c>KEY: value</c> lines (merchant, date, total, tax, currency,
/// item) so a test can feed an exact, repeatable result. Unparseable bytes yield a low-confidence
/// fallback that still exercises the review-queue flagging path.
/// </summary>
public sealed class FakeReceiptExtractor : IReceiptExtractor
{
    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy/MM/dd"];

    public Task<ReceiptExtractionResult> ExtractAsync(byte[] image, string? contentType, CancellationToken ct)
    {
        if (image.Length == 0)
        {
            return Task.FromResult(ReceiptExtractionResult.Empty);
        }

        string text;
        try
        {
            text = System.Text.Encoding.UTF8.GetString(image);
        }
        catch (ArgumentException)
        {
            return Task.FromResult(LowConfidenceFallback());
        }

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<ReceiptLineItem>();

        foreach (var raw in text.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = raw.IndexOf(':');
            if (idx <= 0)
            {
                continue;
            }

            var key = raw[..idx].Trim();
            var value = raw[(idx + 1)..].Trim();

            if (key.Equals("item", StringComparison.OrdinalIgnoreCase))
            {
                items.Add(ParseItem(value));
            }
            else
            {
                fields[key] = value;
            }
        }

        if (fields.Count == 0 && items.Count == 0)
        {
            return Task.FromResult(LowConfidenceFallback());
        }

        var confidence = fields.TryGetValue("confidence", out var conf)
            && double.TryParse(conf, NumberStyles.Float, CultureInfo.InvariantCulture, out var c)
            ? c
            : 0.92;

        var result = new ReceiptExtractionResult(
            MerchantName: fields.GetValueOrDefault("merchant"),
            TransactionDate: ParseDate(fields.GetValueOrDefault("date")),
            Total: ParseAmount(fields.GetValueOrDefault("total")),
            Tax: ParseAmount(fields.GetValueOrDefault("tax")),
            Currency: fields.TryGetValue("currency", out var cur) && !string.IsNullOrWhiteSpace(cur)
                ? cur.ToUpperInvariant()
                : "MXN",
            LineItems: items,
            OverallConfidence: confidence);

        return Task.FromResult(result);
    }

    private static ReceiptLineItem ParseItem(string value)
    {
        // "Descripcion=12.50" or just "Descripcion"
        var eq = value.LastIndexOf('=');
        if (eq > 0 && ParseAmount(value[(eq + 1)..]) is { } amount)
        {
            return new ReceiptLineItem(value[..eq].Trim(), amount, 0.9);
        }

        return new ReceiptLineItem(value, null, 0.9);
    }

    private static ReceiptExtractionResult LowConfidenceFallback() =>
        new("UNREADABLE", null, null, null, "MXN", [], 0.10);

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            return exact;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;
    }

    private static decimal? ParseAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Replace("$", string.Empty)
            .Replace("MXN", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : null;
    }
}
