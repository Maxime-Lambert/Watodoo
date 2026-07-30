import { useMutation, useQueryClient } from '@tanstack/react-query'
import * as api from './api'
import { useAuthStore } from './store'

interface Credentials {
  email: string
  password: string
}

export function useRegister() {
  const setAuth = useAuthStore((s) => s.setAuth)

  return useMutation({
    mutationFn: ({ email, password }: Credentials) => api.register(email, password),
    onSuccess: (data) => {
      setAuth(data.accessToken, { userId: data.userId, email: data.email })
    },
  })
}

export function useLogin() {
  const setAuth = useAuthStore((s) => s.setAuth)

  return useMutation({
    mutationFn: ({ email, password }: Credentials) => api.login(email, password),
    onSuccess: (data) => {
      setAuth(data.accessToken, { userId: data.userId, email: data.email })
    },
  })
}

export function useLogout() {
  const clear = useAuthStore((s) => s.clear)
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: () => api.logout(),
    onSuccess: () => {
      clear()
      queryClient.clear()
    },
  })
}
