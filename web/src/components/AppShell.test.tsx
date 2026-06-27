import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { screen } from '@testing-library/react'
import { renderWithProviders } from '../test/render'
import { AppShell } from './AppShell'
import { primaryNav } from '../lib/nav'
import { __setAuthProvider, type AuthProvider } from '../api/auth'

const fakeAuth: AuthProvider = {
  mode: 'dev',
  getAuthHeaders: () => Promise.resolve({ 'X-Dev-Tenant': 't', 'X-Dev-User': 'u', 'X-Dev-Role': 'Owner' }),
  getContext: () => ({ tenantId: 't', userId: 'u', role: 'Owner' }),
}

function jsonOk(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'content-type': 'application/json' } })
}

/**
 * jsdom does not compute real layout, so this is a structural narrow-viewport check: at a 375px
 * phone width the persistent quick-add bar's control is in the DOM and labelled, the thumb-reachable
 * bottom nav exposes the primary flows, and no rendered element pins itself to a fixed pixel width
 * wider than the viewport (a common cause of horizontal overflow). The real, measured no-horizontal-
 * scroll check lives in the Playwright E2E (tests/e2e), which runs a real browser at 375px.
 */
describe('AppShell — mobile (375px) capture reachability', () => {
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    __setAuthProvider(fakeAuth)
    fetchMock = vi.fn().mockResolvedValue(jsonOk({ id: 'h1', name: 'Fernandez', plan: 'Free', createdAt: '2026-01-01' }))
    vi.stubGlobal('fetch', fetchMock)
    window.innerWidth = 375
    window.innerHeight = 812
    window.dispatchEvent(new Event('resize'))
  })

  afterEach(() => {
    __setAuthProvider(null)
    vi.unstubAllGlobals()
  })

  it('renders the persistent quick-add bar control, labelled and reachable', () => {
    renderWithProviders(
      <AppShell>
        <div>page</div>
      </AppShell>,
    )
    const quickAdd = screen.getByLabelText(/quick add a transaction/i)
    expect(quickAdd).toBeInTheDocument()
    // ≥44px tap target intent: the submit control is an explicit button, not a tiny icon-only hit area.
    expect(screen.getByRole('button', { name: /parse and confirm/i })).toBeInTheDocument()
  })

  it('exposes every primary flow in the thumb-reachable bottom nav', () => {
    renderWithProviders(
      <AppShell>
        <div>page</div>
      </AppShell>,
    )
    // The bottom nav renders one link per primary nav item (Home / Ledger / Scan / Review).
    for (const item of primaryNav) {
      expect(screen.getAllByRole('link', { name: new RegExp(item.label, 'i') }).length).toBeGreaterThan(0)
    }
    // Capture flows are celebrated as primary, thumb-reachable actions.
    expect(primaryNav.map((n) => n.to)).toContain('/capture')
    expect(primaryNav.map((n) => n.to)).toContain('/review')
  })

  it('pins no element to a fixed pixel width wider than the 375px viewport', () => {
    const { container } = renderWithProviders(
      <AppShell>
        <div>page</div>
      </AppShell>,
    )
    // Scan inline styles for a hardcoded px width that would overflow a phone screen.
    const offenders: string[] = []
    container.querySelectorAll<HTMLElement>('[style]').forEach((el) => {
      const width = el.style.width
      const match = /^(\d+(?:\.\d+)?)px$/.exec(width)
      if (match && Number(match[1]) > 375) {
        offenders.push(`${el.tagName} width:${width}`)
      }
    })
    expect(offenders).toEqual([])
  })
})
