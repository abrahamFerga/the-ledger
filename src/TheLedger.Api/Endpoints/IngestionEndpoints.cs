using Microsoft.AspNetCore.Mvc;
using TheLedger.Api.Setup;
using TheLedger.Application.Authorization;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Ingestion.QuickAdd;
using TheLedger.Application.Ingestion.Receipts;

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

        // NL quick-add (feature #51, ADR-0011): parse a free-text/dictated phrase into a transaction DRAFT.
        // The draft is returned for explicit user confirmation — it is NOT persisted here. On confirm, the
        // SPA replays it through POST /transactions (the manual-create path above). Tenant-scoped + RBAC; the
        // existing Idempotency-Key middleware participates as on any write; errors are Problem Details.
        transactions.MapPost("/quick-add",
                async (QuickAddRequest req, INaturalLanguageTransactionParser parser, CancellationToken ct) =>
                {
                    if (string.IsNullOrWhiteSpace(req.Text))
                    {
                        return Results.Problem(
                            title: "Empty quick-add text",
                            detail: "Provide a phrase to parse, e.g. \"gasté 200 en el Oxxo\".",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    var draft = await parser.ParseAsync(req, ct);
                    return Results.Ok(draft);
                })
            .RequireAuthorization(Policies.TransactionsEdit);

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
            .RequireRateLimiting(RateLimitingSetup.UploadPolicy)
            .DisableAntiforgery();
        statements.MapPost("/{statementId:guid}/confirm", async (Guid statementId, IIngestionService svc, CancellationToken ct) =>
                Results.Ok(await svc.ConfirmStatementAsync(statementId, ct)))
            .RequireAuthorization(Policies.TransactionsEdit);

        // Receipt/ticket capture (feature #49, ADR-0009): snap a photo → staged transaction via OCR.
        // Multipart image upload stores the image (IFileStore) + raises an outbox message; the worker
        // runs Document Intelligence → normalization → a staged transaction in the review queue.
        var receipts = v1.MapGroup("/receipts").WithTags("Receipts");
        receipts.MapPost("/", async (
                IFormFile file, [FromForm] Guid accountId, IReceiptIngestionService svc, CancellationToken ct) =>
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                var dto = await svc.UploadAsync(accountId, file.FileName, file.ContentType, ms.ToArray(), ct);
                return Results.Accepted($"/api/v1/receipts/{dto.Id}", dto);
            })
            .RequireAuthorization(Policies.StatementsUpload)
            .RequireRateLimiting(RateLimitingSetup.UploadPolicy)
            .DisableAntiforgery();
        receipts.MapGet("/", async (IReceiptIngestionService svc, CancellationToken ct) =>
                Results.Ok(await svc.ListAsync(ct)))
            .RequireAuthorization(Policies.TransactionsView);
    }
}
