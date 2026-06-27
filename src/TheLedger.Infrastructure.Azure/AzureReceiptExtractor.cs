using System.Globalization;
using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TheLedger.Application.Ingestion.Receipts;

namespace TheLedger.Infrastructure.Azure;

/// <summary>
/// Receipt/ticket OCR via Azure AI Document Intelligence's <c>prebuilt-receipt</c> model (ADR-0009).
/// Auth is Managed Identity (<see cref="DefaultAzureCredential"/>) against a configured endpoint — no
/// key or secret in source. This is the only place the cloud SDK types appear; the rest of the system
/// sees only <see cref="IReceiptExtractor"/> and the cloud-agnostic <see cref="ReceiptExtractionResult"/>.
/// </summary>
public sealed class AzureReceiptExtractor(DocumentIntelligenceClient client) : IReceiptExtractor
{
    private const string ReceiptModelId = "prebuilt-receipt";

    public async Task<ReceiptExtractionResult> ExtractAsync(byte[] image, string? contentType, CancellationToken ct)
    {
        if (image.Length == 0)
        {
            return ReceiptExtractionResult.Empty;
        }

        var operation = await client.AnalyzeDocumentAsync(
            WaitUntil.Completed, ReceiptModelId, BinaryData.FromBytes(image), ct);

        var result = operation.Value;
        var document = result.Documents.Count > 0 ? result.Documents[0] : null;
        if (document is null)
        {
            return ReceiptExtractionResult.Empty;
        }

        var fields = document.Fields;

        var merchant = GetString(fields, "MerchantName");
        var date = GetDate(fields, "TransactionDate");
        var total = GetCurrencyAmount(fields, "Total");
        var tax = GetCurrencyAmount(fields, "TotalTax");
        var currency = GetCurrencyCode(fields, "Total") ?? "MXN";
        var items = GetLineItems(fields);

        return new ReceiptExtractionResult(merchant, date, total, tax, currency, items, document.Confidence);
    }

    private static string? GetString(IReadOnlyDictionary<string, DocumentField> fields, string key) =>
        fields.TryGetValue(key, out var field) && field.FieldType == DocumentFieldType.String
            ? field.ValueString
            : null;

    private static DateOnly? GetDate(IReadOnlyDictionary<string, DocumentField> fields, string key)
    {
        if (fields.TryGetValue(key, out var field) && field.FieldType == DocumentFieldType.Date
            && field.ValueDate is { } value)
        {
            return DateOnly.FromDateTime(value.DateTime);
        }

        return null;
    }

    private static decimal? GetCurrencyAmount(IReadOnlyDictionary<string, DocumentField> fields, string key)
    {
        if (fields.TryGetValue(key, out var field))
        {
            if (field.FieldType == DocumentFieldType.Currency && field.ValueCurrency is { } currency)
            {
                return (decimal)currency.Amount;
            }

            if (field.FieldType == DocumentFieldType.Double && field.ValueDouble is { } d)
            {
                return (decimal)d;
            }
        }

        return null;
    }

    private static string? GetCurrencyCode(IReadOnlyDictionary<string, DocumentField> fields, string key)
    {
        if (fields.TryGetValue(key, out var field)
            && field.FieldType == DocumentFieldType.Currency
            && field.ValueCurrency is { } currency
            && !string.IsNullOrWhiteSpace(currency.CurrencyCode))
        {
            return currency.CurrencyCode.ToUpperInvariant();
        }

        return null;
    }

    private static IReadOnlyList<ReceiptLineItem> GetLineItems(IReadOnlyDictionary<string, DocumentField> fields)
    {
        if (!fields.TryGetValue("Items", out var itemsField)
            || itemsField.FieldType != DocumentFieldType.List
            || itemsField.ValueList is null)
        {
            return [];
        }

        var items = new List<ReceiptLineItem>();
        foreach (var item in itemsField.ValueList)
        {
            if (item.FieldType != DocumentFieldType.Dictionary || item.ValueDictionary is null)
            {
                continue;
            }

            var description = GetString(item.ValueDictionary, "Description") ?? "(item)";
            var price = GetCurrencyAmount(item.ValueDictionary, "TotalPrice");
            items.Add(new ReceiptLineItem(description, price, item.Confidence));
        }

        return items;
    }
}

public static class AzureReceiptExtractorExtensions
{
    /// <summary>
    /// Registers the Azure Document Intelligence receipt extractor when
    /// <c>DocumentIntelligence:Endpoint</c> is configured (Managed Identity auth), overriding the
    /// deterministic fake. When unconfigured, nothing is registered and the fake stays in place — so
    /// CI runs with no Azure dependency (ADR-0009).
    /// </summary>
    public static IServiceCollection AddAzureDocumentIntelligence(this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["DocumentIntelligence:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return services;
        }

        var client = new DocumentIntelligenceClient(new Uri(endpoint), new DefaultAzureCredential());
        services.AddSingleton(client);
        services.AddScoped<IReceiptExtractor, AzureReceiptExtractor>();
        return services;
    }
}
