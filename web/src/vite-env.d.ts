/// <reference types="vite/client" />
/// <reference types="vite-plugin-pwa/client" />

interface ImportMetaEnv {
  /** Base URL of the API. Defaults to the Aspire dev API; falls back to a relative `/api` when blank. */
  readonly VITE_API_BASE_URL?: string
  /** Auth strategy: `dev` attaches X-Dev-* headers; `msal` uses the (lazy, stubbed) Entra path. */
  readonly VITE_AUTH_MODE?: 'dev' | 'msal'
  /** Dev-auth household (tenant) id sent as X-Dev-Tenant when VITE_AUTH_MODE=dev. */
  readonly VITE_DEV_TENANT?: string
  /** Dev-auth user id sent as X-Dev-User when VITE_AUTH_MODE=dev. */
  readonly VITE_DEV_USER?: string
  /** Dev-auth role sent as X-Dev-Role when VITE_AUTH_MODE=dev. */
  readonly VITE_DEV_ROLE?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
