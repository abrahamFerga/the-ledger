using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TheLedger.Application.Abstractions;
using TheLedger.Application.Alerts;
using TheLedger.Application.Budgeting;
using TheLedger.Application.Channels;
using TheLedger.Application.Foundations.DataSubject;
using TheLedger.Application.Foundations.Households;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Ingestion.Extraction;
using TheLedger.Application.Ingestion.QuickAdd;
using TheLedger.Application.Ingestion.Receipts;
using TheLedger.Application.Insights;
using TheLedger.Application.Ledger;
using TheLedger.Application.Notifications;
using TheLedger.Application.Storage;
using TheLedger.Infrastructure.Categorization;
using TheLedger.Infrastructure.Channels;
using TheLedger.Infrastructure.Ingestion;
using TheLedger.Infrastructure.Notifications;
using TheLedger.Infrastructure.Storage;
using TheLedger.Infrastructure.Parsing;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Receipts;
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

        // Receipt/ticket OCR capture (feature #49, ADR-0009). The deterministic fake extractor is the
        // offline/CI default; the Azure Document Intelligence prebuilt-receipt adapter swaps in behind
        // IReceiptExtractor when configured (mirrors the email/blob/LLM dev fallbacks). OCR runs in the
        // worker off the receipt.parse outbox; normalization reuses the existing ICategorizer.
        services.AddScoped<IReceiptExtractor, FakeReceiptExtractor>();
        services.AddScoped<ReceiptNormalizationAgent>();
        services.AddScoped<ReceiptParseHandler>();
        services.AddScoped<IReceiptIngestionService, ReceiptIngestionService>();

        // Ledger + categorization (features #13, #18). CompositeCategorizer runs rules first, then the
        // LLM (ADR-0004) when an IChatClient is configured; otherwise it stays rules-only.
        services.AddScoped<ICategorizer, CompositeCategorizer>();
        services.AddScoped<ILedgerService, LedgerService>();

        // NL quick-add parser (feature #51, ADR-0011). Deterministic clock pinned to America/Mexico_City for
        // relative dates ("ayer"/"antier"/"el lunes"); BCL TimeProvider injected so tests are deterministic.
        // The composite uses the LLM-forward parser only when an IChatClient is wired AND the user opted in to
        // LLM consent; otherwise it falls back to the deterministic fake. The returned draft is never persisted.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<INaturalLanguageTransactionParser, CompositeNaturalLanguageTransactionParser>();

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

        // WhatsApp inbound capture (feature #50, ADR-0010). The processor dedupes on the message id,
        // resolves the sender to an opted-in user, and stages text via the NL parser / image via receipt
        // OCR. Dedupe is backed by the same Redis the idempotency middleware uses; the IWhatsAppSender +
        // IWhatsAppMediaDownloader come from the connector project's AddWhatsAppConnector().
        services.AddScoped<IWhatsAppDedupeStore, RedisWhatsAppDedupeStore>();
        services.AddScoped<IWhatsAppInboundProcessor, WhatsAppInboundProcessor>();
        services.AddScoped<IWhatsAppOptInService, WhatsAppOptInService>();

        // Default file store (feature #35): statement bytes in the DB; replaced by Azure Blob when configured.
        services.AddScoped<IFileStore, DbFileStore>();
        return services;
    }
}
