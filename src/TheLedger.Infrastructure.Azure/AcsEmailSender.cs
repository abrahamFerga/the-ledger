using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TheLedger.Application.Notifications;
using EmailMessage = TheLedger.Application.Notifications.EmailMessage;

namespace TheLedger.Infrastructure.Azure;

/// <summary>Email connector over Azure Communication Services.</summary>
public sealed class AcsEmailSender(EmailClient client, string senderAddress) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        await client.SendAsync(
            WaitUntil.Completed,
            senderAddress,
            message.To,
            message.Subject,
            htmlContent: message.Body,
            cancellationToken: ct);
    }
}

public static class AcsEmailExtensions
{
    /// <summary>
    /// Registers the ACS email connector when <c>Email:Acs:ConnectionString</c> + <c>Email:Acs:Sender</c>
    /// are configured; otherwise leaves the default (no-op) sender in place.
    /// </summary>
    public static IServiceCollection AddAcsEmail(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["Email:Acs:ConnectionString"];
        var sender = configuration["Email:Acs:Sender"];
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(sender))
        {
            return services;
        }

        var client = new EmailClient(connectionString);
        services.AddSingleton<IEmailSender>(new AcsEmailSender(client, sender));
        return services;
    }
}
