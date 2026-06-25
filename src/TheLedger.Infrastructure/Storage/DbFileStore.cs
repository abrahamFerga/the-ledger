using Microsoft.EntityFrameworkCore;
using TheLedger.Application.Storage;
using TheLedger.Domain.Statements;
using TheLedger.Infrastructure.Persistence;

namespace TheLedger.Infrastructure.Storage;

/// <summary>Default file store: keeps statement bytes in the DB (StatementFile), keyed by statement id.</summary>
public sealed class DbFileStore(LedgerDbContext db) : IFileStore
{
    public async Task SaveAsync(string key, byte[] content, CancellationToken ct)
    {
        var statementId = Guid.Parse(key);
        var existing = await db.StatementFiles.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.StatementId == statementId, ct);
        if (existing is null)
        {
            db.StatementFiles.Add(new StatementFile { Id = Guid.CreateVersion7(), StatementId = statementId, Content = content });
        }
        else
        {
            existing.Content = content;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken ct)
    {
        if (!Guid.TryParse(key, out var statementId))
        {
            return null;
        }

        return await db.StatementFiles.IgnoreQueryFilters()
            .Where(f => f.StatementId == statementId)
            .Select(f => f.Content)
            .FirstOrDefaultAsync(ct);
    }
}
