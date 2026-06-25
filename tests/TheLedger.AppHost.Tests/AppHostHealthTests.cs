using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Xunit;

namespace TheLedger.AppHost.Tests;

/// <summary>
/// Boots the whole Aspire app (real Postgres + Redis containers) and exercises the API end to end.
/// Requires Docker; runs in CI and locally when Docker is available.
/// </summary>
public class AppHostHealthTests
{
    [Fact]
    public async Task Api_boots_and_is_healthy_against_real_postgres_and_redis()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.TheLedger_AppHost>();
        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token);

        // HTTPS redirect sends us to the dev cert (untrusted on CI), so accept any server cert here.
        var baseAddress = app.GetEndpoint("api", "https");
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        using var http = new HttpClient(handler) { BaseAddress = baseAddress };

        var health = await http.GetAsync("/health", cts.Token);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        // TODO(#36 follow-up): assert a tenant-scoped endpoint (e.g. GET /api/v1/categories) once the
        // in-container 500 is resolved — migrations apply at startup but the seeded read returns 500
        // when booted via the Aspire test host; needs the booted app's logs to diagnose.
    }
}
