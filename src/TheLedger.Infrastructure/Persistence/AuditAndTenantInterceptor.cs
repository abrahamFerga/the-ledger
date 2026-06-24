using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TheLedger.Application.Abstractions;
using TheLedger.Domain.Auditing;
using TheLedger.Domain.Common;
using TheLedger.Domain.Outbox;

namespace TheLedger.Infrastructure.Persistence;

/// <summary>
/// On every SaveChanges: stamps <see cref="ITenantOwned.TenantId"/> on new rows, maintains
/// <see cref="IAuditable"/> timestamps, and appends an <see cref="AuditEntry"/> per mutation
/// (who/what/when/tenant). The audit log itself and the outbox are exempt to avoid recursion.
/// </summary>
public sealed class AuditAndTenantInterceptor(ITenantContext tenant) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            Apply(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            Apply(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext context)
    {
        var now = DateTimeOffset.UtcNow;
        var audits = new List<AuditEntry>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditEntry or OutboxMessage)
            {
                continue;
            }

            if (entry.State == EntityState.Added
                && entry.Entity is ITenantOwned owned
                && owned.TenantId == Guid.Empty
                && tenant.TenantId is { } tid)
            {
                owned.TenantId = tid;
            }

            if (entry.Entity is IAuditable auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAt = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedAt = now;
                }
            }

            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                audits.Add(new AuditEntry
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = (entry.Entity as ITenantOwned)?.TenantId ?? tenant.TenantId,
                    UserId = tenant.UserId,
                    Action = entry.State.ToString(),
                    EntityType = entry.Entity.GetType().Name,
                    EntityId = PrimaryKeyOf(entry),
                    Timestamp = now
                });
            }
        }

        if (audits.Count > 0)
        {
            context.Set<AuditEntry>().AddRange(audits);
        }
    }

    private static string PrimaryKeyOf(EntityEntry entry)
    {
        var pk = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
        return pk?.CurrentValue?.ToString() ?? string.Empty;
    }
}
