import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useThemeStore } from './store'

describe('useThemeStore', () => {
  beforeEach(() => {
    window.localStorage.clear()
    useThemeStore.getState().setTheme('dark')
  })

  it('defaults to dark', () => {
    expect(useThemeStore.getState().theme).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })

  it('toggle switches to light, applies the class and persists it', () => {
    useThemeStore.getState().toggle()

    const state = useThemeStore.getState()
    expect(state.theme).toBe('light')
    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(window.localStorage.getItem('watodoo-theme')).toBe('light')
  })

  it('toggling twice restores dark', () => {
    useThemeStore.getState().toggle()
    useThemeStore.getState().toggle()

    expect(useThemeStore.getState().theme).toBe('dark')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })

  it('restores a previously stored light theme on reload', async () => {
    window.localStorage.setItem('watodoo-theme', 'light')
    vi.resetModules()

    const { useThemeStore: reloadedStore } = await import('./store')

    expect(reloadedStore.getState().theme).toBe('light')
    expect(document.documentElement.classList.contains('dark')).toBe(false)
  })
})
