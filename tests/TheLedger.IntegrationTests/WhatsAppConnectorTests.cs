using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TheLedger.Application.Channels;
using TheLedger.Domain.Accounts;
using TheLedger.Domain.Channels;
using TheLedger.Domain.Consent;
using TheLedger.Domain.Identity;
using TheLedger.Domain.Ledger;
using TheLedger.Infrastructure.Categorization;
using TheLedger.Infrastructure.Channels;
using TheLedger.Infrastructure.Connectors.WhatsApp;
using TheLedger.Infrastructure.Ingestion;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Services;
using TheLedger.Infrastructure.Storage;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.IntegrationTests;

/// <summary>
/// WhatsApp capture & alerts connector (feature #50, ADR-0010). The deterministic fakes back every
/// assertion — no Meta credentials. Covers: the verify-token challenge, HMAC valid AND tampered,
/// inbound text → a staged transaction, inbound image → a staged transaction (via the receipt path),
/// the unknown/not-opted-in sender (help reply + no tenant data), message-id dedupe, outbound enqueues
/// an outbox message, and the fake-vs-real DI selection.
/// </summary>
public class WhatsAppConnectorTests
{
    private static LedgerDbContext NewContext(SqliteConnection connection, TenantContext tenant) =>
        new(new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditAndTenantInterceptor(tenant))
            .Options, tenant);

    // ---- HMAC signature verification --------------------------------------------------------------

    [Fact]
    public void Hmac_validates_a_correctly_signed_body()
    {
        var body = Encoding.UTF8.GetBytes("""{"object":"whatsapp_business_account"}""");
        const string secret = "top-secret";
        var signature = WhatsAppSignatureVerifier.Compute(body, secret);

        Assert.True(WhatsAppSignatureVerifier.IsValid(body, signature, secret));
    }

    [Fact]
    public void Hmac_rejects_a_tampered_body()
    {
        var body = Encoding.UTF8.GetBytes("""{"object":"whatsapp_business_account"}""");
        var signature = WhatsAppSignatureVerifier.Compute(body, "top-secret");

        var tampered = Encoding.UTF8.GetBytes("""{"object":"whatsapp_business_account","evil":true}""");
        Assert.False(WhatsAppSignatureVerifier.IsValid(tampered, signature, "top-secret"));
    }

    [Fact]
    public void Hmac_rejects_a_missing_or_malformed_signature_header()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        Assert.False(WhatsAppSignatureVerifier.IsValid(body, signatureHeader: null, "s"));
        Assert.False(WhatsAppSignatureVerifier.IsValid(body, "not-a-signature", "s"));
        Assert.False(WhatsAppSignatureVerifier.IsValid(body, "sha256=deadbeef", "s"));
    }

    // ---- Verify-token challenge -------------------------------------------------------------------

    [Fact]
    public void Verify_echoes_the_challenge_only_when_the_token_matches()
    {
        var handler = NewWebhookHandler(new WhatsAppOptions { VerifyToken = "expected" }, processor: null!, new FakeWhatsAppMediaDownloader());

        Assert.Equal("CHALLENGE", handler.Verify("subscribe", "expected", "CHALLENGE"));
        Assert.Null(handler.Verify("subscribe", "wrong", "CHALLENGE"));
        Assert.Null(handler.Verify(mode: null, "expected", "CHALLENGE"));
    }

    // ---- Webhook handler: HMAC gate → parse → dispatch (end-to-end inbound) -----------------------

    [Fact]
    public async Task Webhook_handler_rejects_a_tampered_post_before_processing()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var options = new WhatsAppOptions { AppSecret = "the-secret" };
        var handler = NewWebhookHandler(options, NewProcessor(ctx, tenant), new FakeWhatsAppMediaDownloader());

        var body = Encoding.UTF8.GetBytes(TextWebhookBody("wamid.x", "5215512345678", "gasté 100 en Oxxo"));
        var badSignature = WhatsAppSignatureVerifier.Compute(body, "WRONG-secret");

        var result = await handler.HandleAsync(body, badSignature, default);

        Assert.False(result.SignatureValid);
        Assert.Equal(0, result.Processed);
        Assert.Empty(await ctx.Transactions.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Webhook_handler_processes_a_correctly_signed_text_message_into_a_staged_transaction()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var tenant = new TenantContext();
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        tenant.Resolve(tenantId, userId, "Owner");

        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();
        await SeedOptedInUserAsync(ctx, tenantId, userId, "5215512345678");

        var options = new WhatsAppOptions { AppSecret = "the-secret" };
        var handler = NewWebhookHandler(options, NewProcessor(ctx, tenant), new FakeWhatsAppMediaDownloader());

        var body = Encoding.UTF8.GetBytes(TextWebhookBody("wamid.signed", "5215512345678", "gasté 200 en el Oxxo"));
        var signature = WhatsAppSignatureVerifier.Compute(body, "the-secret");

        var result = await handler.HandleAsync(body, signature, default);

        Assert.True(result.SignatureValid);
        Assert.Equal(1, result.Processed);
        var staged = await ctx.Transactions.IgnoreQueryFilters().SingleAsync();
        Assert.False(staged.IsConfirmed);
        Assert.Equal(200m, staged.Amount);
    }

    private static string TextWebhookBody(string messageId, string from, string text) =>
        $$"""
        {
          "object": "whatsapp_business_account",
          "entry": [{
            "changes": [{
              "value": {
                "messages": [{
                  "id": "{{messageId}}",
                  "from": "{{from}}",
                  "type": "text",
                  "text": { "body": "{{text}}" }
                }]
              }
            }]
          }]
        }
        """;

    // ---- Inbound text → staged transaction --------------------------------------------------------

    [Fact]
    public async Task Inbound_text_from_an_opted_in_sender_stages_a_transaction()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var tenant = new TenantContext();
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        tenant.Resolve(tenantId, userId, "Owner");

        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();
        await SeedOptedInUserAsync(ctx, tenantId, userId, "5215512345678");

        var processor = NewProcessor(ctx, tenant);
        var outcome = await processor.ProcessAsync(
            new WhatsAppInboundMessage("wamid.text1", "5215512345678", WhatsAppInboundKind.Text,
                "gasté 200 en el Oxxo ayer", Media: null, MediaContentType: null),
            default);

        Assert.Equal(WhatsAppInboundOutcome.Staged, outcome);

        var staged = await ctx.Transactions.IgnoreQueryFilters().SingleAsync();
        Assert.False(staged.IsConfirmed); // lands in the review-and-confirm queue
        Assert.Equal(200m, staged.Amount);
        Assert.Equal(TransactionDirection.Debit, staged.Direction);
        Assert.Equal(userId, staged.AttributedUserId);
        Assert.Equal(tenantId, staged.TenantId);
    }

    // ---- Inbound image → staged transaction (reuses the receipt path) -----------------------------

    [Fact]
    public async Task Inbound_image_from_an_opted_in_sender_queues_a_receipt_for_ocr()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var tenant = new TenantContext();
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        tenant.Resolve(tenantId, userId, "Owner");

        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();
        await SeedOptedInUserAsync(ctx, tenantId, userId, "5215599998888");

        var processor = NewProcessor(ctx, tenant);

        // The fake media downloader returns a deterministic receipt text the FakeReceiptExtractor reads.
        var media = await new FakeWhatsAppMediaDownloader().DownloadAsync("media-1", default);
        var outcome = await processor.ProcessAsync(
            new WhatsAppInboundMessage("wamid.img1", "5215599998888", WhatsAppInboundKind.Image,
                Text: null, media!.Content, media.ContentType),
            default);

        Assert.Equal(WhatsAppInboundOutcome.Staged, outcome);

        // The receipt ingestion path stored a receipt + raised a receipt.parse outbox message; the worker
        // OCR (covered by ReceiptScanningTests) then stages the transaction.
        var receipt = await ctx.Receipts.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(tenantId, receipt.TenantId);
        var outbox = await ctx.Outbox.IgnoreQueryFilters().SingleAsync(m => m.Type == ReceiptIngestionService.OutboxType);
        Assert.Equal(receipt.Id.ToString(), outbox.Payload);
    }

    // ---- Unknown / not-opted-in sender: help reply, no tenant data --------------------------------

    [Fact]
    public async Task Unknown_sender_gets_a_help_reply_and_no_transaction_is_staged()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var tenant = new TenantContext();
        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var processor = NewProcessor(ctx, tenant);
        var outcome = await processor.ProcessAsync(
            new WhatsAppInboundMessage("wamid.stranger", "5210000000000", WhatsAppInboundKind.Text,
                "gasté 200 en el Oxxo", Media: null, MediaContentType: null),
            default);

        Assert.Equal(WhatsAppInboundOutcome.UnknownSender, outcome);
        Assert.Empty(await ctx.Transactions.IgnoreQueryFilters().ToListAsync());

        // A help reply was queued to the outbox, with no tenant attached (no household resolved).
        var reply = await ctx.Outbox.IgnoreQueryFilters().SingleAsync(m => m.Type == WhatsAppOutbox.OutboxType);
        Assert.Null(reply.TenantId);
    }

    [Fact]
    public async Task Sender_mapped_but_without_consent_is_treated_as_not_opted_in()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var tenant = new TenantContext();
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        tenant.Resolve(tenantId, userId, "Owner");

        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        // Contact mapping exists but NO WhatsAppChannel consent.
        ctx.WhatsAppContacts.Add(new WhatsAppContact
        {
            Id = Guid.CreateVersion7(), TenantId = tenantId, UserId = userId, PhoneNumber = "5215511112222",
        });
        await ctx.SaveChangesAsync();

        var processor = NewProcessor(ctx, tenant);
        var outcome = await processor.ProcessAsync(
            new WhatsAppInboundMessage("wamid.noconsent", "5215511112222", WhatsAppInboundKind.Text,
                "gasté 200 en el Oxxo", Media: null, MediaContentType: null),
            default);

        Assert.Equal(WhatsAppInboundOutcome.UnknownSender, outcome);
        Assert.Empty(await ctx.Transactions.IgnoreQueryFilters().ToListAsync());
    }

    // ---- Dedupe on the WhatsApp message id --------------------------------------------------------

    [Fact]
    public async Task Duplicate_message_id_is_ignored_so_no_second_transaction_is_staged()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var tenant = new TenantContext();
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        tenant.Resolve(tenantId, userId, "Owner");

        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();
        await SeedOptedInUserAsync(ctx, tenantId, userId, "5215512345678");

        // Share one dedupe store across both deliveries (as Redis is shared in prod).
        var dedupe = new InMemoryDedupeStore();
        var first = NewProcessor(ctx, tenant, dedupe);
        var message = new WhatsAppInboundMessage("wamid.dup", "5215512345678", WhatsAppInboundKind.Text,
            "gasté 200 en el Oxxo", Media: null, MediaContentType: null);

        Assert.Equal(WhatsAppInboundOutcome.Staged, await first.ProcessAsync(message, default));
        Assert.Equal(WhatsAppInboundOutcome.Duplicate, await NewProcessor(ctx, tenant, dedupe).ProcessAsync(message, default));

        Assert.Single(await ctx.Transactions.IgnoreQueryFilters().ToListAsync());
    }

    // ---- Outbound send enqueues an outbox message + the fake sender sends it -----------------------

    [Fact]
    public void Outbound_message_serializes_to_a_whatsapp_send_outbox_message()
    {
        var outbox = WhatsAppOutbox.Send(new WhatsAppMessage("5215512345678", "Hola"), Guid.CreateVersion7());

        Assert.Equal(WhatsAppOutbox.OutboxType, outbox.Type);
        var back = WhatsAppOutbox.Read(outbox.Payload);
        Assert.NotNull(back);
        Assert.Equal("5215512345678", back!.To);
        Assert.Equal("Hola", back.Body);
    }

    [Fact]
    public async Task Fake_sender_records_what_it_sent()
    {
        var sender = new FakeWhatsAppSender(NullLogger<FakeWhatsAppSender>.Instance);
        await sender.SendAsync(new WhatsAppMessage("5215512345678", "Hola"), default);

        var sent = Assert.Single(sender.Sent);
        Assert.Equal("5215512345678", sent.To);
    }

    // ---- Fake-vs-real DI selection ----------------------------------------------------------------

    [Fact]
    public void Connector_uses_the_fake_sender_without_credentials()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWhatsAppConnector(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        Assert.IsType<FakeWhatsAppSender>(provider.GetRequiredService<IWhatsAppSender>());
        Assert.IsType<FakeWhatsAppMediaDownloader>(provider.GetRequiredService<IWhatsAppMediaDownloader>());
    }

    [Fact]
    public void Connector_uses_the_live_meta_sender_when_credentials_are_configured()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WhatsApp:AccessToken"] = "EAA-real-token",
                ["WhatsApp:PhoneNumberId"] = "102226306899880",
            })
            .Build();
        services.AddWhatsAppConnector(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<MetaWhatsAppSender>(provider.GetRequiredService<IWhatsAppSender>());
    }

    // ---- helpers ----------------------------------------------------------------------------------

    private static async Task SeedOptedInUserAsync(LedgerDbContext ctx, Guid tenantId, Guid userId, string phone)
    {
        ctx.Users.Add(new User
        {
            Id = userId, TenantId = tenantId, Email = "owner@example.com",
            ExternalAuthId = "ext-1", Role = UserRole.Owner,
        });
        ctx.Accounts.Add(new Account
        {
            Id = Guid.CreateVersion7(), TenantId = tenantId, Name = "BBVA", Currency = "MXN",
        });
        ctx.WhatsAppContacts.Add(new WhatsAppContact
        {
            Id = Guid.CreateVersion7(), TenantId = tenantId, UserId = userId, PhoneNumber = phone,
        });
        ctx.Consents.Add(new ConsentRecord
        {
            Id = Guid.CreateVersion7(), TenantId = tenantId, UserId = userId,
            Type = ConsentType.WhatsAppChannel, Version = "whatsapp-v1", GrantedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();
    }

    private static WhatsAppInboundProcessor NewProcessor(
        LedgerDbContext ctx, TenantContext tenant, IWhatsAppDedupeStore? dedupe = null)
    {
        var categorizer = new RuleCategorizer(ctx);
        var parser = new FakeNaturalLanguageTransactionParser(categorizer, TimeProvider.System);
        var receipts = new ReceiptIngestionService(ctx, tenant, new DbFileStore(ctx));
        return new WhatsAppInboundProcessor(
            ctx, tenant, dedupe ?? new InMemoryDedupeStore(), parser, receipts,
            NullLogger<WhatsAppInboundProcessor>.Instance);
    }

    private static WhatsAppWebhookHandler NewWebhookHandler(
        WhatsAppOptions options, IWhatsAppInboundProcessor processor, IWhatsAppMediaDownloader downloader) =>
        new(Microsoft.Extensions.Options.Options.Create(options), processor, downloader,
            NullLogger<WhatsAppWebhookHandler>.Instance);

    /// <summary>An in-memory dedupe store standing in for Redis in tests.</summary>
    private sealed class InMemoryDedupeStore : IWhatsAppDedupeStore
    {
        private readonly HashSet<string> _seen = [];

        public Task<bool> TryMarkProcessedAsync(string messageId, CancellationToken ct) =>
            Task.FromResult(_seen.Add(messageId));
    }
}
