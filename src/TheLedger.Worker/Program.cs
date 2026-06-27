using TheLedger.Infrastructure;
using TheLedger.Infrastructure.Azure;
using TheLedger.Infrastructure.Connectors.WhatsApp;
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

// WhatsApp connector (feature #50, ADR-0010): the worker dispatches outbound whatsapp.send outbox
// messages through IWhatsAppSender. The fake sender backs dev/CI; the live Meta sender is selected
// only when credentials are configured.
builder.Services.AddWhatsAppConnector(builder.Configuration, builder.Environment);

builder.Services.AddHostedService<OutboxDispatcher>();

var host = builder.Build();
host.Run();
