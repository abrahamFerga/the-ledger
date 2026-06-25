using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheLedger.Application.Abstractions;
using TheLedger.Application.Alerts;
using TheLedger.Application.Budgeting;
using TheLedger.Application.Foundations.DataSubject;
using TheLedger.Application.Foundations.Households;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Ingestion.Extraction;
using TheLedger.Application.Insights;
using TheLedger.Application.Ledger;
using TheLedger.Application.Notifications;
using TheLedger.Application.Storage;
using TheLedger.Infrastructure.Categorization;
using TheLedger.Infrastructure.Notifications;
using TheLedger.Infrastructure.Storage;
using TheLedger.Infrastructure.Parsing;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Services;
using TheLedger.Infrastructure.Tenancy;

namespace TheLedger.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the operational DbContext (with the audit/tenant interceptor), the scoped tenant
    /// context, and the Foundations services. The connection string is provided by Aspire at runtime.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<AuditAndTenantInterceptor>();

        services.AddDbContext<LedgerDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditAndTenantInterceptor>());
        });

        services.AddScoped<IHouseholdService, HouseholdService>();
        services.AddScoped<IDataSubjectService, DataSubjectService>();
        services.AddScoped<IIngestionService, IngestionService>();

        // Statement parsing (feature #12). The heuristic extractor is the offline default; the
        // LLM-forward extractor (ADR-0004) swaps in behind IStatementExtractor when configured.
        services.AddScoped<IPdfTextExtractor, Utf8TextExtractor>();
        services.AddScoped<IStatementExtractor, HeuristicStatementExtractor>();
        services.AddScoped<StatementParseHandler>();

        // Ledger + categorization (features #13, #18). CompositeCategorizer runs rules first, then the
        // LLM (ADR-0004) when an IChatClient is configured; otherwise it stays rules-only.
        services.AddScoped<ICategorizer, CompositeCategorizer>();
        services.AddScoped<ILedgerService, LedgerService>();

        // Budgeting + goals (features #14, #15).
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IGoalService, GoalService>();

        // Insights + export (feature #16).
        services.AddScoped<IInsightsService, InsightsService>();

        // Alerts: recurring detection + anomalies (feature #17).
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IAlertScanner, Alerts.AlertScanner>();

        // Default email sender (feature #34); replaced by the ACS connector when configured.
        services.AddScoped<IEmailSender, NoOpEmailSender>();

        // Default file store (feature #35): statement bytes in the DB; replaced by Azure Blob when configured.
        services.AddScoped<IFileStore, DbFileStore>();
        return services;
    }
}
