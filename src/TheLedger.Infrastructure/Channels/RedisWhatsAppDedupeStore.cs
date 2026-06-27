using Microsoft.Extensions.Caching.Distributed;
using TheLedger.Application.Channels;

namespace TheLedger.Infrastructure.Channels;

/// <summary>
/// Dedupes inbound WhatsApp messages on the WhatsApp message id using the same Redis the idempotency
/// middleware uses (feature #50). The first delivery of a <c>wamid.*</c> writes a key with a 24h TTL and
/// returns <c>true</c>; any retry inside that window sees the key and returns <c>false</c>, so a retried
/// webhook can't stage a duplicate transaction.
/// </summary>
public sealed class RedisWhatsAppDedupeStore(IDistributedCache cache) : IWhatsAppDedupeStore
{
    private static readonly DistributedCacheEntryOptions Ttl = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
    };

    public async Task<bool> TryMarkProcessedAsync(string messageId, CancellationToken ct)
    {
        var key = Key(messageId);
        if (await cache.GetStringAsync(key, ct) is not null)
        {
            return false;
        }

        await cache.SetStringAsync(key, "1", Ttl, ct);
        return true;
    }

    public Task RemoveAsync(string messageId, CancellationToken ct) =>
        // Compensating action: drop the claim so Meta's retry of this wamid can re-process after a
        // staging failure (the mark is a pre-claim, not proof the capture committed).
        cache.RemoveAsync(Key(messageId), ct);

    private static string Key(string messageId) => $"wa:msg:{messageId}";
}
