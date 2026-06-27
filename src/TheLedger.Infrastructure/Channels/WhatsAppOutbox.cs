using System.Text.Json;
using TheLedger.Application.Channels;
using TheLedger.Domain.Outbox;

namespace TheLedger.Infrastructure.Channels;

/// <summary>
/// Builds and reads <c>whatsapp.send</c> outbox messages (feature #50). Outbound WhatsApp — help replies
/// and bill/anomaly/export-ready alerts — always goes through the transactional outbox, never inline from
/// a handler (ADR-0010), exactly like the email path. The payload is the JSON of a
/// <see cref="WhatsAppMessage"/>.
/// </summary>
public static class WhatsAppOutbox
{
    public const string OutboxType = "whatsapp.send";

    public static OutboxMessage Send(WhatsAppMessage message, Guid? tenantId) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = tenantId,
        Type = OutboxType,
        Payload = JsonSerializer.Serialize(message),
        Status = OutboxStatus.Pending,
    };

    public static WhatsAppMessage? Read(string payload) =>
        JsonSerializer.Deserialize<WhatsAppMessage>(payload);
}
