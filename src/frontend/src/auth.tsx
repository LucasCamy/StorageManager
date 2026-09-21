import { createContext, useContext, useEffect, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ApiError, resetCsrf } from '@/lib/api'
import type { User } from '@/lib/types'
type AuthValue = { user: User | null; loading: boolean; error: unknown; refresh: () => Promise<void>; logout: () => Promise<void> }
const AuthContext = createContext<AuthValue | null>(null)
export function AuthProvider({ children }: { children: ReactNode }) {
  const client = useQueryClient()
  const me = useQuery({ queryKey: ['me'], queryFn: async () => { try { return await api<User>('/me') } catch (error) { if (error instanceof ApiError && error.status === 401) return null; throw error } }, retry: false, staleTime: 60_000 })
  useEffect(() => { const expire = () => { client.clear(); resetCsrf(); client.setQueryData(['me'], null) }; window.addEventListener('session-expired', expire); return () => window.removeEventListener('session-expired', expire) }, [client])
  async function refresh() { resetCsrf(); await client.invalidateQueries({ queryKey: ['me'] }) }
  async function logout() { await api('/auth/logout', { method: 'POST' }); resetCsrf(); client.clear(); client.setQueryData(['me'], null) }
  return <AuthContext.Provider value={{ user: me.data || null, loading: me.isPending, error: me.error, refresh, logout }}>{children}</AuthContext.Provider>
}
export function useAuth() { const value = useContext(AuthContext); if (!value) throw new Error('Sessão não inicializada'); return value }
