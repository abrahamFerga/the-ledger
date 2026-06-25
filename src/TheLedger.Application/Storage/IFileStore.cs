namespace TheLedger.Application.Storage;

/// <summary>Opaque blob storage for statement bytes. Keyed by the statement id.</summary>
public interface IFileStore
{
    Task SaveAsync(string key, byte[] content, CancellationToken ct);
    Task<byte[]?> GetAsync(string key, CancellationToken ct);
}
