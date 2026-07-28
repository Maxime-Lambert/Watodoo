import { expect, test } from '@playwright/test'

test('affiche le shell applicatif Watodoo', async ({ page }) => {
  await page.goto('/')

  await expect(page).toHaveTitle('Watodoo')
  await expect(page.getByRole('heading', { name: 'Watodoo' })).toBeVisible()
})
