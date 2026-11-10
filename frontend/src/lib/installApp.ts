/**
 * Installing the system as a desktop / home-screen app.
 *
 * The browser decides when an app is installable and fires `beforeinstallprompt` once. That event
 * is the only way to show the prompt later, so it is captured here at module load — by the time a
 * component mounts and adds its own listener, the event has usually already fired and is gone.
 */

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

let deferred: BeforeInstallPromptEvent | null = null
const listeners = new Set<() => void>()

function announce() { listeners.forEach((fn) => fn()) }

if (typeof window !== 'undefined') {
  window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault()            // suppress the browser's own mini-infobar; we place the button
    deferred = e as BeforeInstallPromptEvent
    announce()
  })
  window.addEventListener('appinstalled', () => { deferred = null; announce() })
}

/** True when the app is running in its installed window rather than a browser tab. */
export const isInstalled = () =>
  typeof window !== 'undefined' &&
  (window.matchMedia('(display-mode: standalone)').matches ||
    (window.navigator as { standalone?: boolean }).standalone === true)

export const canInstall = () => deferred !== null && !isInstalled()

/** Show the browser's install prompt. Returns true if the user accepted. */
export async function promptInstall(): Promise<boolean> {
  if (!deferred) return false
  await deferred.prompt()
  const { outcome } = await deferred.userChoice
  // The event is single-use: once prompted it cannot be replayed, so the button must go away.
  deferred = null
  announce()
  return outcome === 'accepted'
}

/** Subscribe to availability changes. Returns an unsubscribe function. */
export function onInstallAvailability(fn: () => void) {
  listeners.add(fn)
  return () => { listeners.delete(fn) }
}

/**
 * Register the service worker — the second requirement for installability, and what lets the
 * installed window open when the network is down.
 *
 * Dev is skipped on purpose: a worker caching Vite's module graph makes hot reload lie about what
 * is on disk.
 */
export function registerServiceWorker() {
  if (!import.meta.env.PROD || !('serviceWorker' in navigator)) return
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => { /* not fatal: the app still runs */ })
  })
}
