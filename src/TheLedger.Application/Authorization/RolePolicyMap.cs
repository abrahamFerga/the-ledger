namespace TheLedger.Application.Authorization;

/// <summary>Role names (match <see cref="Domain.Identity.UserRole"/>).</summary>
public static class Roles
{
    public const string Owner = "Owner";
    public const string Member = "Member";
    public const string Viewer = "Viewer";
    public const string Operator = "Operator";
}

/// <summary>
/// Default role → policy assignments. Config can override this map without code changes;
/// the API registers one ASP.NET Core authorization policy per entry, satisfied when the
/// caller's role claim is in the policy's allowed-role set.
/// </summary>
public static class RolePolicyMap
{
    public static readonly IReadOnlyDictionary<string, string[]> Default = new Dictionary<string, string[]>
    {
        [Policies.HouseholdsManage] = [Roles.Owner],
        [Policies.MembersInvite] = [Roles.Owner],
        [Policies.MembersManage] = [Roles.Owner],
        [Policies.BillingManage] = [Roles.Owner],
        [Policies.DataExport] = [Roles.Owner],
        [Policies.DataDelete] = [Roles.Owner],

        [Policies.AccountsManage] = [Roles.Owner],
        [Policies.AccountsView] = [Roles.Owner, Roles.Member, Roles.Viewer],
        [Policies.StatementsUpload] = [Roles.Owner, Roles.Member],
        [Policies.TransactionsView] = [Roles.Owner, Roles.Member, Roles.Viewer],
        [Policies.TransactionsEdit] = [Roles.Owner, Roles.Member],
        [Policies.BudgetsView] = [Roles.Owner, Roles.Member, Roles.Viewer],
        [Policies.BudgetsEdit] = [Roles.Owner, Roles.Member],
        [Policies.GoalsView] = [Roles.Owner, Roles.Member, Roles.Viewer],
        [Policies.GoalsEdit] = [Roles.Owner, Roles.Member],
        [Policies.InsightsView] = [Roles.Owner, Roles.Member, Roles.Viewer],
        [Policies.AlertsView] = [Roles.Owner, Roles.Member, Roles.Viewer],

        [Policies.TenantsProvision] = [Roles.Operator],
        [Policies.InstanceMonitor] = [Roles.Operator],
        [Policies.DataSubjectExecute] = [Roles.Operator],
    };
}
