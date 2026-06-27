import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, request } from './client'
import { __setAuthProvider, type AuthProvider } from './auth'

/** A deterministic dev-auth provider for the transport tests. */
const fakeAuth: AuthProvider = {
  mode: 'dev',
  getAuthHeaders: () =>
    Promise.resolve({
      'X-Dev-Tenant': 'tenant-1',
      'X-Dev-User': 'user-1',
      'X-Dev-Role': 'Owner',
    }),
  getContext: () => ({ tenantId: 'tenant-1', userId: 'user-1', role: 'Owner' }),
}

function jsonResponse(body: unknown, init?: ResponseInit): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'content-type': 'application/json' },
    ...init,
  })
}

describe('api client transport', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    __setAuthProvider(fakeAuth)
    fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    __setAuthProvider(null)
    vi.unstubAllGlobals()
  })

  it('attaches dev-auth headers and hits the configured base URL', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse([{ id: 'a1' }]))

    await request('/api/v1/accounts')

    expect(fetchMock).toHaveBeenCalledTimes(1)
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toContain('/api/v1/accounts')
    const headers = new Headers(init.headers)
    expect(headers.get('X-Dev-Tenant')).toBe('tenant-1')
    expect(headers.get('X-Dev-User')).toBe('user-1')
    expect(headers.get('X-Dev-Role')).toBe('Owner')
    expect(init.method).toBe('GET')
  })

  it('does NOT add an Idempotency-Key on GET', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse([]))
    await request('/api/v1/ledger')
    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(new Headers(init.headers).has('Idempotency-Key')).toBe(false)
  })

  it('adds a uuid Idempotency-Key and JSON body on POST writes', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse({ id: 'acc-1', name: 'BBVA' }))

    await request('/api/v1/accounts', { method: 'POST', body: { name: 'BBVA' } })

    const [, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    const headers = new Headers(init.headers)
    const key = headers.get('Idempotency-Key')
    expect(key).toMatch(/^[0-9a-f-]{36}$/i)
    expect(headers.get('Content-Type')).toBe('application/json')
    expect(init.body).toBe(JSON.stringify({ name: 'BBVA' }))
  })

  it('parses RFC 7807 Problem Details into a typed ApiError', async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(
        JSON.stringify({
          type: 'https://httpstatuses.io/422',
          title: 'Validation failed',
          status: 422,
          detail: 'Amount must be positive',
          errors: { Amount: ['must be > 0'] },
        }),
        { status: 422, headers: { 'content-type': 'application/problem+json' } },
      ),
    )

    await expect(request('/api/v1/transactions', { method: 'POST', body: {} })).rejects.toMatchObject({
      name: 'ApiError',
      status: 422,
    })

    // Re-issue to inspect the thrown instance's display message.
    fetchMock.mockResolvedValueOnce(
      new Response(
        JSON.stringify({ title: 'Validation failed', detail: 'Amount must be positive', errors: { Amount: ['must be > 0'] } }),
        { status: 422, headers: { 'content-type': 'application/problem+json' } },
      ),
    )
    const error = await request('/api/v1/transactions', { method: 'POST', body: {} }).catch((e) => e)
    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).displayMessage).toContain('Validation failed')
    expect((error as ApiError).displayMessage).toContain('must be > 0')
  })

  it('returns undefined for 204 No Content', async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }))
    const result = await request<void>('/api/v1/goals/g1', { method: 'DELETE' })
    expect(result).toBeUndefined()
  })

  it('serializes query params, dropping undefined/null', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse([]))
    await request('/api/v1/ledger', { query: { confirmedOnly: false, accountId: undefined, categoryId: 'c1' } })
    const [url] = fetchMock.mock.calls[0] as [string]
    expect(url).toContain('confirmedOnly=false')
    expect(url).toContain('categoryId=c1')
    expect(url).not.toContain('accountId')
  })
})
