using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TheLedger.Application.Ingestion.QuickAdd;
using TheLedger.Application.Ledger;
using TheLedger.Domain.Consent;
using TheLedger.Domain.Ledger;
using TheLedger.Infrastructure.Categorization;
using TheLedger.Infrastructure.Ingestion;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.IntegrationTests;

/// <summary>
/// NL quick-add (feature #51, ADR-0011) LLM-forward path: structured-output deserialization, server-side
/// date resolution to America/Mexico_City, PII redaction before the model call, category reuse via
/// <see cref="ICategorizer"/>, and the consent gate (fake-vs-real selection in the composite).
/// </summary>
public class QuickAddLlmParserTests
{
    private static readonly DateTimeOffset FixedNowUtc = new(2026, 6, 26, 12, 0, 0, TimeSpan.Zero); // Friday

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static async Task<LedgerDbContext> SeededContextAsync(SqliteConnection connection, TenantContext tenant)
    {
        var ctx = new LedgerDbContext(
            new DbContextOptionsBuilder<LedgerDbContext>().UseSqlite(connection).Options, tenant);
        await ctx.Database.EnsureCreatedAsync();
        return ctx;
    }

    [Fact]
    public async Task Llm_parser_maps_structured_output_and_resolves_relative_date()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), Guid.CreateVersion7(), "Owner");
        await using var ctx = await SeededContextAsync(connection, tenant);

        var fake = new FakeChatClient(
            """{"amount": 350, "currency": "MXN", "direction": "Debit", "merchant": "restaurante", "relativeDate": "ayer"}""");
        var parser = new LlmNaturalLanguageTransactionParser(fake, new RuleCategorizer(ctx), new FixedClock(FixedNowUtc));

        var draft = await parser.ParseAsync(new QuickAddRequest("comí 350 en restaurante ayer"), default);

        Assert.Equal(350m, draft.Amount);
        Assert.Equal("MXN", draft.Currency);
        Assert.Equal(TransactionDirection.Debit, draft.Direction);
        Assert.Equal(new DateOnly(2026, 6, 25), draft.Date); // "ayer" resolved server-side in MX tz
        Assert.Equal("restaurante", draft.Merchant);
        Assert.Equal(SystemCategories.Dining, draft.ProposedCategoryId); // categorizer, not the LLM
    }

    [Fact]
    public async Task Llm_parser_redacts_pii_before_calling_the_model()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), Guid.CreateVersion7(), "Owner");
        await using var ctx = await SeededContextAsync(connection, tenant);

        var fake = new FakeChatClient(
            """{"amount": 500, "currency": "MXN", "direction": "Debit", "merchant": "pago", "relativeDate": "hoy"}""");
        var parser = new LlmNaturalLanguageTransactionParser(fake, new RuleCategorizer(ctx), new FixedClock(FixedNowUtc));

        await parser.ParseAsync(
            new QuickAddRequest("pago 500 a tarjeta 4111111111111111 clabe 002180001234567890"), default);

        var prompt = string.Concat(fake.SeenPrompts);
        Assert.DoesNotContain("4111111111111111", prompt);
        Assert.DoesNotContain("002180001234567890", prompt);
    }

    [Fact]
    public async Task Composite_uses_fake_when_user_has_not_opted_in_even_if_a_model_is_configured()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), Guid.CreateVersion7(), "Owner");
        await using var ctx = await SeededContextAsync(connection, tenant);

        // A model IS configured, but no LlmCategorization consent for this user → must use the fake.
        var fake = new FakeChatClient("""{"amount": 999, "merchant": "wrong"}""");
        var services = new ServiceCollection();
        services.AddSingleton<IChatClient>(fake);
        var composite = new CompositeNaturalLanguageTransactionParser(
            ctx, tenant, new RuleCategorizer(ctx), new FixedClock(FixedNowUtc), services.BuildServiceProvider());

        var draft = await composite.ParseAsync(new QuickAddRequest("gasté 200 en el Oxxo"), default);

        Assert.Empty(fake.SeenPrompts); // model not consulted — opted out
        Assert.Equal(200m, draft.Amount); // deterministic fake produced the draft
        Assert.Equal(SystemCategories.Groceries, draft.ProposedCategoryId);
    }

    [Fact]
    public async Task Composite_uses_llm_when_user_opted_in_and_a_model_is_configured()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var tenant = new TenantContext();
        tenant.Resolve(tenantId, userId, "Owner");
        await using var ctx = await SeededContextAsync(connection, tenant);

        ctx.Consents.Add(new ConsentRecord
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId, // the AuditAndTenantInterceptor stamps this in production
            UserId = userId,
            Type = ConsentType.LlmCategorization,
            Version = "v1",
            GrantedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var fake = new FakeChatClient(
            """{"amount": 75, "currency": "MXN", "direction": "Debit", "merchant": "cafe", "relativeDate": "hoy"}""");
        var services = new ServiceCollection();
        services.AddSingleton<IChatClient>(fake);
        var composite = new CompositeNaturalLanguageTransactionParser(
            ctx, tenant, new RuleCategorizer(ctx), new FixedClock(FixedNowUtc), services.BuildServiceProvider());

        var draft = await composite.ParseAsync(new QuickAddRequest("algo raro que el fake no parsea"), default);

        Assert.Single(fake.SeenPrompts); // model consulted — opted in
        Assert.Equal(75m, draft.Amount); // came from the LLM structured output
    }

    /// <summary>
    /// Minimal MEAI <see cref="IChatClient"/> stub that records prompts and replies with a fixed payload.
    /// Honors the request's JSON-schema response format by returning the canned JSON as the response text.
    /// </summary>
    private sealed class FakeChatClient(string jsonReply) : IChatClient
    {
        public List<string> SeenPrompts { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            SeenPrompts.Add(string.Concat(messages.Select(m => m.Text)));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonReply)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
