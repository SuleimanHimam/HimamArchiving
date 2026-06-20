import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import { auth, type AuthUser } from './auth'

export const CURRENT_USER_KEY = ['currentUser'] as const

async function fetchMe(): Promise<AuthUser> {
  const { data } = await api.get<AuthUser>('/auth/me')
  // Keep localStorage in sync so auth.hasPermission() stays accurate.
  const token = auth.getAccessToken()
  const refresh = auth.getRefreshToken()
  if (token && refresh) auth.setSession(token, refresh, data)
  return data
}

export function useCurrentUser() {
  return useQuery({
    queryKey: CURRENT_USER_KEY,
    queryFn: fetchMe,
    // Seed from localStorage so first render has data immediately (no loading flash).
    initialData: auth.getUser() ?? undefined,
    enabled: auth.isAuthenticated(),
  })
}

export function useHasPermission(code: string): boolean {
  const { data: user } = useCurrentUser()
  return user?.permissions.includes(code) ?? false
}
