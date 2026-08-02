import { useState, type FormEvent } from 'react'
import { useRegister } from './hooks'

interface RegisterFormProps {
  onSwitchToLogin: () => void
}

export function RegisterForm({ onSwitchToLogin }: RegisterFormProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const registerMutation = useRegister()

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    registerMutation.mutate({ email, password })
  }

  return (
    <form onSubmit={handleSubmit} className="flex w-full max-w-sm flex-col gap-3">
      <h2 className="text-xl font-semibold">Créer un compte</h2>
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
      {registerMutation.isError && <p className="text-sm text-red-600">{registerMutation.error.message}</p>}
      <button
        type="submit"
        disabled={registerMutation.isPending}
        className="rounded-md bg-accent px-3 py-2 text-accent-foreground disabled:opacity-50"
      >
        {registerMutation.isPending ? 'Création…' : "S'inscrire"}
      </button>
      <button type="button" onClick={onSwitchToLogin} className="text-sm text-muted underline">
        Déjà un compte ? Se connecter
      </button>
    </form>
  )
}
