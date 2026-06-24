namespace TheLedger.Application.Authorization;

/// <summary>
/// Authorization policy names in <c>Module.Action</c> form. Code and UI reference these
/// constants — never role strings — so roles can be remapped in config without code changes.
/// Refined from PLAN.md's RBAC model.
/// </summary>
public static class Policies
{
    // Foundations
    public const string HouseholdsManage = "Households.Manage";
    public const string MembersInvite = "Members.Invite";
    public const string MembersManage = "Members.Manage";
    public const string DataExport = "Data.Export";
    public const string DataDelete = "Data.Delete";
    public const string BillingManage = "Billing.Manage";

    // Ledger / Budgeting / Insights (declared now; enforced as those epics land)
    public const string AccountsView = "Accounts.View";
    public const string AccountsManage = "Accounts.Manage";
    public const string StatementsUpload = "Statements.Upload";
    public const string TransactionsView = "Transactions.View";
    public const string TransactionsEdit = "Transactions.Edit";
    public const string BudgetsView = "Budgets.View";
    public const string BudgetsEdit = "Budgets.Edit";
    public const string GoalsView = "Goals.View";
    public const string GoalsEdit = "Goals.Edit";
    public const string InsightsView = "Insights.View";
    public const string AlertsView = "Alerts.View";

    // Operator (platform admin)
    public const string TenantsProvision = "Tenants.Provision";
    public const string InstanceMonitor = "Instance.Monitor";
    public const string DataSubjectExecute = "DataSubject.Execute";
}
