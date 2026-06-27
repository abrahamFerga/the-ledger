using System.Text.Json;
using System.Text.Json.Serialization;
using TheLedger.Application.Channels;

namespace TheLedger.Infrastructure.Connectors.WhatsApp;

// Minimal projection of Meta's WhatsApp Business Cloud webhook envelope — only the fields we capture.
// Kept inside the connector so Meta's JSON shape never leaks past IChannel/IWhatsAppInboundProcessor.

internal sealed record WhatsAppWebhookPayload(
    [property: JsonPropertyName("object")] string? Object,
    [property: JsonPropertyName("entry")] IReadOnlyList<WhatsAppEntry>? Entry);

internal sealed record WhatsAppEntry(
    [property: JsonPropertyName("changes")] IReadOnlyList<WhatsAppChange>? Changes);

internal sealed record WhatsAppChange(
    [property: JsonPropertyName("value")] WhatsAppChangeValue? Value);

internal sealed record WhatsAppChangeValue(
    [property: JsonPropertyName("messages")] IReadOnlyList<WhatsAppRawMessage>? Messages);

internal sealed record WhatsAppRawMessage(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("from")] string? From,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("text")] WhatsAppTextBody? Text,
    [property: JsonPropertyName("image")] WhatsAppMediaBody? Image);

internal sealed record WhatsAppTextBody(
    [property: JsonPropertyName("body")] string? Body);

internal sealed record WhatsAppMediaBody(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("mime_type")] string? MimeType,
    [property: JsonPropertyName("caption")] string? Caption);

/// <summary>
/// Parses Meta's webhook envelope into the system-neutral <see cref="WhatsAppInboundMessage"/> list. Pure
/// and side-effect-free over the JSON (media bytes are fetched separately by the connector in real mode).
/// </summary>
internal static class WhatsAppWebhookParser
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<WhatsAppInboundMessage> Parse(ReadOnlySpan<byte> body)
    {
        WhatsAppWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(body, Json);
        }
        catch (JsonException)
        {
            return [];
        }

        if (payload?.Entry is null)
        {
            return [];
        }

        var result = new List<WhatsAppInboundMessage>();
        foreach (var change in payload.Entry
                     .SelectMany(e => e.Changes ?? [])
                     .Select(c => c.Value)
                     .OfType<WhatsAppChangeValue>())
        {
            foreach (var msg in change.Messages ?? [])
            {
                if (string.IsNullOrWhiteSpace(msg.Id) || string.IsNullOrWhiteSpace(msg.From))
                {
                    continue;
                }

                result.Add(Normalize(msg));
            }
        }

        return result;
    }

    private static WhatsAppInboundMessage Normalize(WhatsAppRawMessage msg)
    {
        return msg.Type switch
        {
            "text" => new WhatsAppInboundMessage(
                msg.Id!, msg.From!, WhatsAppInboundKind.Text, msg.Text?.Body, Media: null, MediaContentType: null),
            "image" => new WhatsAppInboundMessage(
                msg.Id!, msg.From!, WhatsAppInboundKind.Image, msg.Image?.Caption, Media: null,
                MediaContentType: msg.Image?.MimeType ?? "image/jpeg"),
            _ => new WhatsAppInboundMessage(
                msg.Id!, msg.From!, WhatsAppInboundKind.Unsupported, Text: null, Media: null, MediaContentType: null),
        };
    }

    /// <summary>The Meta media id of an image message, used to fetch the bytes in real mode.</summary>
    public static IReadOnlyList<(string MessageId, string MediaId)> ImageMediaIds(ReadOnlySpan<byte> body)
    {
        WhatsAppWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(body, Json);
        }
        catch (JsonException)
        {
            return [];
        }

        if (payload?.Entry is null)
        {
            return [];
        }

        return payload.Entry
            .SelectMany(e => e.Changes ?? [])
            .Select(c => c.Value)
            .OfType<WhatsAppChangeValue>()
            .SelectMany(v => v.Messages ?? [])
            .Where(m => m.Type == "image" && !string.IsNullOrWhiteSpace(m.Id) && !string.IsNullOrWhiteSpace(m.Image?.Id))
            .Select(m => (m.Id!, m.Image!.Id!))
            .ToList();
    }
}
