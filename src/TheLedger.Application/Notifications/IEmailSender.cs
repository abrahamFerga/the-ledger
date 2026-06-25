namespace TheLedger.Application.Notifications;

/// <summary>An email to deliver. Also the JSON shape of an <c>email</c> outbox message payload.</summary>
public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>Sends transactional emails (alerts, invitations, export-ready). Implemented by a connector.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
