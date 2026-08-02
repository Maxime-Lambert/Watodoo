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
    <form
      onSubmit={handleSubmit}
      className="flex w-full max-w-sm flex-col gap-3"
    >
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
          className="border-border bg-surface text-foreground rounded-sm border px-3 py-2"
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
          className="border-border bg-surface text-foreground rounded-sm border px-3 py-2"
        />
      </label>
      {loginMutation.isError && (
        <p className="text-sm text-red-600">{loginMutation.error.message}</p>
      )}
      <button
        type="submit"
        disabled={loginMutation.isPending}
        className="bg-accent text-accent-foreground rounded-md px-3 py-2 disabled:opacity-50"
      >
        {loginMutation.isPending ? 'Connexion…' : 'Se connecter'}
      </button>
      <button
        type="button"
        onClick={onSwitchToRegister}
        className="text-muted text-sm underline"
      >
        Pas de compte ? S'inscrire
      </button>
    </form>
  )
}
