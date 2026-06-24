using Microsoft.EntityFrameworkCore;
using TheLedger.Api.Endpoints;
using TheLedger.Api.Idempotency;
using TheLedger.Api.Setup;
using TheLedger.Api.Tenancy;
using TheLedger.Infrastructure;
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

// Dev convenience: create the schema if a database is reachable. Replaced by EF migrations (follow-up).
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Skipped database EnsureCreated (no database reachable yet).");
    }
}

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
