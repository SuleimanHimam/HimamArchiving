// Token + current-user storage for the auth session.

export interface AuthUser {
  id: number
  fullName: string
  email: string
  jobTitle: string
  clearance: string
  roles: string[]
  permissions: string[]
  /** Profile picture as a data URI, or null when none is set. */
  avatarUrl?: string | null
  /** The user's own signature (توقيع) and stamp (ختم) as data URIs. Stamped onto letters they
   *  send when the print design carries a signature/stamp placeholder. */
  signatureUrl?: string | null
  stampUrl?: string | null
  /** The user's department. Print designs resolve User → OrgUnit → Default, so previewing an
   *  unsaved letter needs it to land on the same letterhead the saved one will print on. */
  orgUnitId?: number | null
  /** The design this user chose for outgoing external letters. null = positional fallback. */
  defaultPrintTemplateId?: number | null
  /** The same, for internal messages. */
  defaultMailTemplateId?: number | null
}

const ACCESS = 'archiving.accessToken'
const REFRESH = 'archiving.refreshToken'
const USER = 'archiving.user'
const EXPIRES = 'archiving.expiresAt'

/**
 * sessionStorage, not localStorage.
 *
 * localStorage survives the browsing context: closing the app and opening it again — or the OS
 * killing the process to reclaim memory, which Android does routinely to home-screen shortcuts and
 * installed PWAs — silently resumed the previous session instead of asking who was holding the
 * device. sessionStorage is cleared when the context genuinely ends, so a real close-and-reopen
 * lands on the login screen, while ordinary reloads and in-app navigation are unaffected.
 *
 * Not airtight on its own: Chrome deliberately restores sessionStorage into a tab it resurrects
 * through its own crash-recovery, which is intended behaviour and not something the page can
 * refuse. What bounds that case is the access token's own lifetime (Jwt:AccessTokenMinutes) —
 * kept short enough that a long absence invalidates it, and well above the idle window so it never
 * interrupts someone who is actually working.
 */
const store: Storage = sessionStorage

/**
 * Tokens written by an older build live in localStorage, where they would outlive the browser
 * session this change exists to bound. Cleared once, on load — deliberately without carrying them
 * over: the point of the change is that a session which survived a close should not be resumed.
 */
if (typeof localStorage !== 'undefined' && localStorage.getItem(ACCESS)) {
  for (const k of [ACCESS, REFRESH, USER, EXPIRES]) localStorage.removeItem(k)
}

/** Seeded system role name. Must match the backend's seeded role exactly. */
export const SYSTEM_ADMIN_ROLE = 'System Administrator'

export const auth = {
  getAccessToken: () => store.getItem(ACCESS),
  getRefreshToken: () => store.getItem(REFRESH),

  /** When the access token expires, as epoch ms — or null if it was never recorded. */
  getExpiresAt(): number | null {
    const raw = store.getItem(EXPIRES)
    if (!raw) return null
    const t = Date.parse(raw)
    return Number.isFinite(t) ? t : null
  },

  getUser(): AuthUser | null {
    const raw = store.getItem(USER)
    return raw ? (JSON.parse(raw) as AuthUser) : null
  },

  isAuthenticated: () => !!store.getItem(ACCESS),

  isSystemAdmin: () => auth.getUser()?.roles.includes(SYSTEM_ADMIN_ROLE) ?? false,

  // System Admin short-circuits every gate. This mirrors the server's PermissionBehaviour
  // for UX only — it decides what to render, never what is allowed. The API re-checks
  // every call from the signed token, so a stale localStorage role grants nothing.
  hasPermission: (code: string) =>
    auth.isSystemAdmin() || (auth.getUser()?.permissions.includes(code) ?? false),

  setSession(accessToken: string, refreshToken: string, user: AuthUser, expiresAt?: string) {
    store.setItem(ACCESS, accessToken)
    store.setItem(REFRESH, refreshToken)
    store.setItem(USER, JSON.stringify(user))
    if (expiresAt) store.setItem(EXPIRES, expiresAt)
  },

  clear() {
    store.removeItem(ACCESS)
    store.removeItem(REFRESH)
    store.removeItem(USER)
    store.removeItem(EXPIRES)
  },
}
