/**
 * Session lifetime: how a signed-in session ends, and how open tabs agree about it.
 *
 * Three things live here so no component has to know the rules:
 *   · the timings, read from build-time config rather than written into a component;
 *   · ending a session (client state, then the server, then everyone else's tabs);
 *   · a small cross-tab bus so activity in one tab keeps the others alive and an expiry in one
 *     closes them all.
 */

import { api } from './api'
import { auth } from './auth'

/** Why a session ended — carried to the login screen so it can say so. */
export type LogoutReason = 'idle' | 'manual'

/* ── configuration ─────────────────────────────────────────────────────────────
   Both are read at build time from Vite env vars (see .env.example). Defaults are the
   values a government archive is normally configured with; they are here, in one place,
   rather than as literals inside the countdown. */

function ms(value: string | undefined, fallback: number): number {
  const n = Number(value)
  // A malformed or non-positive value falls back rather than disabling the timer outright —
  // a typo in an env var must never silently leave sessions open forever.
  return Number.isFinite(n) && n > 0 ? n : fallback
}

/** Quiet time before the warning appears. */
export const IDLE_TIMEOUT_MS = ms(import.meta.env.VITE_IDLE_TIMEOUT_MS, 15 * 60_000)

/** How long the warning counts down before the session is ended. */
export const IDLE_WARNING_MS = ms(import.meta.env.VITE_IDLE_WARNING_MS, 60_000)

/** Refresh the access token when it has less than this left and the user is active. */
const REFRESH_MARGIN_MS = 5 * 60_000

/* ── cross-tab bus ─────────────────────────────────────────────────────────────
   BroadcastChannel where it exists; a localStorage write otherwise, since the `storage` event
   fires in every *other* tab of the origin and is supported everywhere this app runs. */

type SessionMessage =
  | { type: 'activity'; at: number }
  | { type: 'logout'; reason: LogoutReason; at: number }

const CHANNEL_NAME = 'archiving.session'
const FALLBACK_KEY = 'archiving.session.signal'

const channel: BroadcastChannel | null =
  typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel(CHANNEL_NAME) : null

const listeners = new Set<(m: SessionMessage) => void>()

export function publish(message: SessionMessage) {
  if (channel) { channel.postMessage(message); return }
  // The value has to differ every time or the event does not fire for a repeat.
  localStorage.setItem(FALLBACK_KEY, JSON.stringify(message))
}

function deliver(message: SessionMessage) {
  listeners.forEach((fn) => fn(message))
}

if (channel) {
  channel.onmessage = (e: MessageEvent<SessionMessage>) => deliver(e.data)
} else if (typeof window !== 'undefined') {
  window.addEventListener('storage', (e) => {
    if (e.key !== FALLBACK_KEY || !e.newValue) return
    try { deliver(JSON.parse(e.newValue) as SessionMessage) } catch { /* ignore a bad write */ }
  })
}

/** Listen for messages from other tabs. Returns an unsubscribe function. */
export function onSessionMessage(fn: (m: SessionMessage) => void) {
  listeners.add(fn)
  return () => { listeners.delete(fn) }
}

/** Tell the other tabs this one saw real user activity. */
export function broadcastActivity() {
  publish({ type: 'activity', at: Date.now() })
}

/* ── ending a session ──────────────────────────────────────────────────────────*/

/**
 * End the session in this tab and, unless told otherwise, everywhere else.
 *
 * Client state is cleared first and the server is told afterwards without waiting: a session has
 * to end even when the network is down, and a hanging request must never be what stands between
 * an unattended screen and the login page.
 *
 * `notifyOthers` is false when this tab is *reacting* to another tab's logout — otherwise the
 * broadcast would echo around the open tabs.
 */
export function endSession(reason: LogoutReason, notifyOthers = true) {
  const refreshToken = auth.getRefreshToken()
  const accessToken = auth.getAccessToken()

  auth.clear()

  if (accessToken) {
    // Explicit header: the request interceptor reads the token from storage, which is now empty.
    void api
      .post('/auth/logout', { refreshToken }, { headers: { Authorization: `Bearer ${accessToken}` } })
      .catch(() => { /* best effort — the client side of the session is already gone */ })
  }

  if (notifyOthers) publish({ type: 'logout', reason, at: Date.now() })
}

/**
 * Trade the refresh token for a new access token.
 *
 * The access token outlives the idle window several times over, so this is not what keeps a
 * session alive minute to minute — the idle timer is. It exists so that a user who stays active
 * for longer than one access-token lifetime is not thrown out mid-sentence by an expiry that has
 * nothing to do with whether they were at their desk.
 */
export async function refreshSession(): Promise<boolean> {
  const refreshToken = auth.getRefreshToken()
  if (!refreshToken) return false
  try {
    const { data } = await api.post('/auth/refresh', { refreshToken })
    auth.setSession(data.accessToken, data.refreshToken, data.user, data.expiresAt)
    return true
  } catch {
    return false
  }
}

/** Refresh only when the current access token is close to expiring. Safe to call often. */
export async function refreshIfExpiringSoon(): Promise<void> {
  const expiresAt = auth.getExpiresAt()
  if (expiresAt === null) return
  if (expiresAt - Date.now() > REFRESH_MARGIN_MS) return
  await refreshSession()
}
