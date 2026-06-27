namespace TheLedger.Application.Channels;

/// <summary>Request to opt the current user in to WhatsApp capture & alerts for a phone number.</summary>
public sealed record WhatsAppOptInRequest(string PhoneNumber, Guid? DefaultAccountId = null);

/// <summary>The stored WhatsApp opt-in for a user (phone → user mapping + consent), tenant-scoped.</summary>
public sealed record WhatsAppOptInDto(Guid Id, string PhoneNumber, Guid UserId, Guid? DefaultAccountId, bool OptedIn);

/// <summary>
/// Manages a user's WhatsApp opt-in (feature #50): records a <c>WhatsAppChannel</c> consent and maps the
/// phone number to the user so inbound captures resolve to them. Tenant-scoped; revoking removes both the
/// mapping and the consent so the number can no longer reach tenant data.
/// </summary>
public interface IWhatsAppOptInService
{
    Task<WhatsAppOptInDto> OptInAsync(WhatsAppOptInRequest request, CancellationToken ct);
    Task<IReadOnlyList<WhatsAppOptInDto>> ListAsync(CancellationToken ct);
    Task RevokeAsync(Guid contactId, CancellationToken ct);
}
