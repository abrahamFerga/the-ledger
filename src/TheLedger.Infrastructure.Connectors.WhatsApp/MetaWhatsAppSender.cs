using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TheLedger.Application.Channels;

namespace TheLedger.Infrastructure.Connectors.WhatsApp;

/// <summary>
/// Outbound sender over the Meta WhatsApp Business Cloud API (feature #50, ADR-0010). Registered only when
/// real credentials are configured; otherwise <see cref="FakeWhatsAppSender"/> stays in place. The HTTP
/// call goes through a named <c>HttpClient</c> with the standard Polly resilience handler (resilience
/// guardrail). Provider specifics live here so the rest of the system sees only <see cref="IWhatsAppSender"/>.
/// </summary>
public sealed class MetaWhatsAppSender(
    HttpClient http,
    IOptions<WhatsAppOptions> options,
    ILogger<MetaWhatsAppSender> logger) : IWhatsAppSender
{
    private readonly WhatsAppOptions _options = options.Value;

    public string Name => "whatsapp";

    public async Task SendAsync(WhatsAppMessage message, CancellationToken ct)
    {
        var url = $"{_options.GraphApiBaseUrl.TrimEnd('/')}/{_options.PhoneNumberId}/messages";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                to = message.To,
                type = "text",
                text = new { body = message.Body },
            }),
        };
        request.Headers.Authorization = new("Bearer", _options.AccessToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        logger.LogInformation("Sent WhatsApp message to {To}", message.To);
    }
}
