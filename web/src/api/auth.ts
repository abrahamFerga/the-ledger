/**
 * Auth seam. The SPA talks to the API through an `AuthProvider`, so the transport layer never knows
 * whether it's authenticating with the local Dev scheme or production Entra (MSAL).
 *
 * - `dev` (default): attaches the `X-Dev-Tenant` / `X-Dev-User` / `X-Dev-Role` headers the API's
 *   `DevAuthHandler` reads (see `src/TheLedger.Api/Auth/DevAuthHandler.cs`). No cloud config needed.
 * - `msal`: production path against Entra External ID (auth code + PKCE). The MSAL dependency is
 *   loaded lazily so the app builds and runs in dev mode without any Entra configuration; until it's
 *   fully wired it throws a clear error rather than silently degrading.
 *
 * Mode is selected by `VITE_AUTH_MODE` (`dev` | `msal`), defaulting to `dev`.
 */

export interface AuthContext {
  /** Stable identifier of the signed-in user (for query keys, display). */
  userId: string
  /** Tenant / household id the user is acting within. */
  tenantId: string
  /** Coarse role used for client-side affordance gating (server is the source of truth). */
  role: string
}

export interface AuthProvider {
  readonly mode: 'dev' | 'msal'
  /** Headers to attach to every API request (auth material). */
  getAuthHeaders(): Promise<Record<string, string>>
  /** The current identity, or null when unauthenticated. */
  getContext(): AuthContext | null
}

// Well-known dev identity matching http/the-ledger.http so the SPA works against a freshly
// provisioned local household out of the box.
const DEV_DEFAULTS = {
  tenant: '11111111-1111-1111-1111-111111111111',
  user: '22222222-2222-2222-2222-222222222222',
  role: 'Owner',
} as const

class DevAuthProvider implements AuthProvider {
  readonly mode = 'dev' as const
  private readonly ctx: AuthContext

  constructor() {
    this.ctx = {
      tenantId: import.meta.env.VITE_DEV_TENANT ?? DEV_DEFAULTS.tenant,
      userId: import.meta.env.VITE_DEV_USER ?? DEV_DEFAULTS.user,
      role: import.meta.env.VITE_DEV_ROLE ?? DEV_DEFAULTS.role,
    }
  }

  getAuthHeaders(): Promise<Record<string, string>> {
    return Promise.resolve({
      'X-Dev-Tenant': this.ctx.tenantId,
      'X-Dev-User': this.ctx.userId,
      'X-Dev-Role': this.ctx.role,
    })
  }

  getContext(): AuthContext | null {
    return this.ctx
  }
}

/**
 * Production seam. Intentionally a stub: it carries no `@azure/msal-*` import so dev builds stay
 * lean and cloud-config-free. Wiring it up means adding MSAL, resolving the account, and returning a
 * bearer token here — the call sites (transport, context) are already in place.
 */
class MsalAuthProvider implements AuthProvider {
  readonly mode = 'msal' as const

  getAuthHeaders(): Promise<Record<string, string>> {
    throw new Error(
      'MSAL auth is not configured in this build. Set VITE_AUTH_MODE=dev for local development, ' +
        'or finish wiring @azure/msal-browser in src/api/auth.ts for production.',
    )
  }

  getContext(): AuthContext | null {
    return null
  }
}

let provider: AuthProvider | null = null

/** The process-wide auth provider, selected from VITE_AUTH_MODE (memoized). */
export function getAuthProvider(): AuthProvider {
  if (provider) {
    return provider
  }
  provider = import.meta.env.VITE_AUTH_MODE === 'msal' ? new MsalAuthProvider() : new DevAuthProvider()
  return provider
}

/** Test seam: override the provider (and reset with `null`). */
export function __setAuthProvider(p: AuthProvider | null): void {
  provider = p
}
