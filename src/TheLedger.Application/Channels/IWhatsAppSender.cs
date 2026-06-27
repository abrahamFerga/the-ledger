using TheLedger.Domain.Common;

namespace TheLedger.Application.Channels;

/// <summary>
/// An outbound WhatsApp text message. Also the JSON shape of an <c>whatsapp.send</c> outbox payload, so
/// outbound sends route through the transactional outbox exactly like email — never inline from a
/// handler (ADR-0010). The recipient number is E.164 digits (no leading '+') and the body is PII.
/// </summary>
public sealed record WhatsAppMessage([property: Pii] string To, [property: Pii] string Body);

/// <summary>
/// Sends WhatsApp messages. The production implementation talks to the Meta WhatsApp Business Cloud API
/// (<c>MetaWhatsAppSender</c>); a deterministic <c>FakeWhatsAppSender</c> backs dev/CI and tests so the
/// system runs without real Meta credentials, mirroring the email no-op/ACS split. Kept behind this
/// interface so the provider is swappable per ADR-0010. Implemented by the WhatsApp connector.
/// </summary>
public interface IWhatsAppSender : IChannel
{
    Task SendAsync(WhatsAppMessage message, CancellationToken ct);
}
