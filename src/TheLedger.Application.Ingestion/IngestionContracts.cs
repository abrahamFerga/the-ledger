namespace TheLedger.Application.Ingestion;

public sealed record CreateAccountRequest(string Name, string Type, string? Institution, string? Currency, string? Number);

public sealed record AccountDto(
    Guid Id, string Name, string Type, string? Institution, string Currency, string? MaskedNumber, decimal CurrentBalance);

public sealed record ManualTransactionRequest(Guid AccountId, DateOnly Date, string Description, decimal Amount, string Direction);

public sealed record TransactionDto(
    Guid Id, Guid AccountId, Guid? StatementId, DateOnly Date, string Description,
    decimal Amount, string Currency, string Direction, bool IsConfirmed);

public sealed record StatementDto(Guid Id, Guid AccountId, string Source, string Status, int TransactionCount);

public sealed record ImportCsvRequest(Guid AccountId, string FileName, string Content);

/// <summary>
/// Ingestion of transactions into the ledger from CSV, manual entry, or a PDF upload (staged for the
/// parse worker). Imported rows land in the review queue (unconfirmed) until the user confirms.
/// </summary>
public interface IIngestionService
{
    Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct);
    Task<IReadOnlyList<AccountDto>> ListAccountsAsync(CancellationToken ct);

    Task<TransactionDto> AddManualTransactionAsync(ManualTransactionRequest request, CancellationToken ct);

    Task<StatementDto> ImportCsvAsync(ImportCsvRequest request, CancellationToken ct);
    Task<StatementDto> UploadPdfAsync(Guid accountId, string fileName, byte[] content, CancellationToken ct);

    Task<IReadOnlyList<TransactionDto>> ListReviewQueueAsync(Guid? statementId, CancellationToken ct);
    Task<StatementDto> ConfirmStatementAsync(Guid statementId, CancellationToken ct);
}
