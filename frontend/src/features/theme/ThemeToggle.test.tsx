import { fireEvent, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { ThemeToggle } from './ThemeToggle'
import { useThemeStore } from './store'

describe('ThemeToggle', () => {
  beforeEach(() => {
    window.localStorage.clear()
    useThemeStore.getState().setTheme('dark')
  })

  it('offers to switch to light mode when dark is active', () => {
    render(<ThemeToggle />)

    expect(
      screen.getByRole('button', { name: 'Activer le thème clair' }),
    ).toBeInTheDocument()
  })

  it('toggles the theme in the store on click', () => {
    render(<ThemeToggle />)

    fireEvent.click(screen.getByRole('button'))

    expect(useThemeStore.getState().theme).toBe('light')
    expect(
      screen.getByRole('button', { name: 'Activer le thème sombre' }),
    ).toBeInTheDocument()
  })
})
