using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Abstractions;
using TheLedger.Application.Foundations.Households;
using TheLedger.Domain.Identity;
using TheLedger.Domain.Tenants;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Services;

public sealed class HouseholdService(LedgerDbContext db, ITenantContext tenant) : IHouseholdService
{
    public async Task<HouseholdDto> ProvisionAsync(ProvisionHouseholdRequest request, CancellationToken ct)
    {
        var household = new Tenant
        {
            Id = Guid.CreateVersion7(),
            Name = request.HouseholdName
        };
        db.Tenants.Add(household);

        var owner = new User
        {
            Id = Guid.CreateVersion7(),
            TenantId = household.Id,
            Email = request.OwnerEmail,
            DisplayName = request.OwnerDisplayName,
            ExternalAuthId = request.OwnerExternalAuthId,
            Role = UserRole.Owner
        };
        db.Users.Add(owner);

        await db.SaveChangesAsync(ct);
        return new HouseholdDto(household.Id, household.Name, household.Plan, household.CreatedAt);
    }

    public async Task<HouseholdDto?> GetCurrentAsync(CancellationToken ct)
    {
        if (tenant.TenantId is not { } tid)
        {
            return null;
        }

        var household = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tid, ct);
        return household is null ? null : new HouseholdDto(household.Id, household.Name, household.Plan, household.CreatedAt);
    }

    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(CancellationToken ct) =>
        await db.Users
            .OrderBy(u => u.CreatedAt)
            .Select(u => new MemberDto(u.Id, u.Email, u.DisplayName, u.Role.ToString()))
            .ToListAsync(ct);

    public async Task<MemberDto> InviteMemberAsync(InviteMemberRequest request, CancellationToken ct)
    {
        var role = Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var parsed) ? parsed : UserRole.Member;

        var member = new User
        {
            Id = Guid.CreateVersion7(),
            // TenantId is stamped by AuditAndTenantInterceptor from the resolved tenant context.
            Email = request.Email,
            Role = role,
            ExternalAuthId = $"pending:{request.Email}"
        };
        db.Users.Add(member);

        await db.SaveChangesAsync(ct);
        return new MemberDto(member.Id, member.Email, member.DisplayName, member.Role.ToString());
    }
}
