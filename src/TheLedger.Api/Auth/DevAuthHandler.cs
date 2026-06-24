using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace TheLedger.Api.Auth;

/// <summary>
/// Local-only auth scheme used when no real OIDC authority is configured. Reads
/// <c>X-Dev-Tenant</c>, <c>X-Dev-User</c>, and <c>X-Dev-Role</c> headers to simulate a signed-in
/// household member, so the API can be exercised end-to-end without Entra. Never registered when
/// <see cref="Options.LedgerAuthOptions.Authority"/> is set.
/// </summary>
public sealed class DevAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Dev";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var tenant = Request.Headers["X-Dev-Tenant"].FirstOrDefault();
        var user = Request.Headers["X-Dev-User"].FirstOrDefault();
        var role = Request.Headers["X-Dev-Role"].FirstOrDefault() ?? "Owner";

        if (string.IsNullOrWhiteSpace(tenant) || string.IsNullOrWhiteSpace(user))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Claim[] claims =
        [
            new("tenant_id", tenant),
            new(ClaimTypes.NameIdentifier, user),
            new("sub", user),
            new(ClaimTypes.Role, role),
            new("role", role)
        ];

        var identity = new ClaimsIdentity(claims, SchemeName, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
