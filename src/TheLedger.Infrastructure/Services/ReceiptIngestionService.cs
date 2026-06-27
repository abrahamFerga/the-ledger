using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Abstractions;
using TheLedger.Application.Ingestion.Receipts;
using TheLedger.Application.Storage;
using TheLedger.Domain.Outbox;
using TheLedger.Domain.Receipts;
using TheLedger.Infrastructure.Persistence;
using TheLedger.Infrastructure.Storage;

namespace TheLedger.Infrastructure.Services;

/// <summary>
/// API-path receipt ingestion (epic 9, ADR-0009): records the upload, stores the image via the
/// existing <see cref="IFileStore"/>, and raises a <c>receipt.parse</c> outbox message — exactly the
/// pattern statement PDF upload uses. The worker drains the outbox to run OCR + normalization and
/// stage a transaction; nothing posts to the ledger without later user confirmation.
/// </summary>
public sealed class ReceiptIngestionService(LedgerDbContext db, ITenantContext tenant, IFileStore fileStore)
    : IReceiptIngestionService
{
    public const string OutboxType = "receipt.parse";

    // The image is stored keyed by the receipt id (a bare GUID), exactly as statements key on the
    // statement id — so both the DB-backed default store and the Azure Blob store work unchanged.
    internal static string FileKey(Guid receiptId) => receiptId.ToString();

    public async Task<ReceiptDto> UploadAsync(
        Guid accountId, string fileName, string contentType, byte[] image, CancellationToken ct)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct)
                      ?? throw new KeyNotFoundException($"Account {accountId} not found.");

        var receipt = new Receipt
        {
            Id = Guid.CreateVersion7(),
            AccountId = account.Id,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType,
            FileRef = $"receipts/{account.TenantId}/{Guid.CreateVersion7()}/{fileName}",
            Status = ReceiptStatus.Uploaded,
            Currency = account.Currency,
            UploadedByUserId = tenant.UserId,
        };
        db.Receipts.Add(receipt);

        db.Outbox.Add(new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.TenantId,
            Type = OutboxType,
            Payload = receipt.Id.ToString(),
            Status = OutboxStatus.Pending,
        });

        await db.SaveChangesAsync(ct);
        await fileStore.SaveAsync(FileKey(receipt.Id), image, ct); // DB or Azure Blob
        return ToDto(receipt);
    }

    public async Task<IReadOnlyList<ReceiptDto>> ListAsync(CancellationToken ct)
    {
        var receipts = await db.Receipts.OrderByDescending(r => r.Id).ToListAsync(ct);
        return receipts.Select(ToDto).ToList();
    }

    private static ReceiptDto ToDto(Receipt r) =>
        new(r.Id, r.AccountId, r.Status.ToString(), r.Merchant, r.TransactionDate,
            r.Total, r.Currency, r.Confidence, r.NeedsReview, r.TransactionId);
}
