using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TheLedger.Application.Channels;

namespace TheLedger.Infrastructure.Connectors.WhatsApp;

/// <summary>
/// Deterministic WhatsApp sender for dev/CI and tests (feature #50, ADR-0010). It never calls Meta, so
/// the system runs without real credentials — exactly how <c>NoOpEmailSender</c> backs the email channel.
/// It records every message in <see cref="Sent"/> so tests can assert what the outbox dispatched.
/// </summary>
public sealed class FakeWhatsAppSender(ILogger<FakeWhatsAppSender> logger) : IWhatsAppSender
{
    private readonly ConcurrentQueue<WhatsAppMessage> _sent = new();

    public string Name => "whatsapp";

    /// <summary>Messages this sender has "sent", in order — for dev inspection and test assertions.</summary>
    public IReadOnlyCollection<WhatsAppMessage> Sent => _sent.ToArray();

    public Task SendAsync(WhatsAppMessage message, CancellationToken ct)
    {
        _sent.Enqueue(message);
        // Body is PII, so we log only its length, never the content.
        logger.LogInformation("Fake WhatsApp send to {To} ({Length} chars)", message.To, message.Body.Length);
        return Task.CompletedTask;
    }
}
