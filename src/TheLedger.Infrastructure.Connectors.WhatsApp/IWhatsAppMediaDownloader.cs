namespace TheLedger.Infrastructure.Connectors.WhatsApp;

/// <summary>Downloaded WhatsApp media bytes plus their content type.</summary>
public sealed record WhatsAppMedia(byte[] Content, string ContentType);

/// <summary>
/// Fetches the bytes of an inbound WhatsApp image by its Meta media id (feature #50). In real mode this
/// is a two-hop Graph API call (resolve the media url, then GET the bytes); in dev/CI a fake returns a
/// small placeholder so the inbound image path is exercisable without Meta credentials.
/// </summary>
public interface IWhatsAppMediaDownloader
{
    Task<WhatsAppMedia?> DownloadAsync(string mediaId, CancellationToken ct);
}
