namespace TheLedger.Api.Options;

/// <summary>
/// OIDC settings (Entra External ID). When <see cref="Authority"/> is empty, the API falls back
/// to a header-driven Dev scheme so it is runnable/testable locally without a real IdP.
/// </summary>
public sealed class LedgerAuthOptions
{
    public const string SectionName = "Auth";

    public string? Authority { get; set; }
    public string? Audience { get; set; }
    public bool RequireHttpsMetadata { get; set; } = true;
}
