namespace TheLedger.Application.Channels;

/// <summary>
/// Dedupes inbound WhatsApp messages on the WhatsApp message id (feature #50). Meta retries webhook
/// deliveries, so the same <c>wamid.*</c> can arrive more than once; processing it twice would stage a
/// duplicate transaction. The production implementation is backed by the same Redis the idempotency
/// middleware uses (a short TTL window); an in-memory implementation backs tests.
/// </summary>
public interface IWhatsAppDedupeStore
{
    /// <summary>
    /// Atomically records the message id as seen. Returns <c>true</c> the first time an id is seen and
    /// <c>false</c> on every subsequent delivery within the dedupe window.
    /// </summary>
    Task<bool> TryMarkProcessedAsync(string messageId, CancellationToken ct);
}
