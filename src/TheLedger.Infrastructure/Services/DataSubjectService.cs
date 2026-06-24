using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Foundations.DataSubject;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Services;

/// <summary>
/// LFPDPPP/GDPR data-subject service. Operator-invoked, so it explicitly targets a tenant id
/// and bypasses the request tenant filter via <c>IgnoreQueryFilters</c>.
/// </summary>
public sealed class DataSubjectService(LedgerDbContext db) : IDataSubjectService
{
    public async Task<DataExportDto> ExportAsync(Guid tenantId, CancellationToken ct)
    {
        var household = await db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.Id, t.Name, t.Plan, t.CreatedAt })
            .FirstOrDefaultAsync(ct);

        var users = await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId)
            .Select(u => new { u.Id, u.Email, u.DisplayName, Role = u.Role.ToString(), u.CreatedAt })
            .ToListAsync(ct);

        var consents = await db.Consents
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId)
            .Select(c => new { Type = c.Type.ToString(), c.Version, c.GrantedAt })
            .ToListAsync(ct);

        return new DataExportDto(tenantId, DateTimeOffset.UtcNow, "application/json",
            new { household, users, consents });
    }

    public async Task DeleteAsync(Guid tenantId, CancellationToken ct)
    {
        await db.Consents.IgnoreQueryFilters().Where(c => c.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await db.Users.IgnoreQueryFilters().Where(u => u.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await db.Outbox.IgnoreQueryFilters().Where(o => o.TenantId == tenantId).ExecuteDeleteAsync(ct);
        await db.Tenants.Where(t => t.Id == tenantId).ExecuteDeleteAsync(ct);
    }
}
