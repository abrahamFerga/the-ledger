using TheLedger.Domain.Common;

namespace TheLedger.Domain.Consent;

/// <summary>Consent kinds captured for LFPDPPP / GDPR lawful basis.</summary>
public enum ConsentType
{
    /// <summary>Aviso de privacidad acceptance.</summary>
    PrivacyNotice,

    /// <summary>Opt-in to sending (redacted) transaction text to the LLM categorizer.</summary>
    LlmCategorization
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
