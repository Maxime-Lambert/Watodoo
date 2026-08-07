import { defineConfig, devices } from '@playwright/test'

const PORT = '5173'
const baseURL = process.env.E2E_BASE_URL ?? `http://localhost:${PORT}`

export default defineConfig({
  timeout: 30_000,
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: 'html',
  use: {
    trace: 'on-first-retry',
  },
  webServer: process.env.E2E_BASE_URL
    ? undefined
    : {
        command: 'pnpm dev',
        url: baseURL,
        reuseExistingServer: !process.env.CI,
      },
  projects: [
    {
      name: 'component',
      testDir: './e2e/component',
      use: { ...devices['Desktop Chrome'], baseURL },
    },
    {
      name: 'journeys',
      testDir: './e2e/journeys',
      use: { ...devices['Desktop Chrome'], baseURL },
    },
  ],
})
