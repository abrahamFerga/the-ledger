import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QuickAddBar } from './QuickAddBar'
import { renderWithProviders } from '../../test/render'
import { __setAuthProvider, type AuthProvider } from '../../api/auth'

const fakeAuth: AuthProvider = {
  mode: 'dev',
  getAuthHeaders: () => Promise.resolve({ 'X-Dev-Tenant': 't', 'X-Dev-User': 'u', 'X-Dev-Role': 'Owner' }),
  getContext: () => ({ tenantId: 't', userId: 'u', role: 'Owner' }),
}

function jsonOk(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  })
}

const ACCOUNTS = [
  { id: 'acc-1', name: 'BBVA Débito', type: 'Checking', institution: 'BBVA', currency: 'MXN', maskedNumber: null, currentBalance: 0 },
]

// The quick-add endpoint returns the RAW draft: direction is a NUMBER (1 = Credit), not a string.
const RAW_DRAFT = {
  amount: 200,
  currency: 'MXN',
  date: '2026-06-26',
  direction: 0, // Debit
  merchant: 'OXXO',
  proposedCategoryId: null,
  confidence: 0.92,
}

describe('QuickAddBar — parse → confirm sheet → create (confirm-before-persist)', () => {
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

  function routeFetch(init: RequestInit | undefined, url: string): Response {
    const method = init?.method ?? 'GET'
    if (url.includes('/transactions/quick-add')) {
      return jsonOk(RAW_DRAFT)
    }
    if (url.includes('/accounts')) {
      return jsonOk(ACCOUNTS)
    }
    if (url.includes('/categories')) {
      return jsonOk([])
    }
    if (url.endsWith('/api/v1/transactions') && method === 'POST') {
      return jsonOk({
        id: 'tx-1',
        accountId: 'acc-1',
        statementId: null,
        date: '2026-06-26',
        description: 'OXXO',
        amount: 200,
        currency: 'MXN',
        direction: 'Debit',
        isConfirmed: true,
      })
    }
    return jsonOk([])
  }

  it('parses a phrase, surfaces an editable draft, and creates only on confirm', async () => {
    const user = userEvent.setup()
    fetchMock.mockImplementation((url: string, init?: RequestInit) =>
      Promise.resolve(routeFetch(init, url)),
    )

    renderWithProviders(<QuickAddBar />)

    const input = screen.getByLabelText(/quick add a transaction/i)
    await user.type(input, 'gasté 200 en el Oxxo ayer{Enter}')

    // The quick-add POST fired, but NO create POST yet (confirm-before-persist).
    await waitFor(() => {
      expect(
        fetchMock.mock.calls.some(([u]) => String(u).includes('/transactions/quick-add')),
      ).toBe(true)
    })
    expect(
      fetchMock.mock.calls.some(
        ([u, init]) =>
          String(u).endsWith('/api/v1/transactions') && (init as RequestInit)?.method === 'POST',
      ),
    ).toBe(false)

    // The confirm sheet opens with the parsed draft pre-filled.
    const sheet = await screen.findByRole('dialog')
    expect(within(sheet).getByText(/confirm transaction/i)).toBeInTheDocument()
    expect(within(sheet).getByDisplayValue('OXXO')).toBeInTheDocument()
    expect(within(sheet).getByDisplayValue('200')).toBeInTheDocument()
    // Numeric direction 0 normalized to the "Expense (money out)" string option.
    expect((within(sheet).getByLabelText('Direction') as HTMLSelectElement).value).toBe('Debit')
    // Confidence is surfaced.
    expect(within(sheet).getByText(/92%/)).toBeInTheDocument()

    // Confirm → create POST fires with the mapped manual-transaction payload + idempotency key.
    await user.click(within(sheet).getByRole('button', { name: /add to ledger/i }))

    await waitFor(() => {
      const post = fetchMock.mock.calls.find(
        ([u, init]) =>
          String(u).endsWith('/api/v1/transactions') && (init as RequestInit)?.method === 'POST',
      )
      expect(post).toBeTruthy()
    })

    const post = fetchMock.mock.calls.find(
      ([u, init]) =>
        String(u).endsWith('/api/v1/transactions') && (init as RequestInit)?.method === 'POST',
    )!
    const init = post[1] as RequestInit
    const body = JSON.parse(init.body as string)
    expect(body).toMatchObject({
      accountId: 'acc-1',
      description: 'OXXO',
      amount: 200,
      direction: 'Debit',
      date: '2026-06-26',
    })
    expect(new Headers(init.headers).get('Idempotency-Key')).toMatch(/^[0-9a-f-]{36}$/i)
  })

  it('does not parse an empty phrase', async () => {
    const user = userEvent.setup()
    fetchMock.mockImplementation((url: string, init?: RequestInit) =>
      Promise.resolve(routeFetch(init, url)),
    )

    renderWithProviders(<QuickAddBar />)
    // Submit with empty input (click the disabled-by-design submit; nothing should fire).
    await user.click(screen.getByRole('button', { name: /parse and confirm/i }))

    expect(
      fetchMock.mock.calls.some(([u]) => String(u).includes('/transactions/quick-add')),
    ).toBe(false)
  })
})
