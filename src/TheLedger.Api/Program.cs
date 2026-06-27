using Microsoft.EntityFrameworkCore;
using TheLedger.Api.Endpoints;
using TheLedger.Api.Idempotency;
using TheLedger.Api.Setup;
using TheLedger.Api.Tenancy;
using TheLedger.Infrastructure;
using TheLedger.Infrastructure.Azure;
using TheLedger.Infrastructure.Connectors.WhatsApp;
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

// Azure Document Intelligence receipt OCR — registered only when configured; defaults to the fake
// extractor so CI needs no Azure dependency (feature #49, ADR-0009).
builder.Services.AddAzureDocumentIntelligence(builder.Configuration);

// WhatsApp connector (feature #50, ADR-0010): inbound webhook handler + outbound sender. Validates its
// options at startup; uses the deterministic fake sender/media downloader unless real Meta credentials
// are configured, so the webhook (verify-token + HMAC) is exercisable in dev/CI without Meta.
builder.Services.AddWhatsAppConnector(builder.Configuration, builder.Environment);

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
app.MapWhatsApp();

// Apply EF Core migrations on startup. A failure here is fatal: fail loudly (and let Aspire restart
// the resource) rather than silently serving 500s against a missing schema (#45).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
