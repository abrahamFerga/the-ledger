using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Ingestion.Receipts;
using TheLedger.Application.Storage;
using TheLedger.Domain.Ledger;
using TheLedger.Domain.Receipts;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Services;
using TheLedger.Infrastructure.Storage;

namespace TheLedger.Infrastructure.Receipts;

/// <summary>
/// Handles the <c>receipt.parse</c> outbox job (epic 9, ADR-0009): loads the uploaded image, runs the
/// <see cref="IReceiptExtractor"/> (Document Intelligence in prod, a fake in dev/CI), normalizes the
/// merchant + proposes a category via the <see cref="ReceiptNormalizationAgent"/>, and stages an
/// unconfirmed <see cref="Transaction"/> in the existing review-and-confirm queue. Low-confidence
/// extraction flags the receipt for review. Runs in the worker outside any request, so reads ignore
/// the tenant query filter and writes stamp the receipt's tenant explicitly.
/// </summary>
public sealed class ReceiptParseHandler(
    LedgerDbContext db,
    IReceiptExtractor extractor,
    ReceiptNormalizationAgent normalizer,
    ILogger<ReceiptParseHandler> logger,
    IFileStore fileStore)
{
    /// <summary>Below this overall/field confidence the receipt is flagged for human review.</summary>
    public const double ReviewThreshold = 0.6;

    public async Task HandleAsync(Guid receiptId, CancellationToken ct)
    {
        var receipt = await db.Receipts.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == receiptId, ct);
        if (receipt is null)
        {
            logger.LogWarning("Receipt {Id} not found for OCR", receiptId);
            return;
        }

        receipt.Status = ReceiptStatus.Processing;
        await db.SaveChangesAsync(ct);

        try
        {
            var image = await fileStore.GetAsync(ReceiptIngestionService.FileKey(receipt.Id), ct);
            if (image is null)
            {
                receipt.Status = ReceiptStatus.Failed;
                receipt.Error = "No stored image for receipt.";
                await db.SaveChangesAsync(ct);
                logger.LogWarning("No stored image for receipt {Id}", receiptId);
                return;
            }

            var account = await db.Accounts.IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == receipt.AccountId, ct);

            var extraction = await extractor.ExtractAsync(image, receipt.ContentType, ct);
            var normalization = await normalizer.NormalizeAsync(extraction, ct);

            var amount = extraction.Total ?? 0m;
            var date = extraction.TransactionDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var currency = string.IsNullOrWhiteSpace(extraction.Currency)
                ? account?.Currency ?? "MXN"
                : extraction.Currency!;

            var needsReview =
                (extraction.OverallConfidence ?? 0) < ReviewThreshold
                || extraction.Total is null
                || extraction.TransactionDate is null
                || string.IsNullOrWhiteSpace(extraction.MerchantName);

            var transaction = new Transaction
            {
                Id = Guid.CreateVersion7(),
                TenantId = receipt.TenantId,
                AccountId = receipt.AccountId,
                Date = date,
                // PAN-mask the merchant before persistence (a ticket can show a partial card PAN; ADR-0002).
                Description = PanMasker.Mask(normalization.Merchant),
                Amount = amount,
                Currency = currency,
                Direction = TransactionDirection.Debit, // a store receipt is money out
                CategoryId = normalization.CategoryId,
                CategorizationSource = normalization.Source,
                Confidence = normalization.Confidence,
                IsConfirmed = false, // stays in the review-and-confirm queue
            };
            db.Transactions.Add(transaction);

            receipt.TransactionId = transaction.Id;
            receipt.Merchant = transaction.Description;
            receipt.TransactionDate = date;
            receipt.Total = amount;
            receipt.Tax = extraction.Tax;
            receipt.Currency = currency;
            receipt.Confidence = extraction.OverallConfidence;
            receipt.NeedsReview = needsReview;
            receipt.Status = ReceiptStatus.Extracted;

            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Extracted receipt {Id}: merchant {Merchant}, total {Total} {Currency}, confidence {Confidence}, needsReview {NeedsReview}",
                receipt.Id, receipt.Merchant, amount, currency, extraction.OverallConfidence, needsReview);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to OCR receipt {Id}", receiptId);
            receipt.Status = ReceiptStatus.Failed;
            receipt.Error = ex.Message;
            await db.SaveChangesAsync(ct);
        }
    }
}
