using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TheLedger.Api.Setup;

public static class RateLimitingSetup
{
    /// <summary>Per-tenant fixed-window limiter (falls back to client IP for unauthenticated calls).</summary>
    public static IServiceCollection AddLedgerRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            {
                var partitionKey = ctx.User.FindFirst("tenant_id")?.Value
                                   ?? ctx.Connection.RemoteIpAddress?.ToString()
                                   ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });
        });
        return services;
    }
}
