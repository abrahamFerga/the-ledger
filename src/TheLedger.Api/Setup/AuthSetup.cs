using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TheLedger.Api.Auth;
using TheLedger.Api.Options;
using TheLedger.Application.Authorization;

namespace TheLedger.Api.Setup;

public static class AuthSetup
{
    /// <summary>
    /// Wires authentication (real JWT bearer against the configured OIDC authority, or the Dev
    /// scheme locally) and registers one authorization policy per <see cref="RolePolicyMap"/> entry.
    /// </summary>
    public static void AddLedgerAuth(this WebApplicationBuilder builder)
    {
        var auth = builder.Configuration.GetSection(LedgerAuthOptions.SectionName).Get<LedgerAuthOptions>()
                   ?? new LedgerAuthOptions();
        var useRealIdp = !string.IsNullOrWhiteSpace(auth.Authority);

        var authn = builder.Services.AddAuthentication(useRealIdp
            ? JwtBearerDefaults.AuthenticationScheme
            : DevAuthHandler.SchemeName);

        if (useRealIdp)
        {
            authn.AddJwtBearer(o =>
            {
                o.Authority = auth.Authority;
                o.Audience = auth.Audience;
                o.RequireHttpsMetadata = auth.RequireHttpsMetadata;
                o.TokenValidationParameters.RoleClaimType = "role";
                o.TokenValidationParameters.NameClaimType = "sub";
            });
        }
        else
        {
            authn.AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevAuthHandler.SchemeName, _ => { });
        }

        var authz = builder.Services.AddAuthorizationBuilder();
        foreach (var (policy, roles) in RolePolicyMap.Default)
        {
            authz.AddPolicy(policy, p => p.RequireAuthenticatedUser().RequireRole(roles));
        }
    }
}
