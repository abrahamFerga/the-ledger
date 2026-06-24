using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Abstractions;
using TheLedger.Application.Ingestion;
using TheLedger.Application.Ingestion.Csv;
using TheLedger.Domain.Accounts;
using TheLedger.Domain.Ledger;
using TheLedger.Domain.Outbox;
using TheLedger.Domain.Statements;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Services;

public sealed class IngestionService(LedgerDbContext db, ITenantContext tenant) : IIngestionService
{
    public async Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct)
    {
        var account = new Account
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Type = Enum.TryParse<AccountType>(request.Type, ignoreCase: true, out var type) ? type : AccountType.Checking,
            Institution = request.Institution,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "MXN" : request.Currency!.ToUpperInvariant(),
            MaskedNumber = PanMasker.MaskNumber(request.Number),
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return ToDto(account);
    }

    public async Task<IReadOnlyList<AccountDto>> ListAccountsAsync(CancellationToken ct)
    {
        var accounts = await db.Accounts.OrderBy(a => a.Name).ToListAsync(ct);
        return accounts.Select(ToDto).ToList();
    }

    public async Task<TransactionDto> AddManualTransactionAsync(ManualTransactionRequest request, CancellationToken ct)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId, ct)
                      ?? throw new KeyNotFoundException($"Account {request.AccountId} not found.");

        var direction = Enum.TryParse<TransactionDirection>(request.Direction, ignoreCase: true, out var d)
            ? d
            : TransactionDirection.Debit;

        var transaction = new Transaction
        {
            Id = Guid.CreateVersion7(),
            AccountId = account.Id,
            Date = request.Date,
            Description = PanMasker.Mask(request.Description),
            Amount = Math.Abs(request.Amount),
            Currency = account.Currency,
            Direction = direction,
            IsConfirmed = true,
            CategorizationSource = CategorizationSource.Manual,
        };
        db.Transactions.Add(transaction);
        account.CurrentBalance += direction == TransactionDirection.Credit ? transaction.Amount : -transaction.Amount;

        await db.SaveChangesAsync(ct);
        return ToDto(transaction);
    }

    public async Task<StatementDto> ImportCsvAsync(ImportCsvRequest request, CancellationToken ct)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId, ct)
                      ?? throw new KeyNotFoundException($"Account {request.AccountId} not found.");

        var statement = new Statement
        {
            Id = Guid.CreateVersion7(),
            AccountId = account.Id,
            Source = StatementSource.Csv,
            FileRef = request.FileName,
            Status = StatementStatus.Parsed,
            UploadedByUserId = tenant.UserId,
        };
        db.Statements.Add(statement);

        var parsed = CsvStatementParser.Parse(request.Content);
        foreach (var row in parsed)
        {
            db.Transactions.Add(new Transaction
            {
                Id = Guid.CreateVersion7(),
                AccountId = account.Id,
                StatementId = statement.Id,
                Date = row.Date,
                Description = row.Description,
                Amount = row.Amount,
                Currency = account.Currency,
                Direction = row.Direction,
                IsConfirmed = false,
            });
        }

        statement.TransactionCount = parsed.Count;
        await db.SaveChangesAsync(ct);
        return ToDto(statement);
    }

    public async Task<StatementDto> UploadPdfAsync(Guid accountId, string fileName, byte[] content, CancellationToken ct)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct)
                      ?? throw new KeyNotFoundException($"Account {accountId} not found.");

        // Bootstrap: record the upload and enqueue a parse job. Blob storage + AI extraction land in #12.
        var statement = new Statement
        {
            Id = Guid.CreateVersion7(),
            AccountId = account.Id,
            Source = StatementSource.Pdf,
            FileRef = $"statements/{account.TenantId}/{Guid.CreateVersion7()}/{fileName}",
            Status = StatementStatus.Uploaded,
            UploadedByUserId = tenant.UserId,
        };
        db.Statements.Add(statement);

        db.StatementFiles.Add(new StatementFile
        {
            Id = Guid.CreateVersion7(),
            StatementId = statement.Id,
            Content = content,
            ContentType = "application/pdf",
        });

        db.Outbox.Add(new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.TenantId,
            Type = "statement.parse",
            Payload = statement.Id.ToString(),
            Status = OutboxStatus.Pending,
        });

        await db.SaveChangesAsync(ct);
        return ToDto(statement);
    }

    public async Task<IReadOnlyList<TransactionDto>> ListReviewQueueAsync(Guid? statementId, CancellationToken ct)
    {
        var query = db.Transactions.Where(t => !t.IsConfirmed);
        if (statementId is { } sid)
        {
            query = query.Where(t => t.StatementId == sid);
        }

        var staged = await query.OrderBy(t => t.Date).ToListAsync(ct);
        return staged.Select(ToDto).ToList();
    }

    public async Task<StatementDto> ConfirmStatementAsync(Guid statementId, CancellationToken ct)
    {
        var statement = await db.Statements.FirstOrDefaultAsync(s => s.Id == statementId, ct)
                        ?? throw new KeyNotFoundException($"Statement {statementId} not found.");

        var staged = await db.Transactions.Where(t => t.StatementId == statementId && !t.IsConfirmed).ToListAsync(ct);
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == statement.AccountId, ct);

        decimal delta = 0m;
        foreach (var transaction in staged)
        {
            transaction.IsConfirmed = true;
            delta += transaction.Direction == TransactionDirection.Credit ? transaction.Amount : -transaction.Amount;
        }

        if (account is not null)
        {
            account.CurrentBalance += delta;
        }

        statement.Status = StatementStatus.Confirmed;
        await db.SaveChangesAsync(ct);
        return ToDto(statement);
    }

    private static AccountDto ToDto(Account a) =>
        new(a.Id, a.Name, a.Type.ToString(), a.Institution, a.Currency, a.MaskedNumber, a.CurrentBalance);

    private static TransactionDto ToDto(Transaction t) =>
        new(t.Id, t.AccountId, t.StatementId, t.Date, t.Description, t.Amount, t.Currency, t.Direction.ToString(), t.IsConfirmed);

    private static StatementDto ToDto(Statement s) =>
        new(s.Id, s.AccountId, s.Source.ToString(), s.Status.ToString(), s.TransactionCount);
}
