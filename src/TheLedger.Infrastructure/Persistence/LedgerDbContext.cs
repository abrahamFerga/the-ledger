using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Abstractions;
using TheLedger.Application.Ledger;
using TheLedger.Domain.Accounts;
using TheLedger.Domain.Alerts;
using TheLedger.Domain.Auditing;
using TheLedger.Domain.Budgeting;
using TheLedger.Domain.Categories;
using TheLedger.Domain.Channels;
using TheLedger.Domain.Consent;
using TheLedger.Domain.Identity;
using TheLedger.Domain.Ledger;
using TheLedger.Domain.Outbox;
using TheLedger.Domain.Receipts;
using TheLedger.Domain.Statements;
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
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Statement> Statements => Set<Statement>();
    public DbSet<StatementFile> StatementFiles => Set<StatementFile>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategorizationRule> CategorizationRules => Set<CategorizationRule>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<RecurringSeries> RecurringSeries => Set<RecurringSeries>();
    public DbSet<WhatsAppContact> WhatsAppContacts => Set<WhatsAppContact>();

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

        b.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Institution).HasMaxLength(200);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.MaskedNumber).HasMaxLength(40);
            e.Property(x => x.CurrentBalance).HasPrecision(19, 4);
            e.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        b.Entity<Statement>(e =>
        {
            e.ToTable("statements");
            e.HasKey(x => x.Id);
            e.Property(x => x.Period).HasMaxLength(50);
            e.HasIndex(x => new { x.TenantId, x.AccountId });
            e.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        b.Entity<StatementFile>(e =>
        {
            e.ToTable("statement_files");
            e.HasKey(x => x.Id);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.HasIndex(x => x.StatementId);
            e.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        b.Entity<Receipt>(e =>
        {
            e.ToTable("receipts");
            e.HasKey(x => x.Id);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.Merchant).HasMaxLength(200);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Total).HasPrecision(19, 4);
            e.Property(x => x.Tax).HasPrecision(19, 4);
            e.HasIndex(x => new { x.TenantId, x.AccountId });
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        b.Entity<Transaction>(e =>
        {
            e.ToTable("transactions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Description).HasMaxLength(500).IsRequired();
            e.Property(x => x.Amount).HasPrecision(19, 4);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.HasIndex(x => new { x.TenantId, x.AccountId, x.Date });
            e.HasIndex(x => new { x.TenantId, x.IsConfirmed });
            e.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        b.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Name });
            // System categories (TenantId empty) are visible to every tenant alongside its own.
            e.HasQueryFilter(x => x.TenantId == CurrentTenantId || x.TenantId == Guid.Empty);
            e.HasData(SystemCategories.All.Select(c => new Category
            {
                Id = c.Id,
                TenantId = Guid.Empty,
                Name = c.Name,
                Kind = c.Kind,
                IsSystem = true,
            }));
        });

        b.Entity<CategorizationRule>(e =>
        {
            e.ToTable("categorization_rules");
            e.HasKey(x => x.Id);
            e.Property(x => x.MatchPattern).HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Priority });
            e.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        b.Entity<Budget>(e =>
        {
            e.ToTable("budgets");
            e.HasKey(x => x.Id);
            e.Property(x => x.TargetAmount).HasPrecision(19, 4);
            e.HasIndex(x => new { x.TenantId, x.CategoryId, x.PeriodMonth }).IsUnique();
            e.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        b.Entity<Goal>(e =>
        {
            e.ToTable("goals");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.TargetAmount).HasPrecision(19, 4);
            e.Property(x => x.CurrentAmount).HasPrecision(19, 4);
            e.HasIndex(x => x.TenantId);
            e.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        b.Entity<Alert>(e =>
        {
            e.ToTable("alerts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Message).HasMaxLength(500).IsRequired();
            e.Property(x => x.DedupeKey).HasMaxLength(300).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        b.Entity<RecurringSeries>(e =>
        {
            e.ToTable("recurring_series");
            e.HasKey(x => x.Id);
            e.Property(x => x.Merchant).HasMaxLength(100).IsRequired();
            e.Property(x => x.ExpectedAmount).HasPrecision(19, 4);
            e.HasIndex(x => new { x.TenantId, x.Merchant }).IsUnique();
            e.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });

        b.Entity<WhatsAppContact>(e =>
        {
            e.ToTable("whatsapp_contacts");
            e.HasKey(x => x.Id);
            e.Property(x => x.PhoneNumber).HasMaxLength(32).IsRequired();
            // PhoneNumber is GLOBALLY unique (not per-tenant): inbound resolution looks a sender up by phone
            // before any tenant is resolved, so one number must map to exactly one contact. OptInAsync
            // rejects a number already owned in another tenant, keeping the by-phone lookup deterministic.
            e.HasIndex(x => x.PhoneNumber).IsUnique();
            e.HasQueryFilter(x => x.TenantId == CurrentTenantId);
        });
    }
}
