using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TheLedger.Infrastructure.Connectors.WhatsApp;

/// <summary>
/// Real media downloader over the Meta Graph API (feature #50, ADR-0010): resolves the media id to a
/// short-lived URL, then GETs the bytes (both calls bearer-authenticated). Registered only when real
/// credentials are present; the resilience handler on the named client retries transient failures.
/// </summary>
public sealed class MetaWhatsAppMediaDownloader(
    HttpClient http,
    IOptions<WhatsAppOptions> options,
    ILogger<MetaWhatsAppMediaDownloader> logger) : IWhatsAppMediaDownloader
{
    private readonly WhatsAppOptions _options = options.Value;

    public async Task<WhatsAppMedia?> DownloadAsync(string mediaId, CancellationToken ct)
    {
        var metaUrl = $"{_options.GraphApiBaseUrl.TrimEnd('/')}/{mediaId}";
        using var metaRequest = new HttpRequestMessage(HttpMethod.Get, metaUrl);
        metaRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        using var metaResponse = await http.SendAsync(metaRequest, ct);
        metaResponse.EnsureSuccessStatusCode();

        var meta = await metaResponse.Content.ReadFromJsonAsync<MediaMetadata>(ct);
        if (meta?.Url is null)
        {
            logger.LogWarning("WhatsApp media {MediaId} had no download url", mediaId);
            return null;
        }

        using var bytesRequest = new HttpRequestMessage(HttpMethod.Get, meta.Url);
        bytesRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        using var bytesResponse = await http.SendAsync(bytesRequest, ct);
        bytesResponse.EnsureSuccessStatusCode();

        var content = await bytesResponse.Content.ReadAsByteArrayAsync(ct);
        return new WhatsAppMedia(content, meta.MimeType ?? "image/jpeg");
    }

    private sealed record MediaMetadata(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("mime_type")] string? MimeType);
}
