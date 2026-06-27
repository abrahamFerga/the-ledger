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
        var key = $"wa:msg:{messageId}";
        if (await cache.GetStringAsync(key, ct) is not null)
        {
            return false;
        }

        await cache.SetStringAsync(key, "1", Ttl, ct);
        return true;
    }
}
