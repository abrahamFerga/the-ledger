using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TheLedger.Application.Abstractions;
using TheLedger.Application.Channels;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Ingestion.QuickAdd;
using TheLedger.Application.Ingestion.Receipts;
using TheLedger.Domain.Channels;
using TheLedger.Domain.Consent;
using TheLedger.Domain.Ledger;
using TheLedger.Domain.Outbox;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Services;

namespace TheLedger.Infrastructure.Channels;

/// <summary>
/// Processes one normalized inbound WhatsApp message (feature #50, ADR-0010). Order matters:
/// <list type="number">
/// <item>dedupe on the WhatsApp message id (Meta retries) before touching anything;</item>
/// <item>resolve the sender phone → an opted-in <see cref="WhatsAppContact"/> + a current
/// <see cref="ConsentType.WhatsAppChannel"/> consent. An unknown / not-opted-in sender gets a generic
/// help reply via the outbox and <b>no tenant data</b> — the cross-tenant leak guard;</item>
/// <item>resolve the tenant context to that user's tenant, then route text → the NL quick-add parser
/// (a <b>staged</b> transaction persisted here) / image → receipt OCR ingestion (staged by the worker).</item>
/// </list>
/// Reads at the edge ignore the tenant query filter because no tenant is resolved yet; once resolved,
/// every write flows through the audit/tenant interceptor exactly like a request.
/// </summary>
public sealed class WhatsAppInboundProcessor(
    LedgerDbContext db,
    ITenantContext tenant,
    IWhatsAppDedupeStore dedupe,
    INaturalLanguageTransactionParser parser,
    IReceiptIngestionService receipts,
    ILogger<WhatsAppInboundProcessor> logger) : IWhatsAppInboundProcessor
{
    private const string HelpReply =
        "Hola 👋 Soy tu asistente de the-ledger. Para capturar gastos por WhatsApp, primero vincula este " +
        "número a tu cuenta y acepta el aviso de privacidad desde la app (Integraciones → WhatsApp).";

    public async Task<WhatsAppInboundOutcome> ProcessAsync(WhatsAppInboundMessage message, CancellationToken ct)
    {
        // 1) Pre-claim the wamid so two concurrent deliveries of the same message can't both stage. This is
        // a CLAIM, not proof the capture committed: if the staging work below throws we must release it
        // (see the catch) so Meta's retry re-processes — otherwise a mid-pipeline failure would silently
        // and permanently drop the capture.
        if (!await dedupe.TryMarkProcessedAsync(message.MessageId, ct))
        {
            logger.LogInformation("Ignoring duplicate WhatsApp message {MessageId}", message.MessageId);
            return WhatsAppInboundOutcome.Duplicate;
        }

        try
        {
            return await ProcessClaimedAsync(message, ct);
        }
        catch (Exception ex)
        {
            // Compensate: release the dedupe claim so the bounded Meta retry of this wamid re-processes
            // and stages, then rethrow so the boundary surfaces it as a transient failure.
            logger.LogWarning(ex,
                "WhatsApp message {MessageId} failed during staging; releasing dedupe claim for retry",
                message.MessageId);
            await dedupe.RemoveAsync(message.MessageId, ct);
            throw;
        }
    }

    private async Task<WhatsAppInboundOutcome> ProcessClaimedAsync(WhatsAppInboundMessage message, CancellationToken ct)
    {
        // 2) Resolve the sender to an opted-in user. No tenant is resolved yet, so this read crosses the
        // tenant filter deliberately; it only ever matches a number a household explicitly registered.
        var contact = await db.WhatsAppContacts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.PhoneNumber == message.From, ct);

        var optedIn = contact is not null && await db.Consents.IgnoreQueryFilters()
            .AnyAsync(c => c.UserId == contact.UserId && c.Type == ConsentType.WhatsAppChannel, ct);

        if (contact is null || !optedIn)
        {
            logger.LogInformation("WhatsApp message from unrecognized/not-opted-in sender; sending help reply only");
            QueueReply(message.From, tenantId: null);
            await db.SaveChangesAsync(ct);
            return WhatsAppInboundOutcome.UnknownSender;
        }

        // 3) Resolve the tenant context to the mapped user's tenant for the rest of this message.
        tenant.Resolve(contact.TenantId, contact.UserId, role: null);

        switch (message.Kind)
        {
            case WhatsAppInboundKind.Text when !string.IsNullOrWhiteSpace(message.Text):
                await StageFromTextAsync(contact, message.Text!, ct);
                return WhatsAppInboundOutcome.Staged;

            case WhatsAppInboundKind.Image when message.Media is { Length: > 0 }:
                await StageFromImageAsync(contact, message, ct);
                return WhatsAppInboundOutcome.Staged;

            default:
                logger.LogInformation("Unsupported WhatsApp message kind {Kind}; sending help reply", message.Kind);
                QueueReply(message.From, contact.TenantId);
                await db.SaveChangesAsync(ct);
                return WhatsAppInboundOutcome.Unsupported;
        }
    }

    /// <summary>Text → NL parser → a staged (unconfirmed) transaction in the review-and-confirm queue.</summary>
    private async Task StageFromTextAsync(WhatsAppContact contact, string text, CancellationToken ct)
    {
        var accountId = await ResolveAccountIdAsync(contact, ct);
        var draft = await parser.ParseAsync(new QuickAddRequest(text, accountId), ct);

        var transaction = new Transaction
        {
            Id = Guid.CreateVersion7(),
            TenantId = contact.TenantId,
            AccountId = accountId,
            Date = draft.Date,
            // The phrase is user text; PAN-mask before persistence on every capture path (ADR-0002).
            Description = PanMasker.Mask(draft.Merchant ?? text),
            Amount = draft.Amount,
            Currency = draft.Currency,
            Direction = draft.Direction,
            CategoryId = draft.ProposedCategoryId,
            CategorizationSource = draft.ProposedCategoryId is null
                ? CategorizationSource.None
                : CategorizationSource.Llm,
            Confidence = draft.Confidence,
            AttributedUserId = contact.UserId,
            IsConfirmed = false, // stays in the review-and-confirm queue — never auto-posts
        };
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Staged WhatsApp text capture {TransactionId} for user {UserId} (confidence {Confidence})",
            transaction.Id, contact.UserId, draft.Confidence);
    }

    /// <summary>Image → receipt OCR ingestion, which stages a transaction via the worker (#49).</summary>
    private async Task StageFromImageAsync(WhatsAppContact contact, WhatsAppInboundMessage message, CancellationToken ct)
    {
        var accountId = await ResolveAccountIdAsync(contact, ct);
        await receipts.UploadAsync(
            accountId,
            $"whatsapp-{message.MessageId}.jpg",
            message.MediaContentType ?? "image/jpeg",
            message.Media!,
            ct);

        logger.LogInformation("Queued WhatsApp image capture from {UserId} for OCR", contact.UserId);
    }

    /// <summary>The contact's default account, or the user's first account, or a fresh cash account.</summary>
    private async Task<Guid> ResolveAccountIdAsync(WhatsAppContact contact, CancellationToken ct)
    {
        if (contact.DefaultAccountId is { } pinned
            && await db.Accounts.AnyAsync(a => a.Id == pinned, ct))
        {
            return pinned;
        }

        var first = await db.Accounts.OrderBy(a => a.Name).FirstOrDefaultAsync(ct);
        if (first is not null)
        {
            return first.Id;
        }

        var cash = new Domain.Accounts.Account
        {
            Id = Guid.CreateVersion7(),
            TenantId = contact.TenantId,
            Name = "Efectivo (WhatsApp)",
            Type = Domain.Accounts.AccountType.Cash,
            Currency = "MXN",
        };
        db.Accounts.Add(cash);
        await db.SaveChangesAsync(ct);
        return cash.Id;
    }

    /// <summary>Queues a help reply through the outbox (never an inline send from this handler).</summary>
    private void QueueReply(string to, Guid? tenantId)
    {
        db.Outbox.Add(WhatsAppOutbox.Send(new WhatsAppMessage(to, HelpReply), tenantId));
    }
}
