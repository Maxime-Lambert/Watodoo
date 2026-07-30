import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from './store'

describe('useAuthStore', () => {
  beforeEach(() => {
    useAuthStore.getState().clear()
  })

  it('starts unauthenticated', () => {
    const state = useAuthStore.getState()

    expect(state.isAuthenticated).toBe(false)
    expect(state.accessToken).toBeNull()
    expect(state.user).toBeNull()
  })

  it('setAuth marks the store authenticated', () => {
    useAuthStore.getState().setAuth('token-123', { userId: 'u1', email: 'user@example.com' })

    const state = useAuthStore.getState()

    expect(state.isAuthenticated).toBe(true)
    expect(state.accessToken).toBe('token-123')
    expect(state.user).toEqual({ userId: 'u1', email: 'user@example.com' })
  })

  it('clear resets the store', () => {
    useAuthStore.getState().setAuth('token-123', { userId: 'u1', email: 'user@example.com' })

    useAuthStore.getState().clear()

    const state = useAuthStore.getState()

    expect(state.isAuthenticated).toBe(false)
    expect(state.accessToken).toBeNull()
    expect(state.user).toBeNull()
  })
})
