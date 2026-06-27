using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Ingestion.QuickAdd;
using TheLedger.Application.Ledger;
using TheLedger.Domain.Ledger;
using TheLedger.Infrastructure.Categorization;
using TheLedger.Infrastructure.Ingestion;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Tenancy;
using Xunit;

namespace TheLedger.IntegrationTests;

/// <summary>
/// NL quick-add (feature #51, ADR-0011) against the deterministic fake parser — no Azure dependency.
/// Covers Spanish phrasings: relative dates ("ayer"/"antier"/"el lunes") pinned to America/Mexico_City,
/// amounts with MXN, and income-vs-expense direction. A fixed clock makes the dates deterministic.
/// </summary>
public class QuickAddParserTests
{
    // 2026-06-26 is a Friday. Anchor "now" just after Mexico City midnight (UTC-6) so the local date is the 26th.
    private static readonly DateTimeOffset FixedNowUtc = new(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static async Task<LedgerDbContext> SeededContextAsync(SqliteConnection connection)
    {
        var tenant = new TenantContext();
        tenant.Resolve(Guid.CreateVersion7(), null, "Owner");
        var ctx = new LedgerDbContext(
            new DbContextOptionsBuilder<LedgerDbContext>().UseSqlite(connection).Options, tenant);
        await ctx.Database.EnsureCreatedAsync();
        return ctx;
    }

    private static FakeNaturalLanguageTransactionParser ParserOver(LedgerDbContext ctx) =>
        new(new RuleCategorizer(ctx), new FixedClock(FixedNowUtc));

    [Fact]
    public async Task Parses_expense_amount_merchant_and_proposes_category_via_categorizer()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var ctx = await SeededContextAsync(connection);

        var draft = await ParserOver(ctx).ParseAsync(new QuickAddRequest("gasté 200 en el Oxxo"), default);

        Assert.Equal(200m, draft.Amount);
        Assert.Equal("MXN", draft.Currency);
        Assert.Equal(TransactionDirection.Debit, draft.Direction);
        Assert.Contains("Oxxo", draft.Merchant, StringComparison.OrdinalIgnoreCase);
        // Merchant → category comes from ICategorizer (OXXO → Groceries), not invented by the parse.
        Assert.Equal(SystemCategories.Groceries, draft.ProposedCategoryId);
    }

    [Fact]
    public async Task Resolves_ayer_to_yesterday_in_mexico_city()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var ctx = await SeededContextAsync(connection);

        var draft = await ParserOver(ctx).ParseAsync(new QuickAddRequest("comí 350 en restaurante ayer"), default);

        Assert.Equal(350m, draft.Amount);
        Assert.Equal(new DateOnly(2026, 6, 25), draft.Date); // 26th − 1
        Assert.Equal(TransactionDirection.Debit, draft.Direction);
        Assert.Equal(SystemCategories.Dining, draft.ProposedCategoryId); // RESTAURANTE → Dining
    }

    [Fact]
    public async Task Resolves_antier_to_two_days_ago()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var ctx = await SeededContextAsync(connection);

        var draft = await ParserOver(ctx).ParseAsync(new QuickAddRequest("pagué 80 de uber antier"), default);

        Assert.Equal(new DateOnly(2026, 6, 24), draft.Date); // 26th − 2
        Assert.Equal(SystemCategories.Transport, draft.ProposedCategoryId); // UBER → Transport
    }

    [Fact]
    public async Task Detects_income_direction_for_cobre_and_deposito()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var ctx = await SeededContextAsync(connection);
        var parser = ParserOver(ctx);

        var cobre = await parser.ParseAsync(new QuickAddRequest("cobré 5000 de nómina hoy"), default);
        Assert.Equal(TransactionDirection.Credit, cobre.Direction);
        Assert.Equal(5000m, cobre.Amount);
        Assert.Equal(new DateOnly(2026, 6, 26), cobre.Date);

        var deposito = await parser.ParseAsync(new QuickAddRequest("me pagaron 1,250.50 de freelance"), default);
        Assert.Equal(TransactionDirection.Credit, deposito.Direction);
        Assert.Equal(1250.50m, deposito.Amount); // thousands separator handled
    }

    [Fact]
    public async Task Resolves_el_lunes_to_most_recent_past_monday()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var ctx = await SeededContextAsync(connection);

        var draft = await ParserOver(ctx).ParseAsync(new QuickAddRequest("gasté 120 en super el lunes"), default);

        // Friday 2026-06-26 → most recent past Monday is 2026-06-22.
        Assert.Equal(new DateOnly(2026, 6, 22), draft.Date);
    }

    [Fact]
    public void Mexico_city_clock_resolves_weekday_to_most_recent_past_occurrence()
    {
        var friday = new DateOnly(2026, 6, 26); // Friday
        Assert.Equal(new DateOnly(2026, 6, 22), MexicoCityClock.ResolveRelative("el lunes", friday));   // Mon before
        Assert.Equal(new DateOnly(2026, 6, 24), MexicoCityClock.ResolveRelative("el miércoles", friday)); // accent-insensitive
        Assert.Equal(new DateOnly(2026, 6, 19), MexicoCityClock.ResolveRelative("el viernes", friday));  // same dow → 7 days back
    }

    [Fact]
    public async Task Empty_or_amountless_text_yields_low_confidence_draft_anchored_today()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var ctx = await SeededContextAsync(connection);

        var draft = await ParserOver(ctx).ParseAsync(new QuickAddRequest("no idea"), default);

        Assert.Equal(0m, draft.Amount);
        Assert.True(draft.Confidence < 0.5, "amountless phrase should be low confidence so the UI pre-fills the form");
        Assert.Equal(new DateOnly(2026, 6, 26), draft.Date);
    }
}
