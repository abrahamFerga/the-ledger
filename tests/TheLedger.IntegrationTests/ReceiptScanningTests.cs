using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TheLedger.Application.Ingestion.Receipts;
using TheLedger.Application.Storage;
using TheLedger.Domain.Accounts;
using TheLedger.Domain.Ledger;
using TheLedger.Domain.Outbox;
using TheLedger.Domain.Receipts;
using TheLedger.Infrastructure.Azure;
using TheLedger.Infrastructure.Categorization;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Receipts;
using TheLedger.Infrastructure.Services;
using TheLedger.Infrastructure.Storage;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.IntegrationTests;

/// <summary>
/// Receipt/ticket OCR capture (feature #49, ADR-0009): the fake extractor + the worker parse handler
/// produce a staged transaction in the existing review-and-confirm queue, with low-confidence
/// extraction flagged for review. No Azure dependency — the deterministic fake backs every assertion.
/// </summary>
public class ReceiptScanningTests
{
    private static LedgerDbContext NewContext(SqliteConnection connection, TenantContext tenant) =>
        new(new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditAndTenantInterceptor(tenant)) // stamps TenantId, as AddInfrastructure does in prod
            .Options, tenant);

    private static byte[] FakeReceipt(string body) => Encoding.UTF8.GetBytes(body);

    [Fact]
    public async Task Fake_extractor_reads_merchant_total_date_and_items_deterministically()
    {
        var image = FakeReceipt(
            "merchant: OXXO TIENDA 1234\n" +
            "date: 2026-01-15\n" +
            "total: 152.50\n" +
            "tax: 21.03\n" +
            "currency: MXN\n" +
            "item: Coca Cola=18.00\n" +
            "item: Sabritas=22.50\n" +
            "confidence: 0.94\n");

        var result = await new FakeReceiptExtractor().ExtractAsync(image, "image/jpeg", default);

        Assert.Equal("OXXO TIENDA 1234", result.MerchantName);
        Assert.Equal(new DateOnly(2026, 1, 15), result.TransactionDate);
        Assert.Equal(152.50m, result.Total);
        Assert.Equal(21.03m, result.Tax);
        Assert.Equal("MXN", result.Currency);
        Assert.Equal(2, result.LineItems.Count);
        Assert.Equal(18.00m, result.LineItems[0].Amount);
        Assert.Equal(0.94, result.OverallConfidence);
    }

    [Fact]
    public async Task Fake_extractor_returns_low_confidence_for_unreadable_image()
    {
        var result = await new FakeReceiptExtractor().ExtractAsync(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg", default);

        Assert.True((result.OverallConfidence ?? 1) < 0.6);
    }

    [Fact]
    public async Task Upload_then_worker_ocr_stages_a_transaction_in_the_review_queue()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), Guid.CreateVersion7(), "Owner");
        var tenantId = tenant.TenantId!.Value;

        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var account = new Account { Id = Guid.CreateVersion7(), TenantId = tenantId, Name = "BBVA", Currency = "MXN" };
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var fileStore = new DbFileStore(ctx);
        var ingest = new ReceiptIngestionService(ctx, tenant, fileStore);

        var image = FakeReceipt(
            "merchant: STARBUCKS REFORMA\n" +
            "date: 2026-02-03\n" +
            "total: 89.00\n" +
            "currency: MXN\n" +
            "confidence: 0.93\n");

        // API path: stores the image + raises the outbox message; receipt is queued, not yet a transaction.
        var dto = await ingest.UploadAsync(account.Id, "ticket.jpg", "image/jpeg", image, default);
        Assert.Equal(ReceiptStatus.Uploaded.ToString(), dto.Status);

        var outbox = await ctx.Outbox.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(ReceiptIngestionService.OutboxType, outbox.Type);
        Assert.Equal(dto.Id.ToString(), outbox.Payload);
        Assert.Equal(OutboxStatus.Pending, outbox.Status);

        // Worker path: OCR → normalize → staged transaction.
        var normalizer = new ReceiptNormalizationAgent(EmptyProvider.Instance, new RuleCategorizer(ctx));
        var handler = new ReceiptParseHandler(
            ctx, new FakeReceiptExtractor(), normalizer, NullLogger<ReceiptParseHandler>.Instance, fileStore);
        await handler.HandleAsync(dto.Id, default);

        var receipt = await ctx.Receipts.IgnoreQueryFilters().FirstAsync(r => r.Id == dto.Id);
        Assert.Equal(ReceiptStatus.Extracted, receipt.Status);
        Assert.False(receipt.NeedsReview); // high confidence + all fields present
        Assert.NotNull(receipt.TransactionId);
        Assert.Equal(89.00m, receipt.Total);

        var staged = await ctx.Transactions.IgnoreQueryFilters().SingleAsync(t => t.Id == receipt.TransactionId);
        Assert.False(staged.IsConfirmed); // lands in the review-and-confirm queue
        Assert.Equal(TransactionDirection.Debit, staged.Direction);
        Assert.Equal(89.00m, staged.Amount);
        Assert.Equal("MXN", staged.Currency);
        Assert.Contains("STARBUCKS", staged.Description);
        // STARBUCKS maps to Dining via the default merchant rules — categorized without an LLM.
        Assert.Equal(CategorizationSource.Rule, staged.CategorizationSource);
    }

    [Fact]
    public async Task Low_confidence_extraction_flags_the_receipt_for_review()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), Guid.CreateVersion7(), "Owner");
        var tenantId = tenant.TenantId!.Value;

        await using var ctx = NewContext(connection, tenant);
        await ctx.Database.EnsureCreatedAsync();

        var account = new Account { Id = Guid.CreateVersion7(), TenantId = tenantId, Name = "BBVA", Currency = "MXN" };
        ctx.Accounts.Add(account);
        await ctx.SaveChangesAsync();

        var fileStore = new DbFileStore(ctx);
        var ingest = new ReceiptIngestionService(ctx, tenant, fileStore);

        // Blurry ticket: total read but very low confidence and no date.
        var image = FakeReceipt(
            "merchant: TIENDA LOCAL\n" +
            "total: 47.00\n" +
            "currency: MXN\n" +
            "confidence: 0.32\n");

        var dto = await ingest.UploadAsync(account.Id, "blurry.jpg", "image/jpeg", image, default);

        var normalizer = new ReceiptNormalizationAgent(EmptyProvider.Instance, new RuleCategorizer(ctx));
        var handler = new ReceiptParseHandler(
            ctx, new FakeReceiptExtractor(), normalizer, NullLogger<ReceiptParseHandler>.Instance, fileStore);
        await handler.HandleAsync(dto.Id, default);

        var receipt = await ctx.Receipts.IgnoreQueryFilters().FirstAsync(r => r.Id == dto.Id);
        Assert.Equal(ReceiptStatus.Extracted, receipt.Status);
        Assert.True(receipt.NeedsReview); // low confidence + missing date → flagged

        // It still stages a transaction (the user reviews + corrects it), it just isn't confirmed.
        var staged = await ctx.Transactions.IgnoreQueryFilters().SingleAsync(t => t.Id == receipt.TransactionId);
        Assert.False(staged.IsConfirmed);
    }

    [Fact]
    public void Azure_document_intelligence_is_not_registered_without_config()
    {
        var services = new ServiceCollection();
        services.AddAzureDocumentIntelligence(new ConfigurationBuilder().Build());
        Assert.Null(services.BuildServiceProvider().GetService<IReceiptExtractor>());
    }

    [Fact]
    public void Azure_document_intelligence_is_registered_when_configured()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentIntelligence:Endpoint"] = "https://example.cognitiveservices.azure.com/",
            })
            .Build();
        services.AddAzureDocumentIntelligence(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IReceiptExtractor>());
    }

    /// <summary>An empty service provider so the normalizer takes its no-IChatClient (rules-only) path.</summary>
    private sealed class EmptyProvider : IServiceProvider
    {
        public static readonly EmptyProvider Instance = new();
        public object? GetService(Type serviceType) => null;
    }
}
