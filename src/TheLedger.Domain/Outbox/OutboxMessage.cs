using TheLedger.Domain.Common;

namespace TheLedger.Domain.Outbox;

public enum OutboxStatus
{
    Pending,
    Processing,
    Done,
    Failed
}

/// <summary>
/// Transactional outbox row. Every external side effect (email, LLM call) is written
/// here in the same transaction as the domain change, then dispatched by the worker —
/// no fire-and-forget from handlers.
/// </summary>
public sealed class OutboxMessage : Entity
{
    public Guid? TenantId { get; set; }
    public required string Type { get; set; }
    public required string Payload { get; set; }
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? Error { get; set; }
}
