import { create } from 'zustand'

export interface AuthUser {
  userId: string
  email: string
}

interface AuthState {
  accessToken: string | null
  user: AuthUser | null
  isAuthenticated: boolean
  setAuth: (accessToken: string, user: AuthUser) => void
  clear: () => void
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  user: null,
  isAuthenticated: false,
  setAuth: (accessToken, user) => {
    set({ accessToken, user, isAuthenticated: true })
  },
  clear: () => {
    set({ accessToken: null, user: null, isAuthenticated: false })
  },
}))
