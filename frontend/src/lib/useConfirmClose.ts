import { useEffect } from 'react'
import { isInstalled } from './installApp'

/**
 * Asks before the window closes, when the system is running as a desktop program.
 *
 * The desktop shortcut opens a chromeless window (`--app=`), and closing it ends the browsing
 * context — which, since the token lives in sessionStorage, ends the session outright. That is the
 * behaviour we want, but it is one stray click on ✕ away, with open work behind it and no undo.
 * So the window asks first.
 *
 * Scoped to the installed/app window on purpose. In an ordinary browser tab the same handler would
 * nag on every tab close for a system people leave open all day, and a prompt that appears when it
 * is not wanted is one users learn to dismiss without reading.
 *
 * Two limits worth knowing, both imposed by browsers and not by this code:
 *   · the wording is the browser's own ("Leave site?" / "هل تريد مغادرة الموقع؟") — a page cannot
 *     supply custom text here, which is why this is a confirmation and not a styled dialog;
 *   · it also appears on a manual reload, since a page cannot tell a reload from a close.
 *
 * VITE_CONFIRM_ON_CLOSE overrides the scoping: `always` (any tab), `never` (off entirely).
 */
export function useConfirmClose(enabled: boolean) {
  useEffect(() => {
    const mode = import.meta.env.VITE_CONFIRM_ON_CLOSE ?? 'standalone'
    if (!enabled || mode === 'never') return
    if (mode !== 'always' && !isInstalled()) return

    const onBeforeUnload = (e: BeforeUnloadEvent) => {
      e.preventDefault()
      // Legacy browsers keyed off a non-empty returnValue; modern ones only need preventDefault.
      e.returnValue = ''
    }

    window.addEventListener('beforeunload', onBeforeUnload)
    return () => window.removeEventListener('beforeunload', onBeforeUnload)
  }, [enabled])
}
