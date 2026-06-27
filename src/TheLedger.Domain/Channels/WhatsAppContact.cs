using TheLedger.Domain.Common;

namespace TheLedger.Domain.Channels;

/// <summary>
/// Maps a WhatsApp phone number (E.164, no leading '+', as Meta delivers the <c>from</c> field) to an
/// opted-in <see cref="Domain.Identity.User"/> within a tenant (feature #50, ADR-0010). Inbound capture
/// resolves the sender against this table; a number with no row — or whose user has not granted
/// <see cref="Domain.Consent.ConsentType.WhatsAppChannel"/> — gets a generic help reply and never any
/// tenant data, so a stranger's message can't leak across households. The phone number is PII.
/// </summary>
public sealed class WhatsAppContact : Entity, ITenantOwned, IAuditable
{
    public Guid TenantId { get; set; }

    /// <summary>The user this WhatsApp number belongs to. Inbound captures stage under this user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Sender phone number as Meta delivers it (E.164 digits, no '+'). PII.</summary>
    [Pii]
    public required string PhoneNumber { get; set; }

    /// <summary>Account inbound captures land on when the message carries no account hint.</summary>
    public Guid? DefaultAccountId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
