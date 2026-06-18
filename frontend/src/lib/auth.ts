// Token + current-user storage for the auth session.

export interface AuthUser {
  id: number
  fullName: string
  email: string
  jobTitle: string
  clearance: string
  roles: string[]
  permissions: string[]
}

const ACCESS = 'archiving.accessToken'
const REFRESH = 'archiving.refreshToken'
const USER = 'archiving.user'

export const auth = {
  getAccessToken: () => localStorage.getItem(ACCESS),
  getRefreshToken: () => localStorage.getItem(REFRESH),

  getUser(): AuthUser | null {
    const raw = localStorage.getItem(USER)
    return raw ? (JSON.parse(raw) as AuthUser) : null
  },

  isAuthenticated: () => !!localStorage.getItem(ACCESS),

  hasPermission: (code: string) => auth.getUser()?.permissions.includes(code) ?? false,

  setSession(accessToken: string, refreshToken: string, user: AuthUser) {
    localStorage.setItem(ACCESS, accessToken)
    localStorage.setItem(REFRESH, refreshToken)
    localStorage.setItem(USER, JSON.stringify(user))
  },

  clear() {
    localStorage.removeItem(ACCESS)
    localStorage.removeItem(REFRESH)
    localStorage.removeItem(USER)
  },
}
