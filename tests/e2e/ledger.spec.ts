import { test, expect } from '@playwright/test'

/**
 * Happy path for feature #55: add an account, record a manual transaction, and see it in the ledger.
 *
 * Runs on demand against a LIVE stack (SPA + API) — it is not part of `npm run build`/`npm test`.
 * The SPA must be built/served with dev-auth (VITE_AUTH_MODE=dev, the default) pointed at a running
 * API whose household has been provisioned, e.g.:
 *
 *   # API (Aspire) up with the Dev auth scheme, then:
 *   cd web && VITE_API_BASE_URL=http://localhost:5016 npm run dev
 *   cd tests/e2e && npm install && npm run install-browsers
 *   E2E_BASE_URL=http://localhost:5173 npm test
 *
 * The dev-auth tenant/user default to the well-known ids in http/the-ledger.http, so a freshly
 * provisioned local household is exercised end to end.
 */
test('add account, add a manual transaction, and see it in the ledger', async ({ page }) => {
  const unique = `Playwright Cash ${Date.now()}`
  const description = `E2E Coffee ${Date.now()}`

  // 1) Create an account.
  await page.goto('/accounts')
  await page.getByRole('button', { name: /add/i }).first().click()
  const accountDialog = page.getByRole('dialog')
  await accountDialog.getByLabel('Name').fill(unique)
  await accountDialog.getByLabel('Type').selectOption('Cash')
  await accountDialog.getByRole('button', { name: /add account/i }).click()
  await expect(page.getByText(unique)).toBeVisible()

  // 2) Add a manual transaction against that account.
  await page.getByRole('link', { name: 'Ledger' }).click()
  await page.getByRole('button', { name: /add/i }).first().click()
  const txDialog = page.getByRole('dialog')
  await txDialog.getByLabel('Account').selectOption({ label: unique })
  await txDialog.getByLabel('Description').fill(description)
  await txDialog.getByLabel('Amount').fill('42.50')
  await txDialog.getByRole('button', { name: /add transaction/i }).click()

  // 3) The new transaction is visible in the unified ledger.
  await expect(page.getByText(description)).toBeVisible()
})
