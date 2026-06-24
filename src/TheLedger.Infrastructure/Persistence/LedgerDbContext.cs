using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Abstractions;
using TheLedger.Domain.Auditing;
using TheLedger.Domain.Consent;
using TheLedger.Domain.Identity;
using TheLedger.Domain.Outbox;
using TheLedger.Domain.Tenants;

namespace TheLedger.Infrastructure.Persistence;

/// <summary>
/// The single operational DbContext. Tenant-owned entities carry a global query filter on the
/// resolved tenant id, so cross-tenant reads are impossible without an explicit
/// <c>IgnoreQueryFilters()</c> (operator tooling only). Foundations slice: Tenants, Users,
/// Consents, audit, outbox. Later epics add accounts/transactions/budgets to this same context.
/// </summary>
public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options, ITenantContext tenant)
    : DbContext(options)
{
    private Guid CurrentTenantId => tenant.TenantId ?? Guid.Empty;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ConsentRecord> Consents => Set<ConsentRecord>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Plan).HasMaxLength(50);
        });

        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.ExternalAuthId).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            e.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        b.Entity<ConsentRecord>(e =>
        {
            e.ToTable("consent_records");
            e.HasKey(x => x.Id);
            e.Property(x => x.Version).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.UserId, x.Type });
            e.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        b.Entity<AuditEntry>(e =>
        {
            e.ToTable("audit_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasMaxLength(50);
            e.Property(x => x.EntityType).HasMaxLength(200);
            e.Property(x => x.EntityId).HasMaxLength(100);
            e.HasIndex(x => new { x.TenantId, x.Timestamp });
            // No query filter: audit is an operator-only cross-cutting log.
        });

        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(200);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });
    }
}
