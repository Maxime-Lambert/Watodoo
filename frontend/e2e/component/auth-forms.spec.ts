import { expect, test } from '@playwright/test'

test.describe('Auth forms', () => {
  test.beforeEach(async ({ page }) => {
    await page.route('**/auth/refresh', async (route) => {
      await route.fulfill({ status: 401 })
    })
  })

  test('login shows a generic error message on invalid credentials', async ({
    page,
  }) => {
    await page.route('**/auth/login', async (route) => {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({
          title: 'Unauthorized',
          detail: 'Email ou mot de passe incorrect.',
        }),
      })
    })

    await page.goto('/')

    await page.getByLabel('Email').fill('user@example.com')
    await page.getByLabel('Mot de passe').fill('wrong-password')
    await page.getByRole('button', { name: 'Se connecter' }).click()

    await expect(
      page.getByText('Email ou mot de passe incorrect.'),
    ).toBeVisible()
  })

  test('submit button is disabled while the request is pending', async ({
    page,
  }) => {
    await page.route('**/auth/login', async (route) => {
      await new Promise((resolve) => setTimeout(resolve, 500))
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          userId: '00000000-0000-0000-0000-000000000000',
          email: 'user@example.com',
          accessToken: 'fake-token',
        }),
      })
    })

    await page.goto('/')

    await page.getByLabel('Email').fill('user@example.com')
    await page.getByLabel('Mot de passe').fill('Passw0rd1')
    await page.getByRole('button', { name: 'Se connecter' }).click()

    await expect(
      page.getByRole('button', { name: 'Connexion…' }),
    ).toBeDisabled()
  })

  test('can switch between login and register forms', async ({ page }) => {
    await page.goto('/')

    await expect(
      page.getByRole('heading', { name: 'Se connecter' }),
    ).toBeVisible()

    await page
      .getByRole('button', { name: "Pas de compte ? S'inscrire" })
      .click()

    await expect(
      page.getByRole('heading', { name: 'Créer un compte' }),
    ).toBeVisible()
  })
})
