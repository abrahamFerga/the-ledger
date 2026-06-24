using Microsoft.EntityFrameworkCore;
using TheLedger.Domain.Outbox;
using TheLedger.Infrastructure.Parsing;
using TheLedger.Infrastructure.Persistence;

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

                        // TODO(epics Alerts/AI): route email + llm-categorize types here too.
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
