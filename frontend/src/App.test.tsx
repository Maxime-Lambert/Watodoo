import { render, screen } from '@testing-library/react'
import { HelmetProvider } from 'react-helmet-async'
import { describe, expect, it } from 'vitest'
import App from './App'

describe('App', () => {
  it('renders the Watodoo heading', () => {
    render(
      <HelmetProvider>
        <App />
      </HelmetProvider>,
    )

    expect(screen.getByRole('heading', { name: 'Watodoo' })).toBeInTheDocument()
  })
})
