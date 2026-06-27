namespace TheLedger.Application.Channels;

/// <summary>The result of handling an inbound webhook POST, so the endpoint can shape its HTTP response.</summary>
public sealed record WhatsAppWebhookResult(bool SignatureValid, int Processed);

/// <summary>
/// Verifies and dispatches an inbound WhatsApp webhook POST (feature #50, ADR-0010). Implemented by the
/// connector: it validates the HMAC of the raw body, parses Meta's envelope, downloads any image media,
/// and hands each normalized message to <see cref="IWhatsAppInboundProcessor"/>. The API endpoint depends
/// only on this contract — Meta's JSON/HTTP specifics never leak into the API.
/// </summary>
public interface IWhatsAppWebhookHandler
{
    /// <summary>Answers the GET subscription challenge: returns the challenge when the verify token matches.</summary>
    string? Verify(string? mode, string? verifyToken, string? challenge);

    /// <summary>
    /// Verifies the <c>X-Hub-Signature-256</c> HMAC of <paramref name="rawBody"/>, then parses + dispatches
    /// every inbound message. Returns <see cref="WhatsAppWebhookResult.SignatureValid"/> = false (and does
    /// no processing) when the signature is missing or tampered, so the endpoint can reject with 403.
    /// </summary>
    Task<WhatsAppWebhookResult> HandleAsync(byte[] rawBody, string? signatureHeader, CancellationToken ct);
}
