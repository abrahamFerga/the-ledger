import { test, expect } from '@playwright/test'

// Mobile primary-flow smoke for the PWA (feature #10).
test('mobile shell loads with thumb-reachable navigation', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByText('the-ledger')).toBeVisible()
  await expect(page.getByRole('link', { name: 'Upload' })).toBeVisible()
})

test('upload screen is reachable from the bottom nav', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('link', { name: 'Upload' }).click()
  await expect(page.getByText(/choose a PDF or CSV/i)).toBeVisible()
})
