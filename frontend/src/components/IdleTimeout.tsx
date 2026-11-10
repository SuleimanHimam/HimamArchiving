import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import { Clock } from 'lucide-react'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { auth } from '../lib/auth'
import {
  IDLE_TIMEOUT_MS, IDLE_WARNING_MS, broadcastActivity, endSession,
  onSessionMessage, refreshIfExpiringSoon, refreshSession,
} from '../lib/session'

/** DOM events that count as a person being at the machine. Deliberately not: focus, visibility,
 *  network responses or React renders — background polling must never look like presence. */
const ACTIVITY_EVENTS = ['mousemove', 'mousedown', 'keydown', 'touchstart', 'scroll', 'wheel'] as const

/** How often other tabs are told about activity. Every mousemove would be thousands of messages. */
const BROADCAST_THROTTLE_MS = 5_000

/**
 * Ends an unattended session.
 *
 * Mounted once inside the authenticated shell — there is no session to time out before sign-in,
 * and mounting per page would restart the clock on every navigation.
 *
 * The countdown runs against a wall-clock deadline rather than a decrementing counter, because a
 * background tab's timers are throttled: a naive counter would still read "43 seconds" after the
 * screen had been unattended for an hour.
 */
export default function IdleTimeout() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const qc = useQueryClient()

  const lastActivity = useRef(Date.now())
  const lastBroadcast = useRef(0)
  /** Epoch ms at which the session ends, or null while no warning is showing. */
  const [deadline, setDeadline] = useState<number | null>(null)
  const [remaining, setRemaining] = useState(IDLE_WARNING_MS)
  // Read inside listeners that are registered once; a stale closure would let a warning be
  // dismissed by the very mouse movement it is meant to ignore.
  const warning = useRef(false)
  warning.current = deadline !== null

  /** Leave for the login screen, saying why. Shared by this tab's expiry and another tab's. */
  const goToLogin = useCallback((reason: 'idle' | 'manual') => {
    qc.clear()
    setDeadline(null)
    navigate('/login', { replace: true, state: { reason } })
  }, [navigate, qc])

  const resetIdle = useCallback((notifyOthers: boolean) => {
    lastActivity.current = Date.now()
    setDeadline(null)
    if (notifyOthers && Date.now() - lastBroadcast.current > BROADCAST_THROTTLE_MS) {
      lastBroadcast.current = Date.now()
      broadcastActivity()
    }
  }, [])

  // ── real input in this tab ──────────────────────────────────────────────────
  useEffect(() => {
    const onActivity = () => {
      // While the warning is up, movement in *this* tab is ignored: a cursor nudged while reading
      // the dialog must not silently cancel it. The button is the only local way out.
      if (warning.current) return
      resetIdle(true)
      void refreshIfExpiringSoon()
    }
    for (const e of ACTIVITY_EVENTS) {
      window.addEventListener(e, onActivity, { passive: true })
    }
    return () => {
      for (const e of ACTIVITY_EVENTS) window.removeEventListener(e, onActivity)
    }
  }, [resetIdle])

  // ── the other tabs ──────────────────────────────────────────────────────────
  useEffect(() => onSessionMessage((m) => {
    if (m.type === 'activity') {
      // Someone is working in another tab, so this one is not unattended — and this *does* clear a
      // warning here, unlike local mouse movement: it is confirmed presence, not a stray cursor.
      resetIdle(false)
    } else if (m.type === 'logout') {
      auth.clear()
      goToLogin(m.reason)
    }
  }), [resetIdle, goToLogin])

  // ── the clock ───────────────────────────────────────────────────────────────
  useEffect(() => {
    const tick = window.setInterval(() => {
      const now = Date.now()

      if (deadline === null) {
        if (now - lastActivity.current >= IDLE_TIMEOUT_MS) setDeadline(now + IDLE_WARNING_MS)
        return
      }

      const left = deadline - now
      setRemaining(left > 0 ? left : 0)
      if (left <= 0) {
        endSession('idle')          // clears storage, tells the server, closes the other tabs
        goToLogin('idle')
      }
    }, 500)
    return () => window.clearInterval(tick)
  }, [deadline, goToLogin])

  async function stayLoggedIn() {
    resetIdle(true)
    // The idle window is a fraction of the access token's life, so this is rarely needed — but a
    // user who has been working for longer than one token lifetime should not be signed out by an
    // expiry a moment after telling us they are still there.
    await refreshSession()
  }

  if (deadline === null) return null

  const seconds = Math.ceil(remaining / 1000)

  return (
    <Dialog open onOpenChange={() => { /* not dismissible by backdrop or Escape — answer it */ }}>
      {/* [&>button]:hidden removes DialogContent's own ✕ — this dialog has one answer, and a close
          affordance that cannot close anything reads as a broken control. */}
      <DialogContent className="max-w-sm [&>button]:hidden"
                     onPointerDownOutside={(e) => e.preventDefault()}
                     onEscapeKeyDown={(e) => e.preventDefault()}>
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Clock size={18} aria-hidden />
            {t('session.idle.title')}
          </DialogTitle>
          <DialogDescription>{t('session.idle.body')}</DialogDescription>
        </DialogHeader>

        {/* `seconds`, not i18next's reserved `count`: that key selects a plural form, and Arabic
            has six — the message is one sentence with a number in it, not six sentences. */}
        <p className="text-center text-lg font-semibold tabular-nums" role="timer" aria-live="assertive">
          {t('session.idle.countdown', { seconds })}
        </p>

        {/* Wrapped: the rule above hides DialogContent's *direct* button children, and this one
            would otherwise vanish with the ✕ — leaving a modal with no way out of it. */}
        <div>
          <Button onClick={() => { void stayLoggedIn() }} autoFocus className="w-full">
            {t('session.idle.stay')}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  )
}
