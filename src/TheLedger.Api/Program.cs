using Microsoft.EntityFrameworkCore;
using TheLedger.Api.Endpoints;
using TheLedger.Api.Idempotency;
using TheLedger.Api.Setup;
using TheLedger.Api.Tenancy;
using TheLedger.Infrastructure;
using TheLedger.Infrastructure.Azure;
using TheLedger.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Observability, health, resilience (OpenTelemetry via ServiceDefaults).
builder.AddServiceDefaults();

// Redis distributed cache (idempotency replay + rate-limit state). Connection injected by Aspire.
builder.AddRedisDistributedCache("cache");

// Persistence + multi-tenancy + audit + Foundations services. Connection injected by Aspire as "ledgerdb".
var connectionString = builder.Configuration.GetConnectionString("ledgerdb")
    ?? "Host=localhost;Port=5432;Database=ledgerdb;Username=postgres;Password=postgres";
builder.Services.AddInfrastructure(connectionString);

// Azure OpenAI IChatClient for LLM categorization (registered only when configured; ADR-0004).
builder.Services.AddAzureAiCategorization(builder.Configuration);

// Email connector (Azure Communication Services) — registered only when configured (feature #34).
builder.Services.AddAcsEmail(builder.Configuration);

// Azure Blob statement storage — registered only when configured; defaults to the DB store (feature #35).
builder.Services.AddAzureBlobStorage(builder.Configuration);

// AuthN (OIDC / Dev) + RBAC policies.
builder.AddLedgerAuth();

// API conventions.
builder.Services.AddLedgerRateLimiting();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                  ?? ["http://localhost:5173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

// RFC 7807 Problem Details for unhandled exceptions and error status codes.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantResolverMiddleware>();
app.UseAuthorization();
app.UseMiddleware<IdempotencyMiddleware>();

// Health/liveness from ServiceDefaults.
app.MapDefaultEndpoints();
app.MapFoundations();
app.MapIngestion();
app.MapLedger();
app.MapBudgets();
app.MapGoals();
app.MapInsights();
app.MapAlerts();

// Apply EF Core migrations on startup in development when a database is reachable.
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Skipped database migration (no database reachable yet).");
    }
}

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
