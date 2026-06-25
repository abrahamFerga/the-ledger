using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TheLedger.Application.Notifications;
using TheLedger.Infrastructure.Azure;
using TheLedger.Infrastructure.Notifications;
using Xunit;

namespace TheLedger.IntegrationTests;

public class EmailTests
{
    [Fact]
    public void Email_message_round_trips_through_json()
    {
        var message = new EmailMessage("a@example.com", "Low balance", "<b>Hi</b>");
        var back = JsonSerializer.Deserialize<EmailMessage>(JsonSerializer.Serialize(message));
        Assert.Equal(message, back);
    }

    [Fact]
    public async Task NoOp_email_sender_does_not_throw()
    {
        var sender = new NoOpEmailSender(NullLogger<NoOpEmailSender>.Instance);
        await sender.SendAsync(new EmailMessage("a@example.com", "subject", "body"), default);
    }

    [Fact]
    public void Acs_email_is_registered_only_when_configured()
    {
        var unconfigured = new ServiceCollection();
        unconfigured.AddAcsEmail(new ConfigurationBuilder().Build());
        Assert.Null(unconfigured.BuildServiceProvider().GetService<IEmailSender>());

        var configured = new ServiceCollection();
        configured.AddAcsEmail(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Acs:ConnectionString"] = "endpoint=https://test.communication.azure.com/;accesskey=YWJjZGVmZ2hpamtsbW5vcA==",
                ["Email:Acs:Sender"] = "noreply@test.com",
            })
            .Build());
        Assert.NotNull(configured.BuildServiceProvider().GetService<IEmailSender>());
    }
}
