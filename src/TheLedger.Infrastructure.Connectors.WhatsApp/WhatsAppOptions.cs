namespace TheLedger.Infrastructure.Connectors.WhatsApp;

/// <summary>
/// Strongly-typed WhatsApp connector config (feature #50, ADR-0010). Bound from the <c>WhatsApp</c>
/// section and validated at startup via <c>IOptions&lt;T&gt;</c> + <c>ValidateOnStart()</c>. Secrets
/// (<see cref="AppSecret"/>, <see cref="VerifyToken"/>, <see cref="AccessToken"/>) come from the cloud
/// secret store / config — never hardcoded. When no <see cref="AccessToken"/> is configured the
/// connector runs in dev/fake mode: the verify-token + HMAC checks still apply (so the webhook is
/// exercisable) but outbound sends go to the deterministic fake instead of Meta, so CI needs no real
/// Meta credentials (mirrors the email no-op/ACS and Document-Intelligence dev fallbacks).
/// </summary>
public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>
    /// Token echoed back on the GET subscription challenge. Defaults to a dev value so the webhook is
    /// exercisable locally; set a real secret in any shared environment.
    /// </summary>
    public string VerifyToken { get; set; } = "dev-verify-token";

    /// <summary>
    /// Meta app secret used to HMAC-SHA256 the raw request body (<c>X-Hub-Signature-256</c>). Defaults
    /// to a dev value so the signature path is testable offline; a real secret is required in prod.
    /// </summary>
    public string AppSecret { get; set; } = "dev-app-secret";

    /// <summary>The WhatsApp phone-number id outbound messages are sent from. From config/Key Vault.</summary>
    public string? PhoneNumberId { get; set; }

    /// <summary>
    /// Meta Graph API access token for outbound send + media download. When empty the connector uses the
    /// deterministic fake sender and never calls Meta (dev/CI mode).
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>Meta Graph API base, e.g. <c>https://graph.facebook.com/v21.0</c>. Overridable for tests.</summary>
    public string GraphApiBaseUrl { get; set; } = "https://graph.facebook.com/v21.0";

    /// <summary>True when real Meta credentials are present and the live sender/media download is used.</summary>
    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(AccessToken) && !string.IsNullOrWhiteSpace(PhoneNumberId);
}
