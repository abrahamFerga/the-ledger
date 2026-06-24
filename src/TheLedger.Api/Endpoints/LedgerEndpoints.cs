using TheLedger.Application.Authorization;
using TheLedger.Application.Ledger;

namespace TheLedger.Api.Endpoints;

public static class LedgerEndpoints
{
    /// <summary>Unified ledger (feature #13): the transaction feed, edit/split/recategorize, and categories.</summary>
    public static void MapLedger(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        v1.MapGet("/ledger", async (Guid? accountId, Guid? categoryId, bool? confirmedOnly, ILedgerService svc, CancellationToken ct) =>
                Results.Ok(await svc.GetFeedAsync(new TransactionFeedQuery(accountId, categoryId, confirmedOnly ?? true), ct)))
            .WithTags("Ledger")
            .RequireAuthorization(Policies.TransactionsView);

        var transactions = v1.MapGroup("/transactions").WithTags("Transactions");
        transactions.MapPatch("/{id:guid}", async (Guid id, UpdateTransactionRequest req, ILedgerService svc, CancellationToken ct) =>
                await svc.UpdateTransactionAsync(id, req, ct) is { } dto ? Results.Ok(dto) : Results.NotFound())
            .RequireAuthorization(Policies.TransactionsEdit);
        transactions.MapPost("/{id:guid}/split", async (Guid id, SplitTransactionRequest req, ILedgerService svc, CancellationToken ct) =>
                Results.Ok(await svc.SplitTransactionAsync(id, req, ct)))
            .RequireAuthorization(Policies.TransactionsEdit);

        var categories = v1.MapGroup("/categories").WithTags("Categories");
        categories.MapGet("/", async (ILedgerService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListCategoriesAsync(ct)))
            .RequireAuthorization(Policies.TransactionsView);
        categories.MapPost("/", async (CreateCategoryRequest req, ILedgerService svc, CancellationToken ct) =>
                Results.Ok(await svc.CreateCategoryAsync(req, ct)))
            .RequireAuthorization(Policies.TransactionsEdit);
    }
}
