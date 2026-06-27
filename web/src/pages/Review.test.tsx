import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Review } from './Review'
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

// A statement-grouped staged txn (confirmable as a batch) + a statement-less AI-capture staged txn.
const STAGED = [
  {
    id: 'tx-stmt-1',
    accountId: 'acc-1',
    statementId: 'stmt-1',
    date: '2026-01-05',
    description: 'WALMART',
    amount: 300,
    currency: 'MXN',
    direction: 'Debit',
    isConfirmed: false,
  },
  {
    id: 'tx-cap-1',
    accountId: 'acc-1',
    statementId: null,
    date: '2026-01-06',
    description: 'OXXO (receipt)',
    amount: 152.5,
    currency: 'MXN',
    direction: 'Debit',
    isConfirmed: false,
  },
]

describe('Review queue — grouped staging + statement-batch confirm', () => {
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

  function route(url: string, init?: RequestInit): Response {
    const method = init?.method ?? 'GET'
    if (url.includes('/transactions/review')) {
      return jsonOk(STAGED)
    }
    if (url.includes('/categories')) {
      return jsonOk([{ id: 'cat-1', name: 'Groceries', kind: 'Expense', isSystem: true }])
    }
    if (url.includes('/statements/stmt-1/confirm') && method === 'POST') {
      return jsonOk({ id: 'stmt-1', accountId: 'acc-1', source: 'Csv', status: 'Confirmed', transactionCount: 1 })
    }
    return jsonOk([])
  }

  it('renders staged items grouped by source (statement batch + AI capture)', async () => {
    fetchMock.mockImplementation((url: string, init?: RequestInit) => Promise.resolve(route(url, init)))
    renderWithProviders(<Review />)

    expect(await screen.findByText('WALMART')).toBeInTheDocument()
    expect(screen.getByText('OXXO (receipt)')).toBeInTheDocument()
    // The statement group exposes a batch-confirm button; the AI-capture group does not.
    expect(screen.getByRole('button', { name: /confirm all/i })).toBeInTheDocument()
    expect(screen.getByText(/from ai capture/i)).toBeInTheDocument()
  })

  it('confirms a statement batch and optimistically drops its rows', async () => {
    const user = userEvent.setup()
    // Stateful review endpoint: once the statement is confirmed, its rows leave the queue (as the
    // real backend behaves), leaving only the statement-less AI-capture row.
    let confirmed = false
    fetchMock.mockImplementation((url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      if (url.includes('/transactions/review')) {
        return Promise.resolve(jsonOk(confirmed ? STAGED.filter((t) => t.statementId === null) : STAGED))
      }
      if (url.includes('/statements/stmt-1/confirm') && method === 'POST') {
        confirmed = true
        return Promise.resolve(
          jsonOk({ id: 'stmt-1', accountId: 'acc-1', source: 'Csv', status: 'Confirmed', transactionCount: 1 }),
        )
      }
      return Promise.resolve(route(url, init))
    })
    renderWithProviders(<Review />)

    await screen.findByText('WALMART')
    await user.click(screen.getByRole('button', { name: /confirm all/i }))

    // The statement confirm POST fired against the right route.
    await waitFor(() => {
      const post = fetchMock.mock.calls.find(
        ([u, i]) =>
          String(u).includes('/statements/stmt-1/confirm') && (i as RequestInit)?.method === 'POST',
      )
      expect(post).toBeTruthy()
    })

    // The statement row is gone (optimistically, then confirmed by the refetch); the AI-capture row remains.
    await waitFor(() => expect(screen.queryByText('WALMART')).not.toBeInTheDocument())
    expect(screen.getByText('OXXO (receipt)')).toBeInTheDocument()
  })

  it('inline-edits a staged description via PATCH', async () => {
    const user = userEvent.setup()
    fetchMock.mockImplementation((url: string, init?: RequestInit) => {
      if (url.includes('/transactions/tx-cap-1') && init?.method === 'PATCH') {
        return Promise.resolve(jsonOk({ ...STAGED[1], description: 'OXXO Centro' }))
      }
      return Promise.resolve(route(url, init))
    })
    renderWithProviders(<Review />)

    await user.click(await screen.findByRole('button', { name: /OXXO \(receipt\)/i }))
    const editField = await screen.findByLabelText('Edit description')
    await user.clear(editField)
    await user.type(editField, 'OXXO Centro{Enter}')

    await waitFor(() => {
      const patch = fetchMock.mock.calls.find(
        ([u, i]) =>
          String(u).includes('/transactions/tx-cap-1') && (i as RequestInit)?.method === 'PATCH',
      )
      expect(patch).toBeTruthy()
      const body = JSON.parse((patch![1] as RequestInit).body as string)
      expect(body).toMatchObject({ description: 'OXXO Centro' })
    })
  })
})
