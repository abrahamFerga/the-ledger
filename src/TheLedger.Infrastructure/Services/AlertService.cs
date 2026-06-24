using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Alerts;
using TheLedger.Domain.Alerts;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Services;

public sealed class AlertService(LedgerDbContext db) : IAlertService
{
    public async Task<IReadOnlyList<AlertDto>> ListAlertsAsync(bool includeResolved, CancellationToken ct)
    {
        var query = db.Alerts.AsQueryable();
        if (!includeResolved)
        {
            query = query.Where(a => a.Status != AlertStatus.Dismissed);
        }

        // Order client-side: SQLite (used in tests) cannot ORDER BY a DateTimeOffset.
        var alerts = await query.ToListAsync(ct);
        return alerts
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AlertDto(a.Id, a.Type.ToString(), a.TransactionId, a.AccountId, a.Message, a.Status.ToString(), a.CreatedAt))
            .ToList();
    }

    public async Task<bool> DismissAsync(Guid alertId, CancellationToken ct)
    {
        var alert = await db.Alerts.FirstOrDefaultAsync(a => a.Id == alertId, ct);
        if (alert is null)
        {
            return false;
        }

        alert.Status = AlertStatus.Dismissed;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<RecurringSeriesDto>> ListRecurringAsync(CancellationToken ct)
    {
        var series = await db.RecurringSeries.OrderBy(s => s.Merchant).ToListAsync(ct);
        return series
            .Select(s => new RecurringSeriesDto(s.Id, s.Merchant, s.Cadence.ToString(), s.ExpectedAmount, s.NextExpectedDate, s.OccurrenceCount))
            .ToList();
    }
}
