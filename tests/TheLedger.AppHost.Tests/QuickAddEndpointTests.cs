using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Xunit;

namespace TheLedger.AppHost.Tests;

/// <summary>
/// Contract test for the NL quick-add endpoint (feature #51, ADR-0011) against the real Aspire host
/// (Postgres + Redis containers). Proves the route is wired into the API and gated by RBAC — an
/// unauthenticated POST is rejected, never reaching the parser. Requires Docker; runs in CI when available.
/// </summary>
public class QuickAddEndpointTests
{
    [Fact]
    public async Task Quick_add_endpoint_is_wired_and_requires_authorization()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.TheLedger_AppHost>();
        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token);

        var baseAddress = app.GetEndpoint("api", "https");
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        using var http = new HttpClient(handler) { BaseAddress = baseAddress };

        // No dev-auth headers → the route exists but RBAC (Transactions.Edit) rejects it.
        // A 404 here would mean the endpoint was never mapped; a 200 would mean it ran unauthenticated.
        var response = await http.PostAsJsonAsync(
            "/api/v1/transactions/quick-add",
            new { text = "gasté 200 en el Oxxo" },
            cts.Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
