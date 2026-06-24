namespace TheLedger.Application.Alerts;

public sealed record AlertDto(
    Guid Id, string Type, Guid? TransactionId, Guid? AccountId, string Message, string Status, DateTimeOffset CreatedAt);

public sealed record RecurringSeriesDto(
    Guid Id, string Merchant, string Cadence, decimal ExpectedAmount, DateOnly NextExpectedDate, int OccurrenceCount);

public interface IAlertService
{
    Task<IReadOnlyList<AlertDto>> ListAlertsAsync(bool includeResolved, CancellationToken ct);
    Task<bool> DismissAsync(Guid alertId, CancellationToken ct);
    Task<IReadOnlyList<RecurringSeriesDto>> ListRecurringAsync(CancellationToken ct);
}

/// <summary>
/// Detects recurring series and raises anomaly/bill alerts for the current tenant. Idempotent via
/// a per-alert dedupe key; new alerts are enqueued to the outbox for email delivery.
/// </summary>
public interface IAlertScanner
{
    Task<int> ScanAsync(CancellationToken ct);
}
