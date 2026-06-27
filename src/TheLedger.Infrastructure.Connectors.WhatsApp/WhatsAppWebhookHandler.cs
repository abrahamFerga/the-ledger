using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TheLedger.Application.Channels;

namespace TheLedger.Infrastructure.Connectors.WhatsApp;

/// <summary>
/// The connector's edge handler for inbound WhatsApp (feature #50, ADR-0010). It owns every Meta-specific
/// concern — the verify-token challenge, the <c>X-Hub-Signature-256</c> HMAC over the raw body, parsing
/// Meta's envelope, and downloading image media — then hands system-neutral
/// <see cref="WhatsAppInboundMessage"/>s to <see cref="IWhatsAppInboundProcessor"/>. The API endpoint
/// never sees any of this detail.
/// </summary>
public sealed class WhatsAppWebhookHandler(
    IOptions<WhatsAppOptions> options,
    IWhatsAppInboundProcessor processor,
    IWhatsAppMediaDownloader mediaDownloader,
    ILogger<WhatsAppWebhookHandler> logger) : IWhatsAppWebhookHandler
{
    private readonly WhatsAppOptions _options = options.Value;

    public string? Verify(string? mode, string? verifyToken, string? challenge)
    {
        if (mode == "subscribe"
            && !string.IsNullOrEmpty(verifyToken)
            && string.Equals(verifyToken, _options.VerifyToken, StringComparison.Ordinal))
        {
            return challenge;
        }

        logger.LogWarning("WhatsApp webhook verify rejected (mode {Mode})", mode);
        return null;
    }

    public async Task<WhatsAppWebhookResult> HandleAsync(byte[] rawBody, string? signatureHeader, CancellationToken ct)
    {
        // HMAC of the RAW body against the app secret BEFORE any processing — reject unverified.
        if (!WhatsAppSignatureVerifier.IsValid(rawBody, signatureHeader, _options.AppSecret))
        {
            logger.LogWarning("WhatsApp webhook POST rejected: invalid X-Hub-Signature-256");
            return new WhatsAppWebhookResult(SignatureValid: false, Processed: 0);
        }

        var messages = WhatsAppWebhookParser.Parse(rawBody);
        if (messages.Count == 0)
        {
            // A status callback or an unparseable body that still passed HMAC — nothing to capture.
            return new WhatsAppWebhookResult(SignatureValid: true, Processed: 0);
        }

        // For image messages, fetch the media bytes (real: Meta Graph; dev: a deterministic placeholder).
        var mediaByMessage = await DownloadMediaAsync(rawBody, ct);

        var processed = 0;
        foreach (var message in messages)
        {
            var hydrated = message;
            if (message.Kind == WhatsAppInboundKind.Image
                && mediaByMessage.TryGetValue(message.MessageId, out var media))
            {
                hydrated = message with { Media = media.Content, MediaContentType = media.ContentType };
            }

            await processor.ProcessAsync(hydrated, ct);
            processed++;
        }

        return new WhatsAppWebhookResult(SignatureValid: true, Processed: processed);
    }

    private async Task<Dictionary<string, WhatsAppMedia>> DownloadMediaAsync(byte[] rawBody, CancellationToken ct)
    {
        var result = new Dictionary<string, WhatsAppMedia>();
        foreach (var (messageId, mediaId) in WhatsAppWebhookParser.ImageMediaIds(rawBody))
        {
            var media = await mediaDownloader.DownloadAsync(mediaId, ct);
            if (media is not null)
            {
                result[messageId] = media;
            }
        }

        return result;
    }
}
