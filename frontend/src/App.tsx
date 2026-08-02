import { useState } from 'react'
import { Helmet } from 'react-helmet-async'
import { LoginForm } from './features/auth/LoginForm'
import { RegisterForm } from './features/auth/RegisterForm'
import { useLogout } from './features/auth/hooks'
import { useAuthStore } from './features/auth/store'
import { useAuthBootstrap } from './features/auth/useAuthBootstrap'
import { ThemeToggle } from './features/theme/ThemeToggle'

function AuthenticatedView({ onLoggedOut }: { onLoggedOut: () => void }) {
  const user = useAuthStore((s) => s.user)
  const logoutMutation = useLogout()

  return (
    <div className="flex flex-col items-center gap-2">
      <p>Connecté en tant que {user?.email}</p>
      <button
        type="button"
        onClick={() => {
          onLoggedOut()
          logoutMutation.mutate()
        }}
        className="bg-accent text-accent-foreground rounded-md px-3 py-2"
      >
        Se déconnecter
      </button>
    </div>
  )
}

function App() {
  const isReady = useAuthBootstrap()
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const [mode, setMode] = useState<'login' | 'register'>('login')

  return (
    <>
      <Helmet>
        <title>Watodoo</title>
      </Helmet>
      <main className="bg-background text-foreground relative flex min-h-screen flex-col items-center justify-center gap-6">
        <div className="absolute top-4 right-4">
          <ThemeToggle />
        </div>
        <div className="flex flex-col items-center gap-2">
          <h1 className="font-display text-3xl font-semibold">Watodoo</h1>
          <p className="text-muted">
            En quelques clics, découvre quoi faire ce soir.
          </p>
        </div>
        {isReady &&
          (isAuthenticated ? (
            <AuthenticatedView
              onLoggedOut={() => {
                setMode('login')
              }}
            />
          ) : mode === 'login' ? (
            <LoginForm
              onSwitchToRegister={() => {
                setMode('register')
              }}
            />
          ) : (
            <RegisterForm
              onSwitchToLogin={() => {
                setMode('login')
              }}
            />
          ))}
      </main>
    </>
  )
}

export default App
