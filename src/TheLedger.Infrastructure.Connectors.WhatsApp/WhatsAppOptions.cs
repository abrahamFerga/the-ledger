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
    /// The in-source dev verify token. Usable in Development only — startup fails closed if it leaks into
    /// any non-Development environment (see <see cref="Validate"/>), so a misconfigured prod deploy can
    /// never accept the GET challenge with a publicly-known token.
    /// </summary>
    internal const string DevVerifyToken = "dev-verify-token";

    /// <summary>
    /// The in-source dev app secret. Usable in Development only — startup fails closed if it leaks into any
    /// non-Development environment (see <see cref="Validate"/>), so a misconfigured prod deploy can never
    /// validate a forged <c>X-Hub-Signature-256</c> computed with this publicly-known secret.
    /// </summary>
    internal const string DevAppSecret = "dev-app-secret";

    /// <summary>
    /// Token echoed back on the GET subscription challenge. Defaults to a dev value so the webhook is
    /// exercisable locally; a real secret is required outside Development (enforced by <see cref="Validate"/>).
    /// </summary>
    public string VerifyToken { get; set; } = DevVerifyToken;

    /// <summary>
    /// Meta app secret used to HMAC-SHA256 the raw request body (<c>X-Hub-Signature-256</c>). Defaults
    /// to a dev value so the signature path is testable offline; a real secret is required outside
    /// Development (enforced by <see cref="Validate"/>).
    /// </summary>
    public string AppSecret { get; set; } = DevAppSecret;

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

    /// <summary>
    /// Env-aware startup validation (fail-closed). Mirrors the connector "HasCredentials / dev-fallback"
    /// idiom: the in-source dev defaults stay usable in Development (so local + CI run with no Meta config)
    /// but are <b>rejected outside Development</b> — a prod deploy that forgets to set a real
    /// <c>WhatsApp:AppSecret</c> / <c>WhatsApp:VerifyToken</c> fails startup loudly instead of silently
    /// accepting forged webhooks signed with the publicly-known dev secret. Returns the first failure
    /// message, or <c>null</c> when valid.
    /// </summary>
    public string? Validate(bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(VerifyToken))
        {
            return "WhatsApp:VerifyToken is required.";
        }

        if (string.IsNullOrWhiteSpace(AppSecret))
        {
            return "WhatsApp:AppSecret is required.";
        }

        if (!isDevelopment
            && (string.Equals(AppSecret, DevAppSecret, StringComparison.Ordinal)
                || string.Equals(VerifyToken, DevVerifyToken, StringComparison.Ordinal)))
        {
            return "Real WhatsApp:AppSecret and WhatsApp:VerifyToken are required outside Development.";
        }

        if (HasCredentials && string.IsNullOrWhiteSpace(GraphApiBaseUrl))
        {
            return "WhatsApp:GraphApiBaseUrl is required when credentials are configured.";
        }

        return null;
    }
}
