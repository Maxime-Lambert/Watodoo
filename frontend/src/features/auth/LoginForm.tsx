import { useState, type FormEvent } from 'react'
import { useLogin } from './hooks'

interface LoginFormProps {
  onSwitchToRegister: () => void
}

export function LoginForm({ onSwitchToRegister }: LoginFormProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const loginMutation = useLogin()

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    loginMutation.mutate({ email, password })
  }

  return (
    <form onSubmit={handleSubmit} className="flex w-full max-w-sm flex-col gap-3">
      <h2 className="text-xl font-semibold">Se connecter</h2>
      <label className="flex flex-col gap-1 text-sm">
        Email
        <input
          type="email"
          required
          value={email}
          onChange={(event) => {
            setEmail(event.target.value)
          }}
          className="rounded-sm border border-border bg-surface px-3 py-2 text-foreground"
        />
      </label>
      <label className="flex flex-col gap-1 text-sm">
        Mot de passe
        <input
          type="password"
          required
          value={password}
          onChange={(event) => {
            setPassword(event.target.value)
          }}
          className="rounded-sm border border-border bg-surface px-3 py-2 text-foreground"
        />
      </label>
      {loginMutation.isError && <p className="text-sm text-red-600">{loginMutation.error.message}</p>}
      <button
        type="submit"
        disabled={loginMutation.isPending}
        className="rounded-md bg-accent px-3 py-2 text-accent-foreground disabled:opacity-50"
      >
        {loginMutation.isPending ? 'Connexion…' : 'Se connecter'}
      </button>
      <button type="button" onClick={onSwitchToRegister} className="text-sm text-muted underline">
        Pas de compte ? S'inscrire
      </button>
    </form>
  )
}
