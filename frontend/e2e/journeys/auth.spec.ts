import { expect, test } from '@playwright/test'

function uniqueEmail(): string {
  return `e2e-${Date.now().toString()}-${Math.floor(Math.random() * 100000).toString()}@example.com`
}

test('inscription puis déconnexion puis reconnexion', async ({ page }) => {
  const email = uniqueEmail()
  const password = 'Passw0rd1'

  await page.goto('/')

  await page.getByRole('button', { name: "Pas de compte ? S'inscrire" }).click()
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Mot de passe').fill(password)
  await page.getByRole('button', { name: "S'inscrire" }).click()

  await expect(page.getByText(`Connecté en tant que ${email}`)).toBeVisible()

  await page.getByRole('button', { name: 'Se déconnecter' }).click()

  await expect(page.getByRole('heading', { name: 'Se connecter' })).toBeVisible()

  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Mot de passe').fill(password)
  await page.getByRole('button', { name: 'Se connecter' }).click()

  await expect(page.getByText(`Connecté en tant que ${email}`)).toBeVisible()
})
