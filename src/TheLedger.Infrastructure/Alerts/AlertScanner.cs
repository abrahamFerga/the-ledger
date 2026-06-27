using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Abstractions;
using TheLedger.Application.Alerts;
using TheLedger.Application.Channels;
using TheLedger.Domain.Accounts;
using TheLedger.Domain.Alerts;
using TheLedger.Domain.Consent;
using TheLedger.Domain.Ledger;
using TheLedger.Domain.Outbox;
using TheLedger.Infrastructure.Channels;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Alerts;

public sealed class AlertScanner(LedgerDbContext db, ITenantContext tenant) : IAlertScanner
{
    private const decimal LowBalanceThreshold = 100m;
    private static readonly string[] FeeKeywords = ["COMISION", "COMISIÓN", "FEE", "CARGO POR", "ANUALIDAD"];

    // Phone numbers of users in this tenant who opted in to WhatsApp alerts (resolved once per scan).
    private List<string> _whatsAppRecipients = [];

    public async Task<int> ScanAsync(CancellationToken ct)
    {
        var transactions = await db.Transactions.Where(t => t.IsConfirmed).OrderBy(t => t.Date).ToListAsync(ct);
        var existing = await db.Alerts.Where(a => a.Status != AlertStatus.Dismissed).Select(a => a.DedupeKey).ToListAsync(ct);
        var keys = new HashSet<string>(existing);
        _whatsAppRecipients = await ResolveWhatsAppRecipientsAsync(ct);

        var raised = 0;
        raised += AddDuplicates(transactions, keys);
        raised += AddFees(transactions, keys);
        raised += await AddLowBalanceAsync(keys, ct);
        await UpsertRecurringAsync(transactions, ct);
        raised += await AddBillsDueAsync(keys, ct);

        await db.SaveChangesAsync(ct);
        return raised;
    }

    private int AddDuplicates(List<Transaction> transactions, HashSet<string> keys)
    {
        var raised = 0;
        foreach (var group in transactions
                     .GroupBy(t => (t.AccountId, t.Date, t.Amount, t.Description))
                     .Where(g => g.Count() > 1))
        {
            var k = group.Key;
            var key = $"dup:{k.AccountId}:{k.Date:yyyyMMdd}:{k.Amount}:{k.Description}";
            raised += Raise(keys, AlertType.DuplicateCharge, key,
                $"Possible duplicate charge: {k.Description} {k.Amount} on {k.Date:yyyy-MM-dd} ({group.Count()}x)",
                group.First().Id, k.AccountId);
        }

        return raised;
    }

    private int AddFees(List<Transaction> transactions, HashSet<string> keys)
    {
        var raised = 0;
        foreach (var t in transactions.Where(t =>
                     t.Direction == TransactionDirection.Debit &&
                     FeeKeywords.Any(k => t.Description.ToUpperInvariant().Contains(k))))
        {
            raised += Raise(keys, AlertType.NewFee, $"fee:{t.Id}",
                $"Fee detected: {t.Description} {t.Amount}", t.Id, t.AccountId);
        }

        return raised;
    }

    private async Task<int> AddLowBalanceAsync(HashSet<string> keys, CancellationToken ct)
    {
        var accounts = await db.Accounts.ToListAsync(ct);
        var raised = 0;
        foreach (var a in accounts.Where(a => a.Type != AccountType.Card && a.CurrentBalance < LowBalanceThreshold))
        {
            raised += Raise(keys, AlertType.LowBalance, $"low:{a.Id}",
                $"Low balance on {a.Name}: {a.CurrentBalance} {a.Currency}", null, a.Id);
        }

        return raised;
    }

    private async Task UpsertRecurringAsync(List<Transaction> transactions, CancellationToken ct)
    {
        var existing = await db.RecurringSeries.ToListAsync(ct);

        foreach (var group in transactions.GroupBy(t => Normalize(t.Description)).Where(g => g.Count() >= 3))
        {
            var amounts = group.Select(t => t.Amount).ToList();
            var average = amounts.Average();
            if (average <= 0 || amounts.Any(a => Math.Abs(a - average) > average * 0.15m))
            {
                continue; // not consistent enough to be recurring
            }

            var lastSeen = group.Max(t => t.Date);
            var merchant = group.Key;
            var series = existing.FirstOrDefault(s => s.Merchant == merchant);
            if (series is null)
            {
                series = new RecurringSeries
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = tenant.TenantId ?? Guid.Empty,
                    Merchant = merchant,
                };
                db.RecurringSeries.Add(series);
                existing.Add(series);
            }

            series.Cadence = RecurringCadence.Monthly;
            series.ExpectedAmount = average;
            series.LastSeen = lastSeen;
            series.NextExpectedDate = lastSeen.AddMonths(1);
            series.OccurrenceCount = group.Count();
        }
    }

    private async Task<int> AddBillsDueAsync(HashSet<string> keys, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(7);
        var series = await db.RecurringSeries.ToListAsync(ct);

        var raised = 0;
        foreach (var s in series.Where(s => s.NextExpectedDate >= today && s.NextExpectedDate <= horizon))
        {
            raised += Raise(keys, AlertType.BillDue, $"bill:{s.Id}:{s.NextExpectedDate:yyyyMM}",
                $"Upcoming bill: {s.Merchant} ~{s.ExpectedAmount} on {s.NextExpectedDate:yyyy-MM-dd}");
        }

        return raised;
    }

    private int Raise(HashSet<string> keys, AlertType type, string dedupeKey, string message,
        Guid? transactionId = null, Guid? accountId = null)
    {
        if (!keys.Add(dedupeKey))
        {
            return 0;
        }

        db.Alerts.Add(new Alert
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.TenantId ?? Guid.Empty,
            Type = type,
            TransactionId = transactionId,
            AccountId = accountId,
            Message = message,
            DedupeKey = dedupeKey,
        });

        db.Outbox.Add(new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.TenantId,
            Type = "alert.email",
            Payload = dedupeKey,
            Status = OutboxStatus.Pending,
        });

        // Parallel WhatsApp channel (feature #50): fan the same bill/anomaly/export-ready alert out to
        // every user in this tenant who opted in to WhatsApp, routed through the outbox like email.
        foreach (var phone in _whatsAppRecipients)
        {
            db.Outbox.Add(WhatsAppOutbox.Send(new WhatsAppMessage(phone, $"🔔 the-ledger: {message}"), tenant.TenantId));
        }

        return 1;
    }

    /// <summary>
    /// The phone numbers of users in the current tenant who hold a <see cref="ConsentType.WhatsAppChannel"/>
    /// consent and have a mapped <see cref="Domain.Channels.WhatsAppContact"/>. Empty when no one opted in,
    /// so alerts simply stay email-only — the WhatsApp channel is purely additive and opt-in gated.
    /// </summary>
    private async Task<List<string>> ResolveWhatsAppRecipientsAsync(CancellationToken ct)
    {
        var optedInUserIds = await db.Consents
            .Where(c => c.Type == ConsentType.WhatsAppChannel)
            .Select(c => c.UserId)
            .ToListAsync(ct);

        if (optedInUserIds.Count == 0)
        {
            return [];
        }

        return await db.WhatsAppContacts
            .Where(c => optedInUserIds.Contains(c.UserId))
            .Select(c => c.PhoneNumber)
            .ToListAsync(ct);
    }

    private static string Normalize(string description)
    {
        var upper = description.Trim().ToUpperInvariant();
        return upper.Length > 20 ? upper[..20] : upper;
    }
}
