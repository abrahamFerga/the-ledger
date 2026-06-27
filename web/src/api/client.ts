/**
 * Typed HTTP transport for the API. Centralizes:
 *  - base URL (from VITE_API_BASE_URL; defaults to the Aspire dev API, falling back to relative `/api`)
 *  - auth headers from the AuthProvider seam (dev `X-Dev-*` headers or MSAL bearer)
 *  - an `Idempotency-Key` (uuid) on every non-GET write, per the API guardrail
 *  - RFC 7807 Problem Details parsing into a typed `ApiError`
 *
 * Endpoints are versioned under `/api/v1`; callers pass paths relative to the base (e.g. `/accounts`).
 */
import { v4 as uuid } from 'uuid'
import { getAuthProvider } from './auth'

/** RFC 7807 Problem Details payload. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  errors?: Record<string, string[]>
}

/** Error thrown for any non-2xx response, carrying parsed Problem Details when present. */
export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null

  constructor(status: number, message: string, problem: ProblemDetails | null) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  /** A human-readable, toast-friendly message: Problem Details title/detail, else the status. */
  get displayMessage(): string {
    if (this.problem) {
      const validation = this.problem.errors
        ? Object.values(this.problem.errors).flat().join(' ')
        : ''
      return [this.problem.title, this.problem.detail, validation].filter(Boolean).join(' — ') || this.message
    }
    return this.message
  }
}

/** Resolve the API base. Trailing slashes are trimmed; empty/undefined means same-origin relative. */
function resolveBaseUrl(): string {
  const raw = import.meta.env.VITE_API_BASE_URL
  // Default to the Aspire/local dev API. A relative '' makes requests same-origin (prod behind a
  // reverse proxy that serves the SPA and the API under one host).
  const base = raw ?? 'http://localhost:5016'
  return base.replace(/\/+$/, '')
}

const MUTATION_METHODS = new Set(['POST', 'PUT', 'PATCH', 'DELETE'])

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  /** JSON body; serialized automatically. Omit for GET. */
  body?: unknown
  /** Raw body (e.g. FormData) — bypasses JSON serialization. */
  rawBody?: BodyInit
  /** Query string params; undefined/null values are dropped. */
  query?: Record<string, string | number | boolean | undefined | null>
  signal?: AbortSignal
  /** Override the idempotency key (defaults to a fresh uuid on writes). */
  idempotencyKey?: string
}

function buildUrl(path: string, query?: RequestOptions['query']): string {
  const base = resolveBaseUrl()
  const url = `${base}${path.startsWith('/') ? path : `/${path}`}`
  if (!query) {
    return url
  }
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null) {
      params.append(key, String(value))
    }
  }
  const qs = params.toString()
  return qs ? `${url}?${qs}` : url
}

async function parseProblem(response: Response): Promise<ProblemDetails | null> {
  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('json')) {
    return null
  }
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return null
  }
}

/** Core request. Returns parsed JSON (or `undefined` for empty 204 responses). */
export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const method = options.method ?? 'GET'
  const headers = new Headers({ Accept: 'application/json' })

  const auth = await getAuthProvider().getAuthHeaders()
  for (const [k, v] of Object.entries(auth)) {
    headers.set(k, v)
  }

  let body: BodyInit | undefined
  if (options.rawBody !== undefined) {
    body = options.rawBody
  } else if (options.body !== undefined) {
    headers.set('Content-Type', 'application/json')
    body = JSON.stringify(options.body)
  }

  if (MUTATION_METHODS.has(method)) {
    headers.set('Idempotency-Key', options.idempotencyKey ?? uuid())
  }

  const response = await fetch(buildUrl(path, options.query), {
    method,
    headers,
    body,
    signal: options.signal,
  })

  if (!response.ok) {
    const problem = await parseProblem(response)
    const message = problem?.title ?? `Request failed (${response.status})`
    throw new ApiError(response.status, message, problem)
  }

  if (response.status === 204) {
    return undefined as T
  }

  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.includes('json')) {
    return (await response.text()) as unknown as T
  }
  return (await response.json()) as T
}

/** Fetch a binary resource (e.g. CSV export) as a Blob, with auth + Problem Details handling. */
export async function requestBlob(path: string, options: RequestOptions = {}): Promise<Blob> {
  const headers = new Headers()
  const auth = await getAuthProvider().getAuthHeaders()
  for (const [k, v] of Object.entries(auth)) {
    headers.set(k, v)
  }
  const response = await fetch(buildUrl(path, options.query), {
    method: options.method ?? 'GET',
    headers,
    signal: options.signal,
  })
  if (!response.ok) {
    const problem = await parseProblem(response)
    throw new ApiError(response.status, problem?.title ?? `Request failed (${response.status})`, problem)
  }
  return response.blob()
}
