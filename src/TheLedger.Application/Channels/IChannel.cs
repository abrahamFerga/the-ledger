namespace TheLedger.Application.Channels;

/// <summary>
/// A messaging channel the system can speak through (the pluggable-connector "channel" contract;
/// feature #50, ADR-0010). Mirrors how <c>IEmailSender</c> abstracts the email connector: the rest of
/// the system depends only on this contract and the domain types, never on a provider SDK, so the
/// underlying provider (Meta WhatsApp Cloud API today; ACS Advanced Messaging or Twilio later) is
/// swappable without touching handlers. Channel-specific HTTP/SDK detail stays inside the connector
/// project.
/// </summary>
public interface IChannel
{
    /// <summary>Stable kebab-case channel id, e.g. <c>whatsapp</c>. Matches the connector folder name.</summary>
    string Name { get; }
}
