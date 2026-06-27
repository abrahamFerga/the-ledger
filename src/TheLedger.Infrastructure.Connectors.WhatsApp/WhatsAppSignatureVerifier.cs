using System.Security.Cryptography;
using System.Text;

namespace TheLedger.Infrastructure.Connectors.WhatsApp;

/// <summary>
/// Verifies Meta's <c>X-Hub-Signature-256</c> header against an HMAC-SHA256 of the <b>raw</b> request
/// body keyed by the app secret (feature #50, ADR-0010). This runs at the edge before any processing;
/// an unverified POST is rejected with 403 so only Meta-originated calls reach the inbound pipeline.
/// The compare is constant-time to avoid a timing side-channel.
/// </summary>
public static class WhatsAppSignatureVerifier
{
    private const string Prefix = "sha256=";

    /// <summary>Computes <c>sha256=&lt;hex&gt;</c> over <paramref name="body"/> using <paramref name="appSecret"/>.</summary>
    public static string Compute(ReadOnlySpan<byte> body, string appSecret)
    {
        var key = Encoding.UTF8.GetBytes(appSecret);
        Span<byte> hash = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(key, body, hash);
        return Prefix + Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// True when <paramref name="signatureHeader"/> (the value of <c>X-Hub-Signature-256</c>, of the form
    /// <c>sha256=&lt;hex&gt;</c>) matches the HMAC of <paramref name="body"/>. False for a missing,
    /// malformed, or tampered signature.
    /// </summary>
    public static bool IsValid(ReadOnlySpan<byte> body, string? signatureHeader, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader)
            || !signatureHeader.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(Compute(body, appSecret));
        var actual = Encoding.ASCII.GetBytes(signatureHeader);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
