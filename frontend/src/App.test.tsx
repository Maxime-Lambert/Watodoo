import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import { HelmetProvider } from 'react-helmet-async'
import { describe, expect, it } from 'vitest'
import App from './App'

describe('App', () => {
  it('renders the Watodoo heading', () => {
    const queryClient = new QueryClient()

    render(
      <HelmetProvider>
        <QueryClientProvider client={queryClient}>
          <App />
        </QueryClientProvider>
      </HelmetProvider>,
    )

    expect(screen.getByRole('heading', { name: 'Watodoo' })).toBeInTheDocument()
  })

  it('links to the legal pages', () => {
    const queryClient = new QueryClient()

    render(
      <HelmetProvider>
        <QueryClientProvider client={queryClient}>
          <App />
        </QueryClientProvider>
      </HelmetProvider>,
    )

    expect(
      screen.getByRole('link', { name: 'Mentions légales' }),
    ).toHaveAttribute('href', '/mentions-legales.html')
    expect(screen.getByRole('link', { name: 'CGU' })).toHaveAttribute(
      'href',
      '/cgu.html',
    )
  })
})
