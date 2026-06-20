import { useEffect, useRef } from 'react'

/**
 * Periodically invokes `fn` while the tab is visible (and once again when it regains focus).
 * Pass a "silent" refresh callback so the list updates in the background without loading flicker.
 */
export function useAutoRefresh(fn: () => void, intervalMs = 30000, enabled = true) {
  const ref = useRef(fn)
  ref.current = fn

  useEffect(() => {
    if (!enabled || intervalMs <= 0) return
    const tick = () => { if (document.visibilityState === 'visible') ref.current() }
    const id = window.setInterval(tick, intervalMs)
    const onVisible = () => { if (document.visibilityState === 'visible') ref.current() }
    document.addEventListener('visibilitychange', onVisible)
    return () => { clearInterval(id); document.removeEventListener('visibilitychange', onVisible) }
  }, [intervalMs, enabled])
}
