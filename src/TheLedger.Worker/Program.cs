using TheLedger.Infrastructure;
using TheLedger.Infrastructure.Azure;
using TheLedger.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("ledgerdb")
    ?? "Host=localhost;Port=5432;Database=ledgerdb;Username=postgres;Password=postgres";
builder.Services.AddInfrastructure(connectionString);

// OCR runs in the worker (ADR-0009). Azure OpenAI for merchant normalization + categorization, the
// Azure Blob image store, and the Azure Document Intelligence receipt extractor are each registered
// only when configured; otherwise the deterministic dev/CI fallbacks stay in place.
builder.Services.AddAzureAiCategorization(builder.Configuration);
builder.Services.AddAzureBlobStorage(builder.Configuration);
builder.Services.AddAzureDocumentIntelligence(builder.Configuration);

builder.Services.AddHostedService<OutboxDispatcher>();

var host = builder.Build();
host.Run();
