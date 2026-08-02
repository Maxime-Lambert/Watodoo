import { expect, test } from '@playwright/test'

test('affiche le shell applicatif Watodoo', async ({ page }) => {
  await page.goto('/')

  await expect(page).toHaveTitle('Watodoo')
  await expect(page.getByRole('heading', { name: 'Watodoo' })).toBeVisible()
})

test('le thème sombre est actif par défaut et bascule vers le thème clair', async ({
  page,
}) => {
  await page.goto('/')

  const html = page.locator('html')
  await expect(html).toHaveClass(/dark/)

  await page.getByRole('button', { name: 'Activer le thème clair' }).click()

  await expect(html).not.toHaveClass(/dark/)
  await expect(
    page.getByRole('button', { name: 'Activer le thème sombre' }),
  ).toBeVisible()
})

test('les pages légales sont accessibles depuis le footer', async ({
  page,
}) => {
  await page.goto('/')

  await page.getByRole('link', { name: 'Mentions légales' }).click()
  await expect(page).toHaveTitle('Mentions légales — Watodoo')
  await expect(
    page.getByRole('heading', { name: 'Mentions légales' }),
  ).toBeVisible()

  await page.goto('/')
  await page.getByRole('link', { name: 'CGU' }).click()
  await expect(page).toHaveTitle("Conditions générales d'utilisation — Watodoo")
})
