using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TheLedger.Application.Storage;

namespace TheLedger.Infrastructure.Azure;

/// <summary>Statement file store backed by an Azure Blob container (encrypted at rest).</summary>
public sealed class AzureBlobFileStore(BlobContainerClient container) : IFileStore
{
    public async Task SaveAsync(string key, byte[] content, CancellationToken ct)
    {
        var blob = container.GetBlobClient(key);
        using var stream = new MemoryStream(content);
        await blob.UploadAsync(stream, overwrite: true, ct);
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken ct)
    {
        var blob = container.GetBlobClient(key);
        if (!await blob.ExistsAsync(ct))
        {
            return null;
        }

        var response = await blob.DownloadContentAsync(ct);
        return response.Value.Content.ToArray();
    }
}

public static class AzureBlobExtensions
{
    /// <summary>
    /// Registers the Azure Blob file store when <c>Storage:Blob:ConnectionString</c> is configured;
    /// otherwise the DB-backed default store remains in place.
    /// </summary>
    public static IServiceCollection AddAzureBlobStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["Storage:Blob:ConnectionString"];
        var container = configuration["Storage:Blob:Container"] ?? "statements";
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return services;
        }

        services.AddSingleton<IFileStore>(new AzureBlobFileStore(new BlobContainerClient(connectionString, container)));
        return services;
    }
}
