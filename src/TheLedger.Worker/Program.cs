using TheLedger.Infrastructure;
using TheLedger.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("ledgerdb")
    ?? "Host=localhost;Port=5432;Database=ledgerdb;Username=postgres;Password=postgres";
builder.Services.AddInfrastructure(connectionString);

builder.Services.AddHostedService<OutboxDispatcher>();

var host = builder.Build();
host.Run();
