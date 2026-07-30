const API_URL: string = (import.meta.env.VITE_API_URL as string | undefined) ?? 'http://localhost:5174'

export interface AuthResponse {
  userId: string
  email: string
  accessToken: string
}

interface ProblemDetails {
  title?: string
  detail?: string
}

async function parseJsonOrThrow<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null
    throw new Error(problem?.detail ?? problem?.title ?? `Erreur ${String(response.status)}`)
  }

  return (await response.json()) as T
}

export async function register(email: string, password: string): Promise<AuthResponse> {
  const response = await fetch(`${API_URL}/auth/register`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })

  return parseJsonOrThrow<AuthResponse>(response)
}

export async function login(email: string, password: string): Promise<AuthResponse> {
  const response = await fetch(`${API_URL}/auth/login`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })

  return parseJsonOrThrow<AuthResponse>(response)
}

export async function refresh(): Promise<AuthResponse | null> {
  try {
    const response = await fetch(`${API_URL}/auth/refresh`, {
      method: 'POST',
      credentials: 'include',
    })

    if (!response.ok) {
      return null
    }

    return (await response.json()) as AuthResponse
  } catch {
    return null
  }
}

export async function logout(): Promise<void> {
  await fetch(`${API_URL}/auth/logout`, {
    method: 'POST',
    credentials: 'include',
  })
}
