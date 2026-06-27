using System.Net;
using System.Net.Http.Json;
using System.Text;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Xunit;

namespace TheLedger.AppHost.Tests;

/// <summary>
/// Contract tests for the WhatsApp inbound webhook (feature #50, ADR-0010) against the real Aspire host
/// (Postgres + Redis). Proves the webhook is wired, JWT-anonymous (called by Meta), gated by the
/// verify-token on GET, and rejects an unsigned/bad-HMAC POST with 403 before any processing. The dev
/// default verify token ("dev-verify-token") applies since no real Meta config is set. Requires Docker.
/// </summary>
public class WhatsAppWebhookEndpointTests
{
    private const string WebhookPath = "/api/v1/connectors/whatsapp/webhook";

    [Fact]
    public async Task Get_verify_echoes_the_challenge_when_the_token_matches()
    {
        using var http = await StartHostAsync();

        var response = await http.GetAsync(
            $"{WebhookPath}?hub.mode=subscribe&hub.verify_token=dev-verify-token&hub.challenge=12345");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("12345", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Get_verify_is_forbidden_when_the_token_is_wrong()
    {
        using var http = await StartHostAsync();

        var response = await http.GetAsync(
            $"{WebhookPath}?hub.mode=subscribe&hub.verify_token=wrong-token&hub.challenge=12345");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_without_a_valid_signature_is_rejected_403()
    {
        using var http = await StartHostAsync();

        // A well-formed body but no / wrong X-Hub-Signature-256 → rejected before any processing.
        using var content = new StringContent(
            """{"object":"whatsapp_business_account","entry":[]}""", Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", "sha256=deadbeef");

        var response = await http.PostAsync(WebhookPath, content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_an_oversize_body_is_rejected_413_before_any_processing()
    {
        using var http = await StartHostAsync();

        // > 256 KB body: the buffered-read cap rejects it with 413 before HMAC/processing, so a hostile
        // caller can't use the webhook to exhaust memory.
        var huge = new string('a', 300 * 1024);
        using var content = new StringContent(
            $$"""{"object":"whatsapp_business_account","pad":"{{huge}}"}""", Encoding.UTF8, "application/json");
        content.Headers.Add("X-Hub-Signature-256", "sha256=deadbeef");

        var response = await http.PostAsync(WebhookPath, content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private static async Task<HttpClient> StartHostAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.TheLedger_AppHost>();
        var app = await appHost.BuildAsync();
        await app.StartAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token);

        var baseAddress = app.GetEndpoint("api", "https");
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        return new HttpClient(handler) { BaseAddress = baseAddress };
    }
}
