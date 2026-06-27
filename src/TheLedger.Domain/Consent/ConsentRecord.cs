using TheLedger.Domain.Common;

namespace TheLedger.Domain.Consent;

/// <summary>Consent kinds captured for LFPDPPP / GDPR lawful basis.</summary>
public enum ConsentType
{
    /// <summary>Aviso de privacidad acceptance.</summary>
    PrivacyNotice,

    /// <summary>Opt-in to sending (redacted) transaction text to the LLM categorizer.</summary>
    LlmCategorization,

    /// <summary>
    /// Opt-in to capture and alerts over WhatsApp (feature #50): inbound messages from the user's
    /// mapped phone are processed, and outbound bill/anomaly/export-ready alerts may target WhatsApp.
    /// Without this consent a sender's number is never resolved to tenant data.
    /// </summary>
    WhatsAppChannel
}

/// <summary>Evidence that a user granted a specific consent version, for ARCO/GDPR.</summary>
public sealed class ConsentRecord : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public ConsentType Type { get; set; }
    public required string Version { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
}
