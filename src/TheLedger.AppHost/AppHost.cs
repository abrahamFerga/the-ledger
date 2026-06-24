var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL (operational + audit + outbox) with a persistent local volume.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();
var ledgerdb = postgres.AddDatabase("ledgerdb");

// Redis (idempotency replay + rate-limit windows + cache).
var cache = builder.AddRedis("cache");

// API service.
builder.AddProject<Projects.TheLedger_Api>("api")
    .WithReference(ledgerdb)
    .WithReference(cache)
    .WaitFor(ledgerdb)
    .WaitFor(cache);

// Background worker (outbox dispatch + scheduled jobs).
builder.AddProject<Projects.TheLedger_Worker>("worker")
    .WithReference(ledgerdb)
    .WaitFor(ledgerdb);

builder.Build().Run();
