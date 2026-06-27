using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using TheLedger.Application.Channels;

namespace TheLedger.Infrastructure.Connectors.WhatsApp;

/// <summary>
/// The WhatsApp connector's single DI entry point (feature #50, ADR-0010) — the pluggable-connector
/// "one AddXxxConnector() per connector" contract, called from Program.cs alongside <c>AddAcsEmail</c>.
/// Binds + validates <see cref="WhatsAppOptions"/> at startup, then selects the live Meta sender/media
/// downloader when real credentials are configured, or the deterministic fakes otherwise (so dev/CI run
/// with no Meta dependency). The verify-token + HMAC path works in both modes.
/// </summary>
public static class WhatsAppConnectorExtensions
{
    public static IServiceCollection AddWhatsAppConnector(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // Fail-closed startup validation (env-aware): the in-source dev secrets are usable in Development
        // only — outside Development a deploy that omits real WhatsApp:AppSecret / WhatsApp:VerifyToken
        // fails to start rather than silently accepting forged webhooks (see WhatsAppOptions.Validate).
        var isDevelopment = environment.IsDevelopment();
        services.AddOptions<WhatsAppOptions>()
            .Bind(configuration.GetSection(WhatsAppOptions.SectionName))
            .Validate(o => o.Validate(isDevelopment) is null,
                "WhatsApp options are invalid. Real WhatsApp:AppSecret and WhatsApp:VerifyToken are required outside Development.")
            .ValidateOnStart();

        // The edge handler (verify-token + HMAC + envelope parse + media + dispatch) — the API depends
        // only on IWhatsAppWebhookHandler, so Meta's HTTP/JSON specifics stay inside this connector.
        services.AddScoped<IWhatsAppWebhookHandler, WhatsAppWebhookHandler>();

        var options = configuration.GetSection(WhatsAppOptions.SectionName).Get<WhatsAppOptions>()
                      ?? new WhatsAppOptions();

        if (options.HasCredentials)
        {
            // Live Meta Cloud API: outbound send + media download over a resilient named client.
            services.AddHttpClient<IWhatsAppSender, MetaWhatsAppSender>()
                .AddStandardResilienceHandler();
            services.AddHttpClient<IWhatsAppMediaDownloader, MetaWhatsAppMediaDownloader>()
                .AddStandardResilienceHandler();
        }
        else
        {
            // Dev/CI: deterministic fakes, no Meta credentials needed. Singleton so the recorded
            // outbound messages (FakeWhatsAppSender.Sent) are observable across a request/worker scope.
            services.AddSingleton<FakeWhatsAppSender>();
            services.AddSingleton<IWhatsAppSender>(sp => sp.GetRequiredService<FakeWhatsAppSender>());
            services.AddSingleton<IWhatsAppMediaDownloader, FakeWhatsAppMediaDownloader>();
        }

        return services;
    }
}
