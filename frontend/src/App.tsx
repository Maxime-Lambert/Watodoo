import { useState } from 'react'
import { Helmet } from 'react-helmet-async'
import { LoginForm } from './features/auth/LoginForm'
import { RegisterForm } from './features/auth/RegisterForm'
import { useLogout } from './features/auth/hooks'
import { useAuthStore } from './features/auth/store'
import { useAuthBootstrap } from './features/auth/useAuthBootstrap'

function AuthenticatedView() {
  const user = useAuthStore((s) => s.user)
  const logoutMutation = useLogout()

  return (
    <div className="flex flex-col items-center gap-2">
      <p>Connecté en tant que {user?.email}</p>
      <button
        type="button"
        onClick={() => {
          logoutMutation.mutate()
        }}
        className="rounded bg-slate-900 px-3 py-2 text-white dark:bg-white dark:text-slate-900"
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
      <main className="flex min-h-screen flex-col items-center justify-center gap-6 bg-white text-slate-900 dark:bg-slate-900 dark:text-white">
        <div className="flex flex-col items-center gap-2">
          <h1 className="text-3xl font-semibold">Watodoo</h1>
          <p className="text-slate-500 dark:text-slate-400">En quelques clics, découvre quoi faire ce soir.</p>
        </div>
        {isReady &&
          (isAuthenticated ? (
            <AuthenticatedView />
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
