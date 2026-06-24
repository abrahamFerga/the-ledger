using Microsoft.AspNetCore.Mvc;
using TheLedger.Application.Authorization;
using TheLedger.Application.Ingestion;

namespace TheLedger.Api.Endpoints;

public static class IngestionEndpoints
{
    /// <summary>Ingestion surface (feature #11): accounts, manual entry, CSV/PDF upload, review + confirm.</summary>
    public static void MapIngestion(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        var accounts = v1.MapGroup("/accounts").WithTags("Accounts");
        accounts.MapGet("/", async (IIngestionService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListAccountsAsync(ct)))
            .RequireAuthorization(Policies.AccountsView);
        accounts.MapPost("/", async (CreateAccountRequest req, IIngestionService svc, CancellationToken ct) =>
                Results.Ok(await svc.CreateAccountAsync(req, ct)))
            .RequireAuthorization(Policies.AccountsManage);

        var transactions = v1.MapGroup("/transactions").WithTags("Transactions");
        transactions.MapPost("/", async (ManualTransactionRequest req, IIngestionService svc, CancellationToken ct) =>
                Results.Ok(await svc.AddManualTransactionAsync(req, ct)))
            .RequireAuthorization(Policies.TransactionsEdit);
        transactions.MapGet("/review", async (Guid? statementId, IIngestionService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListReviewQueueAsync(statementId, ct)))
            .RequireAuthorization(Policies.TransactionsView);

        var statements = v1.MapGroup("/statements").WithTags("Statements");
        statements.MapPost("/csv", async (ImportCsvRequest req, IIngestionService svc, CancellationToken ct) =>
                Results.Ok(await svc.ImportCsvAsync(req, ct)))
            .RequireAuthorization(Policies.StatementsUpload);
        statements.MapPost("/pdf", async (IFormFile file, [FromForm] Guid accountId, IIngestionService svc, CancellationToken ct) =>
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                return Results.Ok(await svc.UploadPdfAsync(accountId, file.FileName, ms.ToArray(), ct));
            })
            .RequireAuthorization(Policies.StatementsUpload)
            .DisableAntiforgery();
        statements.MapPost("/{statementId:guid}/confirm", async (Guid statementId, IIngestionService svc, CancellationToken ct) =>
                Results.Ok(await svc.ConfirmStatementAsync(statementId, ct)))
            .RequireAuthorization(Policies.TransactionsEdit);
    }
}
