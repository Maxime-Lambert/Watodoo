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
    <form
      onSubmit={handleSubmit}
      className="flex w-full max-w-sm flex-col gap-3"
    >
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
      {registerMutation.isError && (
        <p className="text-sm text-red-600">{registerMutation.error.message}</p>
      )}
      <button
        type="submit"
        disabled={registerMutation.isPending}
        className="bg-accent text-accent-foreground rounded-md px-3 py-2 disabled:opacity-50"
      >
        {registerMutation.isPending ? 'Création…' : "S'inscrire"}
      </button>
      <button
        type="button"
        onClick={onSwitchToLogin}
        className="text-muted text-sm underline"
      >
        Déjà un compte ? Se connecter
      </button>
    </form>
  )
}
