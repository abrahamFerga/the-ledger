using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace TheLedger.Api.Idempotency;

/// <summary>
/// Honors the <c>Idempotency-Key</c> header on non-GET writes: a key seen within the 24h window
/// (tracked in Redis) is rejected as a duplicate. Full response-replay is a follow-up; this stops
/// double-submits. Writes without a key are allowed but logged.
/// </summary>
public sealed class IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
{
    private static readonly HashSet<string> WriteMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    public async Task InvokeAsync(HttpContext context, IDistributedCache cache)
    {
        if (!WriteMethods.Contains(context.Request.Method))
        {
            await next(context);
            return;
        }

        var key = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            logger.LogWarning("Write {Method} {Path} without an Idempotency-Key", context.Request.Method, context.Request.Path);
            await next(context);
            return;
        }

        var cacheKey = $"idem:{key}";
        if (await cache.GetStringAsync(cacheKey) is not null)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Duplicate request",
                Detail = "This Idempotency-Key has already been processed."
            });
            return;
        }

        await cache.SetStringAsync(cacheKey, "1", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        });

        await next(context);
    }
}
