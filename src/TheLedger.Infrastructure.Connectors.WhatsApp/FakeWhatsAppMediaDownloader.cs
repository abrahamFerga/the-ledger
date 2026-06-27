using System.Text;

namespace TheLedger.Infrastructure.Connectors.WhatsApp;

/// <summary>
/// Dev/CI media downloader (feature #50): returns a deterministic receipt-shaped payload so the inbound
/// image → receipt-OCR path works end-to-end without Meta credentials. The bytes are the UTF-8 "receipt
/// text" the <c>FakeReceiptExtractor</c> reads, exactly like the receipt <c>.http</c> sample, so a fake
/// WhatsApp photo stages a transaction just as a real one would in prod.
/// </summary>
public sealed class FakeWhatsAppMediaDownloader : IWhatsAppMediaDownloader
{
    private const string SampleReceipt =
        "merchant: OXXO TIENDA 1234\n" +
        "date: 2026-03-10\n" +
        "total: 152.50\n" +
        "currency: MXN\n" +
        "confidence: 0.94\n";

    public Task<WhatsAppMedia?> DownloadAsync(string mediaId, CancellationToken ct) =>
        Task.FromResult<WhatsAppMedia?>(new WhatsAppMedia(Encoding.UTF8.GetBytes(SampleReceipt), "image/jpeg"));
}
