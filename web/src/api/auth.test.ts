import { afterEach, describe, expect, it } from 'vitest'
import { __setAuthProvider, getAuthProvider } from './auth'

describe('auth provider seam', () => {
  afterEach(() => __setAuthProvider(null))

  it('defaults to the dev provider and emits X-Dev-* headers', async () => {
    // VITE_AUTH_MODE is unset in the test env → dev mode.
    const provider = getAuthProvider()
    expect(provider.mode).toBe('dev')

    const headers = await provider.getAuthHeaders()
    expect(headers['X-Dev-Tenant']).toMatch(/^[0-9a-f-]{36}$/i)
    expect(headers['X-Dev-User']).toMatch(/^[0-9a-f-]{36}$/i)
    expect(headers['X-Dev-Role']).toBe('Owner')

    const ctx = provider.getContext()
    expect(ctx).not.toBeNull()
    expect(ctx?.role).toBe('Owner')
  })

  it('memoizes the provider instance', () => {
    expect(getAuthProvider()).toBe(getAuthProvider())
  })
})
