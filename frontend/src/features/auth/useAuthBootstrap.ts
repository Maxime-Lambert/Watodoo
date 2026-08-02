import { useEffect, useState } from 'react'
import { refresh } from './api'
import { useAuthStore } from './store'

// Renvoie true une fois la tentative de réhydratation (réussie ou non) terminée.
export function useAuthBootstrap(): boolean {
  const setAuth = useAuthStore((s) => s.setAuth)
  const [isReady, setIsReady] = useState(false)

  useEffect(() => {
    let cancelled = false

    void refresh().then((result) => {
      if (cancelled) {
        return
      }

      if (result) {
        setAuth(result.accessToken, {
          userId: result.userId,
          email: result.email,
        })
      }

      setIsReady(true)
    })

    return () => {
      cancelled = true
    }
  }, [setAuth])

  return isReady
}
