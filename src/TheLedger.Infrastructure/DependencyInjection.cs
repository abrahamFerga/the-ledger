using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheLedger.Application.Abstractions;
using TheLedger.Application.Foundations.DataSubject;
using TheLedger.Application.Foundations.Households;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Ingestion.Extraction;
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
        return services;
    }
}
