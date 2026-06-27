using TheLedger.Domain.Common;

namespace TheLedger.Application.Channels;

/// <summary>The kind of inbound WhatsApp payload, after the connector parses Meta's envelope.</summary>
public enum WhatsAppInboundKind
{
    /// <summary>A free-text/dictated phrase, e.g. "gasté 200 en el Oxxo" → NL quick-add parser.</summary>
    Text,

    /// <summary>An image (a receipt/ticket photo) → receipt OCR ingestion.</summary>
    Image,

    /// <summary>Anything else (audio, location, sticker…). Acknowledged with a help reply, not processed.</summary>
    Unsupported
}

/// <summary>
/// One inbound WhatsApp message, normalized by the connector out of Meta's webhook envelope so the rest
/// of the system never sees Meta's JSON shape (feature #50, ADR-0010). For an image, the connector has
/// already downloaded the media bytes via the Cloud API (or, in dev, the bytes are the fake-extractor
/// text). The sender phone and any text/caption are PII.
/// </summary>
public sealed record WhatsAppInboundMessage(
    string MessageId,
    [property: Pii] string From,
    WhatsAppInboundKind Kind,
    [property: Pii] string? Text,
    byte[]? Media,
    string? MediaContentType);

/// <summary>The outcome of processing one inbound message, so the connector can shape its reply/telemetry.</summary>
public enum WhatsAppInboundOutcome
{
    /// <summary>Sender was unknown / not opted in: a generic help reply was queued, no tenant data touched.</summary>
    UnknownSender,

    /// <summary>A duplicate WhatsApp message id we already processed: ignored.</summary>
    Duplicate,

    /// <summary>A staged transaction was created (text parsed or image queued for OCR).</summary>
    Staged,

    /// <summary>The message type isn't a capture (audio/location/etc.): a help reply was queued.</summary>
    Unsupported
}

/// <summary>
/// Processes a normalized inbound WhatsApp message: dedupes on the message id, resolves the sender phone
/// to an opted-in user (or replies with help and leaks nothing), and routes text → the NL quick-add
/// parser / image → receipt OCR, producing a <b>staged</b> transaction in the existing review-and-confirm
/// queue. The connector hands every parsed message here; all tenant-scoped work lives behind this contract.
/// </summary>
public interface IWhatsAppInboundProcessor
{
    Task<WhatsAppInboundOutcome> ProcessAsync(WhatsAppInboundMessage message, CancellationToken ct);
}
