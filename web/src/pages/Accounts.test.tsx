import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Accounts } from './Accounts'
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

describe('Accounts page — list + add form (Form pattern)', () => {
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

  it('renders accounts returned by the API', async () => {
    fetchMock.mockResolvedValue(
      jsonOk([
        {
          id: 'a1',
          name: 'BBVA Débito',
          type: 'Checking',
          institution: 'BBVA',
          currency: 'MXN',
          maskedNumber: '•• 5678',
          currentBalance: 1234.5,
        },
      ]),
    )

    renderWithProviders(<Accounts />)

    expect(await screen.findByText('BBVA Débito')).toBeInTheDocument()
    expect(screen.getByText('Checking')).toBeInTheDocument()
  })

  it('validates required fields before submitting', async () => {
    const user = userEvent.setup()
    fetchMock.mockResolvedValue(jsonOk([]))

    renderWithProviders(<Accounts />)
    // Wait for the initial accounts query to settle (empty state).
    await screen.findByText('No accounts yet')

    await user.click(screen.getByRole('button', { name: /add account/i }))
    const dialog = await screen.findByRole('dialog')

    // Clear the name and submit → zod validation should block the POST.
    await user.clear(within(dialog).getByLabelText('Name'))
    await user.click(within(dialog).getByRole('button', { name: /add account/i }))

    expect(await within(dialog).findByText('Name is required')).toBeInTheDocument()
    // Only the initial GET happened — no POST fired.
    expect(fetchMock.mock.calls.every(([, init]) => (init as RequestInit)?.method !== 'POST')).toBe(true)
  })

  it('submits a mapped CreateAccountRequest payload', async () => {
    const user = userEvent.setup()
    fetchMock.mockImplementation((_url: string, init?: RequestInit) => {
      if (init?.method === 'POST') {
        return Promise.resolve(
          jsonOk({
            id: 'a2',
            name: 'Nu',
            type: 'Card',
            institution: 'Nu',
            currency: 'MXN',
            maskedNumber: null,
            currentBalance: 0,
          }),
        )
      }
      return Promise.resolve(jsonOk([]))
    })

    renderWithProviders(<Accounts />)
    await screen.findByText('No accounts yet')

    await user.click(screen.getByRole('button', { name: /add account/i }))
    const dialog = await screen.findByRole('dialog')

    await user.type(within(dialog).getByLabelText('Name'), 'Nu')
    await user.selectOptions(within(dialog).getByLabelText('Type'), 'Card')
    await user.click(within(dialog).getByRole('button', { name: /add account/i }))

    await waitFor(() => {
      const post = fetchMock.mock.calls.find(([, init]) => (init as RequestInit)?.method === 'POST')
      expect(post).toBeTruthy()
    })

    const post = fetchMock.mock.calls.find(([, init]) => (init as RequestInit)?.method === 'POST')!
    const [url, init] = post as [string, RequestInit]
    expect(url).toContain('/api/v1/accounts')
    const body = JSON.parse(init.body as string)
    expect(body).toMatchObject({ name: 'Nu', type: 'Card', currency: 'MXN' })
    // Write carries an idempotency key.
    expect(new Headers(init.headers).get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/i)
  })
})
