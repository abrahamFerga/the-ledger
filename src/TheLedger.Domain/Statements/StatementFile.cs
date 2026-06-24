using TheLedger.Domain.Common;

namespace TheLedger.Domain.Statements;

/// <summary>
/// Raw bytes of an uploaded statement, kept until parsed. In production this is Azure Blob
/// (FileRef on <see cref="Statement"/>); for the bootstrap the bytes live here so the worker can
/// parse without external storage. Tenant-owned and never returned to clients.
/// </summary>
public sealed class StatementFile : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid StatementId { get; set; }
    public required byte[] Content { get; set; }
    public string ContentType { get; set; } = "application/pdf";
}
