using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using TheLedger.Application.Ingestion.QuickAdd;
using TheLedger.Application.Ledger;
using TheLedger.Domain.Ledger;
using TheLedger.Infrastructure.Categorization;

namespace TheLedger.Infrastructure.Ingestion;

/// <summary>
/// LLM-forward natural-language parser (the <c>QuickAddParserAgent</c>, ADR-0011) built on the existing
/// Azure OpenAI <see cref="IChatClient"/> — the same client the categorizer uses. The model returns a
/// <b>schema-validated</b> structured response (amount, currency, relative-date phrase, direction, merchant);
/// the date is resolved server-side against <c>America/Mexico_City</c> and the category is proposed by reusing
/// <see cref="ICategorizer"/> (the LLM never invents a category id). PII is redacted before the call. The
/// returned draft is never persisted without explicit user confirmation.
/// </summary>
public sealed class LlmNaturalLanguageTransactionParser(
    IChatClient chat,
    ICategorizer categorizer,
    TimeProvider clock) : INaturalLanguageTransactionParser
{
    private const string SystemPrompt =
        "Eres un asistente que extrae una transacción financiera de una frase en español mexicano. " +
        "Devuelve SOLO los campos del esquema. " +
        "amount = monto positivo en número; currency = código ISO (MXN por defecto); " +
        "direction = \"Credit\" si es un ingreso (cobré, me pagaron, depósito, nómina) o \"Debit\" si es un gasto; " +
        "merchant = el comercio o concepto (p. ej. \"Oxxo\", \"restaurante\"); " +
        "relativeDate = la expresión temporal tal cual aparece (\"hoy\", \"ayer\", \"antier\", \"el lunes\") o \"hoy\" si no se menciona. " +
        "No inventes una categoría.";

    public async Task<TransactionDraft> ParseAsync(QuickAddRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var redacted = MerchantRedactor.Redact(request.Text ?? string.Empty);

        var options = new ChatOptions
        {
            Temperature = 0f,
            MaxOutputTokens = 300,
            ResponseFormat = ChatResponseFormat.ForJsonSchema<ParsedPhrase>(),
        };

        var response = await chat.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, SystemPrompt),
                new ChatMessage(ChatRole.User, redacted),
            ],
            options,
            ct);

        var parsed = Deserialize(response.Text);

        // Server-side validation + normalization: never trust the model's raw shape.
        var amount = Math.Abs(parsed?.Amount ?? 0m);
        var currency = NormalizeCurrency(parsed?.Currency);
        var direction = string.Equals(parsed?.Direction, "Credit", StringComparison.OrdinalIgnoreCase)
            ? TransactionDirection.Credit
            : TransactionDirection.Debit;
        var date = MexicoCityClock.ResolveRelative(parsed?.RelativeDate, MexicoCityClock.Today(clock));
        var merchant = string.IsNullOrWhiteSpace(parsed?.Merchant) ? null : parsed!.Merchant!.Trim();

        // Category is proposed by the existing categorizer (rules + LLM), not by this parse — merchant → category.
        Guid? categoryId = null;
        double categoryConfidence = 0;
        if (merchant is not null)
        {
            var categorization = await categorizer.CategorizeAsync(merchant, ct);
            categoryId = categorization.CategoryId;
            categoryConfidence = categorization.Confidence ?? 0;
        }

        var confidence = amount > 0 ? 0.75 : 0.3;
        if (categoryId is not null)
        {
            confidence = Math.Min(1.0, confidence + (categoryConfidence * 0.2));
        }

        return new TransactionDraft(
            Amount: amount,
            Currency: currency,
            Date: date,
            Direction: direction,
            Merchant: merchant,
            ProposedCategoryId: categoryId,
            Confidence: Math.Round(confidence, 2));
    }

    private static ParsedPhrase? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, QuickAddJsonContext.Default.ParsedPhrase);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
        {
            return "MXN";
        }

        return currency.ToUpperInvariant();
    }

    /// <summary>The strongly-typed structured-output shape the model fills in (schema generated from this type).</summary>
    internal sealed record ParsedPhrase(
        [property: JsonPropertyName("amount")] decimal Amount,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("direction")] string? Direction,
        [property: JsonPropertyName("merchant")] string? Merchant,
        [property: JsonPropertyName("relativeDate")] string? RelativeDate);
}

[JsonSerializable(typeof(LlmNaturalLanguageTransactionParser.ParsedPhrase))]
internal sealed partial class QuickAddJsonContext : JsonSerializerContext;
