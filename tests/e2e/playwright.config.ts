import { defineConfig, devices } from '@playwright/test'

// Run against a live SPA:
//   cd tests/e2e && npm install && npm run install-browsers
//   E2E_BASE_URL=http://localhost:5173 npm test
export default defineConfig({
  testDir: '.',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173',
  },
  projects: [
    { name: 'mobile', use: { ...devices['Pixel 7'] } },
  ],
})
