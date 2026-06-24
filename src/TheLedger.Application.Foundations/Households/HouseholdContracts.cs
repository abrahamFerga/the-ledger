namespace TheLedger.Application.Foundations.Households;

public sealed record ProvisionHouseholdRequest(
    string HouseholdName,
    string OwnerEmail,
    string OwnerExternalAuthId,
    string? OwnerDisplayName);

public sealed record HouseholdDto(Guid Id, string Name, string Plan, DateTimeOffset CreatedAt);

public sealed record MemberDto(Guid Id, string Email, string? DisplayName, string Role);

public sealed record InviteMemberRequest(string Email, string Role);

/// <summary>Household provisioning + membership. The signup flow calls Provision; owners invite members.</summary>
public interface IHouseholdService
{
    Task<HouseholdDto> ProvisionAsync(ProvisionHouseholdRequest request, CancellationToken ct);
    Task<HouseholdDto?> GetCurrentAsync(CancellationToken ct);
    Task<IReadOnlyList<MemberDto>> ListMembersAsync(CancellationToken ct);
    Task<MemberDto> InviteMemberAsync(InviteMemberRequest request, CancellationToken ct);
}
