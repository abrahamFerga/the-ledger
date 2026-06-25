using Microsoft.Extensions.Logging;
using TheLedger.Application.Notifications;

namespace TheLedger.Infrastructure.Notifications;

/// <summary>Default email sender: logs instead of sending. Replaced by a real connector when configured.</summary>
public sealed class NoOpEmailSender(ILogger<NoOpEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        logger.LogInformation("Email not delivered (no email connector configured): to={To} subject={Subject}",
            message.To, message.Subject);
        return Task.CompletedTask;
    }
}
