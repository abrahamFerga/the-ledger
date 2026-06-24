using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TheLedger.Application.Ledger;
using TheLedger.Domain.Ledger;
using TheLedger.Infrastructure.Categorization;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.IntegrationTests;

public class CategorizationTests
{
    private static async Task<LedgerDbContext> SeededContextAsync(SqliteConnection connection)
    {
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        var ctx = new LedgerDbContext(
            new DbContextOptionsBuilder<LedgerDbContext>().UseSqlite(connection).Options, tenant);
        await ctx.Database.EnsureCreatedAsync();
        return ctx;
    }

    [Fact]
    public async Task Llm_categorizer_maps_the_model_answer_to_a_category()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var ctx = await SeededContextAsync(connection);

        var fake = new FakeChatClient("Dining");
        var result = await new LlmCategorizer(fake, ctx).CategorizeAsync("TAQUERIA EL PASTOR", default);

        Assert.Equal(SystemCategories.Dining, result.CategoryId);
        Assert.Equal(CategorizationSource.Llm, result.Source);
    }

    [Fact]
    public async Task Llm_categorizer_redacts_pii_before_calling_the_model()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var ctx = await SeededContextAsync(connection);

        var fake = new FakeChatClient("Shopping");
        await new LlmCategorizer(fake, ctx).CategorizeAsync("PAGO TARJETA 4111111111111111 CLABE 002180001234567890", default);

        var prompt = Assert.Single(fake.SeenPrompts);
        Assert.DoesNotContain("4111111111111111", prompt);
        Assert.DoesNotContain("002180001234567890", prompt);
    }

    [Fact]
    public async Task Composite_uses_rules_first_then_falls_back_to_the_llm()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var ctx = await SeededContextAsync(connection);

        var fake = new FakeChatClient("Entertainment");
        var services = new ServiceCollection();
        services.AddSingleton<IChatClient>(fake);
        var composite = new CompositeCategorizer(ctx, services.BuildServiceProvider());

        var ruleHit = await composite.CategorizeAsync("OXXO TIENDA", default);
        Assert.Equal(SystemCategories.Groceries, ruleHit.CategoryId);
        Assert.Empty(fake.SeenPrompts); // rule matched — model not consulted

        var llmHit = await composite.CategorizeAsync("CLUB NOCTURNO DESCONOCIDO", default);
        Assert.Equal(SystemCategories.Entertainment, llmHit.CategoryId);
        Assert.Equal(CategorizationSource.Llm, llmHit.Source);
        Assert.Single(fake.SeenPrompts);
    }

    private sealed class FakeChatClient(string reply) : IChatClient
    {
        public List<string> SeenPrompts { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            SeenPrompts.Add(string.Concat(messages.Select(m => m.Text)));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
