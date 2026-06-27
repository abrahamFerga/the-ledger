import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Integrations } from './Integrations'
import { renderWithProviders } from '../test/render'
import { __setAuthProvider, type AuthProvider } from '../api/auth'

const fakeAuth: AuthProvider = {
  mode: 'dev',
  getAuthHeaders: () => Promise.resolve({ 'X-Dev-Tenant': 't', 'X-Dev-User': 'u', 'X-Dev-Role': 'Owner' }),
  getContext: () => ({ tenantId: 't', userId: 'u', role: 'Owner' }),
}

function jsonOk(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'content-type': 'application/json' },
  })
}

describe('Integrations — WhatsApp opt-in', () => {
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

  it('shows "Not connected" then opts a number in via POST', async () => {
    const user = userEvent.setup()
    fetchMock.mockImplementation((url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      if (url.includes('/connectors/whatsapp/opt-in') && method === 'POST') {
        return Promise.resolve(
          jsonOk({ id: 'c1', phoneNumber: '5215512345678', userId: 'u', defaultAccountId: null, optedIn: true }),
        )
      }
      if (url.includes('/connectors/whatsapp/opt-in')) {
        return Promise.resolve(jsonOk([]))
      }
      if (url.includes('/accounts')) {
        return Promise.resolve(jsonOk([]))
      }
      return Promise.resolve(jsonOk([]))
    })

    renderWithProviders(<Integrations />)

    expect(await screen.findByText(/not connected/i)).toBeInTheDocument()

    await user.type(screen.getByLabelText(/whatsapp number/i), '5215512345678')
    await user.click(screen.getByRole('button', { name: /connect whatsapp/i }))

    await waitFor(() => {
      const post = fetchMock.mock.calls.find(
        ([u, i]) =>
          String(u).includes('/connectors/whatsapp/opt-in') && (i as RequestInit)?.method === 'POST',
      )
      expect(post).toBeTruthy()
      const body = JSON.parse((post![1] as RequestInit).body as string)
      expect(body).toMatchObject({ phoneNumber: '5215512345678' })
    })
  })

  it('validates the phone format before posting', async () => {
    const user = userEvent.setup()
    fetchMock.mockImplementation((url: string) =>
      Promise.resolve(jsonOk(url.includes('opt-in') ? [] : [])),
    )

    renderWithProviders(<Integrations />)
    await screen.findByText(/not connected/i)

    await user.type(screen.getByLabelText(/whatsapp number/i), 'abc')
    await user.click(screen.getByRole('button', { name: /connect whatsapp/i }))

    expect(await screen.findByText(/international format/i)).toBeInTheDocument()
    expect(
      fetchMock.mock.calls.some(
        ([u, i]) =>
          String(u).includes('/connectors/whatsapp/opt-in') && (i as RequestInit)?.method === 'POST',
      ),
    ).toBe(false)
  })

  it('lists a connected number and disconnects it (DELETE, optimistic)', async () => {
    const user = userEvent.setup()
    // Stateful opt-in list: the DELETE removes the number, so the post-mutation refetch returns [].
    let removed = false
    fetchMock.mockImplementation((url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      if (url.includes('/connectors/whatsapp/opt-in/c1') && method === 'DELETE') {
        removed = true
        return Promise.resolve(new Response(null, { status: 204 }))
      }
      if (url.includes('/connectors/whatsapp/opt-in')) {
        return Promise.resolve(
          jsonOk(
            removed
              ? []
              : [{ id: 'c1', phoneNumber: '5215512345678', userId: 'u', defaultAccountId: null, optedIn: true }],
          ),
        )
      }
      return Promise.resolve(jsonOk([]))
    })

    renderWithProviders(<Integrations />)

    expect(await screen.findByText('+5215512345678')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /disconnect 5215512345678/i }))

    await waitFor(() => {
      const del = fetchMock.mock.calls.find(
        ([u, i]) =>
          String(u).includes('/connectors/whatsapp/opt-in/c1') &&
          (i as RequestInit)?.method === 'DELETE',
      )
      expect(del).toBeTruthy()
    })
    // Optimistic removal.
    await waitFor(() => expect(screen.queryByText('+5215512345678')).not.toBeInTheDocument())
  })
})
