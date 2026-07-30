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
          className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
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
          className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
        />
      </label>
      {registerMutation.isError && <p className="text-sm text-red-600">{registerMutation.error.message}</p>}
      <button
        type="submit"
        disabled={registerMutation.isPending}
        className="rounded bg-slate-900 px-3 py-2 text-white disabled:opacity-50 dark:bg-white dark:text-slate-900"
      >
        {registerMutation.isPending ? 'Création…' : "S'inscrire"}
      </button>
      <button type="button" onClick={onSwitchToLogin} className="text-sm text-slate-500 underline">
        Déjà un compte ? Se connecter
      </button>
    </form>
  )
}
