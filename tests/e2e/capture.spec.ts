import { test, expect } from '@playwright/test'

/**
 * Capture UX E2E (epic 9, feature #52). Runs on demand against a LIVE stack (SPA + API), exactly like
 * ledger.spec.ts — it is NOT part of `npm run build` / `npm test`. Bring the stack up with dev-auth:
 *
 *   # API (Aspire) up with the Dev auth scheme, then:
 *   cd web && VITE_API_BASE_URL=http://localhost:5016 npm run dev
 *   cd tests/e2e && npm install && npm run install-browsers
 *   E2E_BASE_URL=http://localhost:5173 npm test
 *
 * Covers the two headline checks from the issue:
 *  1. Quick-add happy path: type a phrase → AI parses a draft → confirm → it lands in the ledger.
 *  2. Narrow viewport (375px): the persistent capture bar is visible with NO horizontal overflow.
 */

test('quick-add a transaction → confirm → it appears in the ledger', async ({ page }) => {
  const phrase = `gasté 4242 en E2E Cafe ${Date.now()}`

  // An account is needed so the confirm sheet can file the transaction. Create one if the ledger
  // has none yet (idempotent enough for a fresh local household).
  await page.goto('/accounts')
  if (!(await page.getByText(/BBVA|Cash|Checking/i).first().isVisible().catch(() => false))) {
    await page.getByRole('button', { name: /add/i }).first().click()
    const dialog = page.getByRole('dialog')
    await dialog.getByLabel('Name').fill(`Capture Cash ${Date.now()}`)
    await dialog.getByLabel('Type').selectOption('Cash')
    await dialog.getByRole('button', { name: /add account/i }).click()
    await expect(dialog).toBeHidden()
  }

  // The quick-add bar is persistent — drive it from the home page.
  await page.goto('/')
  const quickAdd = page.getByLabel(/quick add a transaction/i)
  await expect(quickAdd).toBeVisible()
  await quickAdd.fill(phrase)
  await quickAdd.press('Enter')

  // The confirm sheet opens with the parsed draft. Nothing is persisted yet (confirm-before-persist).
  const sheet = page.getByRole('dialog')
  await expect(sheet.getByText(/confirm transaction/i)).toBeVisible()
  await expect(sheet.getByText(/AI confidence/i)).toBeVisible()
  // Ensure a known description, then confirm.
  await sheet.getByLabel(/merchant/i).fill('E2E Cafe Quickadd')
  await sheet.getByRole('button', { name: /add to ledger/i }).click()
  await expect(sheet).toBeHidden()

  // It shows up in the unified ledger (include unconfirmed so it's visible immediately).
  await page.getByRole('link', { name: 'Ledger' }).click()
  await expect(page.getByText('E2E Cafe Quickadd')).toBeVisible()
})

test('capture bar is visible at 375px with no horizontal overflow', async ({ page }) => {
  // Force the issue's target phone width.
  await page.setViewportSize({ width: 375, height: 812 })
  await page.goto('/')

  // The persistent quick-add bar control is visible and within the viewport.
  const quickAdd = page.getByLabel(/quick add a transaction/i)
  await expect(quickAdd).toBeVisible()
  const box = await quickAdd.boundingBox()
  expect(box).not.toBeNull()
  expect(box!.x).toBeGreaterThanOrEqual(0)
  expect(box!.x + box!.width).toBeLessThanOrEqual(375 + 1)

  // No horizontal scrolling: the document is not wider than the viewport.
  const { scrollWidth, clientWidth } = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }))
  expect(scrollWidth).toBeLessThanOrEqual(clientWidth + 1)

  // The thumb-reachable bottom nav exposes the primary capture flows.
  await expect(page.getByRole('link', { name: 'Scan' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Review' })).toBeVisible()
})
