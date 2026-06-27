using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Abstractions;
using TheLedger.Application.Channels;
using TheLedger.Domain.Channels;
using TheLedger.Domain.Consent;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Channels;

/// <summary>
/// Records and revokes a user's WhatsApp opt-in (feature #50, ADR-0010). Opt-in writes both a
/// <see cref="WhatsAppContact"/> (phone → user mapping) and a <see cref="ConsentType.WhatsAppChannel"/>
/// <see cref="ConsentRecord"/>; inbound capture requires both. Everything is tenant-scoped through the
/// global query filter and stamped/audited by the interceptor, so a number can only ever be linked to a
/// user inside the caller's own household.
/// </summary>
public sealed class WhatsAppOptInService(LedgerDbContext db, ITenantContext tenant) : IWhatsAppOptInService
{
    private const string ConsentVersion = "whatsapp-v1";

    public async Task<WhatsAppOptInDto> OptInAsync(WhatsAppOptInRequest request, CancellationToken ct)
    {
        if (tenant.UserId is not { } userId || tenant.TenantId is not { } tenantId)
        {
            throw new InvalidOperationException("A resolved user is required to opt in to WhatsApp.");
        }

        var phone = NormalizePhone(request.PhoneNumber);

        // PhoneNumber is globally unique (a WhatsApp number maps to exactly one household in v1) and inbound
        // resolution looks the sender up by phone alone. Check across ALL tenants: a number already owned in
        // another tenant must be rejected, not silently re-pointed, so the by-phone lookup stays deterministic
        // (and one household can't hijack another's number).
        var existing = await db.WhatsAppContacts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.PhoneNumber == phone, ct);
        if (existing is not null && existing.TenantId != tenantId)
        {
            throw new InvalidOperationException(
                "This WhatsApp number is already linked to an account in another household and cannot be re-used.");
        }

        var contact = existing;
        if (contact is null)
        {
            contact = new WhatsAppContact
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                PhoneNumber = phone,
                DefaultAccountId = request.DefaultAccountId,
            };
            db.WhatsAppContacts.Add(contact);
        }
        else
        {
            contact.UserId = userId;
            contact.DefaultAccountId = request.DefaultAccountId;
        }

        var hasConsent = await db.Consents.AnyAsync(
            c => c.UserId == userId && c.Type == ConsentType.WhatsAppChannel, ct);
        if (!hasConsent)
        {
            db.Consents.Add(new ConsentRecord
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                Type = ConsentType.WhatsAppChannel,
                Version = ConsentVersion,
                GrantedAt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
        return new WhatsAppOptInDto(contact.Id, contact.PhoneNumber, contact.UserId, contact.DefaultAccountId, OptedIn: true);
    }

    public async Task<IReadOnlyList<WhatsAppOptInDto>> ListAsync(CancellationToken ct)
    {
        var contacts = await db.WhatsAppContacts.OrderBy(c => c.PhoneNumber).ToListAsync(ct);
        var optedInUserIds = await db.Consents
            .Where(c => c.Type == ConsentType.WhatsAppChannel)
            .Select(c => c.UserId)
            .ToListAsync(ct);
        var optedIn = optedInUserIds.ToHashSet();

        return contacts
            .Select(c => new WhatsAppOptInDto(c.Id, c.PhoneNumber, c.UserId, c.DefaultAccountId, optedIn.Contains(c.UserId)))
            .ToList();
    }

    public async Task RevokeAsync(Guid contactId, CancellationToken ct)
    {
        var contact = await db.WhatsAppContacts.FirstOrDefaultAsync(c => c.Id == contactId, ct);
        if (contact is null)
        {
            return;
        }

        db.WhatsAppContacts.Remove(contact);

        // Drop the WhatsApp consent for that user only if no other number remains mapped to them.
        var otherNumbers = await db.WhatsAppContacts
            .AnyAsync(c => c.UserId == contact.UserId && c.Id != contact.Id, ct);
        if (!otherNumbers)
        {
            var consents = await db.Consents
                .Where(c => c.UserId == contact.UserId && c.Type == ConsentType.WhatsAppChannel)
                .ToListAsync(ct);
            db.Consents.RemoveRange(consents);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Reduce to E.164 digits (no '+'), matching how Meta delivers the inbound <c>from</c> field.</summary>
    private static string NormalizePhone(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return digits;
    }
}
