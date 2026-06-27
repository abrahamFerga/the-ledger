using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Channels;
using TheLedger.Application.Notifications;
using TheLedger.Domain.Outbox;
using TheLedger.Infrastructure.Channels;
using TheLedger.Infrastructure.Parsing;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Receipts;
using TheLedger.Infrastructure.Services;

namespace TheLedger.Worker;

/// <summary>
/// Drains the transactional outbox. Bootstrap implementation marks messages dispatched; later epics
/// route by <see cref="OutboxMessage.Type"/> to the email/LLM handlers. On Azure this is replaced by
/// a KEDA queue-scaled trigger (ADR-0005); the polling loop keeps it runnable locally and in tests.
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
                var parseHandler = scope.ServiceProvider.GetRequiredService<StatementParseHandler>();
                var receiptHandler = scope.ServiceProvider.GetRequiredService<ReceiptParseHandler>();
                var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                var whatsAppSender = scope.ServiceProvider.GetRequiredService<IWhatsAppSender>();

                var pending = await db.Outbox
                    .Where(m => m.Status == OutboxStatus.Pending)
                    .OrderBy(m => m.CreatedAt)
                    .Take(20)
                    .ToListAsync(stoppingToken);

                foreach (var message in pending)
                {
                    try
                    {
                        if (message.Type == "statement.parse" && Guid.TryParse(message.Payload, out var statementId))
                        {
                            await parseHandler.HandleAsync(statementId, stoppingToken);
                        }
                        else if (message.Type == ReceiptIngestionService.OutboxType && Guid.TryParse(message.Payload, out var receiptId))
                        {
                            await receiptHandler.HandleAsync(receiptId, stoppingToken);
                        }
                        else if (message.Type == "email")
                        {
                            var email = JsonSerializer.Deserialize<EmailMessage>(message.Payload);
                            if (email is not null)
                            {
                                await emailSender.SendAsync(email, stoppingToken);
                            }
                        }
                        else if (message.Type == WhatsAppOutbox.OutboxType)
                        {
                            // Outbound WhatsApp (help replies + opt-in alerts) — sent here, never inline
                            // from a handler (ADR-0010). The fake sender backs dev/CI; the live Meta
                            // sender is selected by AddWhatsAppConnector when credentials are configured.
                            var whatsApp = WhatsAppOutbox.Read(message.Payload);
                            if (whatsApp is not null)
                            {
                                await whatsAppSender.SendAsync(whatsApp, stoppingToken);
                            }
                        }

                        message.Status = OutboxStatus.Done;
                        message.ProcessedAt = DateTimeOffset.UtcNow;
                        logger.LogInformation("Dispatched outbox message {Id} ({Type})", message.Id, message.Type);
                    }
                    catch (Exception ex)
                    {
                        message.Status = OutboxStatus.Failed;
                        message.Attempts++;
                        message.Error = ex.Message;
                        logger.LogError(ex, "Outbox message {Id} ({Type}) failed", message.Id, message.Type);
                    }
                }

                if (pending.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatch iteration failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
