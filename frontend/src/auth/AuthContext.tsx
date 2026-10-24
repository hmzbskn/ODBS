import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, setUnauthorizedHandler } from '../api/client'
import type { LoginResponse, UserInfo } from '../api/types'

const SESSION_KEY = 'advisor.session'

interface AuthState {
  user: UserInfo | null
  login: (username: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthState | null>(null)

function loadSession(): LoginResponse | null {
  try {
    const raw = localStorage.getItem(SESSION_KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as LoginResponse
    return new Date(session.expiresAtUtc).getTime() > Date.now() ? session : null
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<LoginResponse | null>(loadSession)

  const logout = useCallback(() => {
    try {
      localStorage.removeItem(SESSION_KEY)
    } catch {
      // Depolama kapalıysa yalnızca bellekteki oturum temizlenir.
    }
    setSession(null)
  }, [])

  const login = useCallback(async (username: string, password: string) => {
    const result = await api.post<LoginResponse>('/api/auth/login', { username, password })
    try {
      localStorage.setItem(SESSION_KEY, JSON.stringify(result))
    } catch {
      // Depolama kapalıysa oturum yalnızca bu sekme açıkken geçerli olur.
    }
    setSession(result)
  }, [])

  useEffect(() => {
    setUnauthorizedHandler(logout)
    return () => setUnauthorizedHandler(null)
  }, [logout])

  const value = useMemo<AuthState>(() => ({ user: session?.user ?? null, login, logout }), [session, login, logout])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthState {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth, AuthProvider içinde kullanılmalıdır.')
  return ctx
}
