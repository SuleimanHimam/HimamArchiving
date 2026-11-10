import { useEffect, useState } from 'react'
import { Download } from 'lucide-react'
import { canInstall, onInstallAvailability, promptInstall } from '../lib/installApp'
import { DESKTOP_SHORTCUT_URL, isWindows } from '../lib/downloads'

/**
 * Offers to install the system as a desktop app.
 *
 * Two routes to the same window — no tab strip, no address bar, its own taskbar button:
 *
 *   · the browser's own install prompt, when the browser offers one. It is the better route: it
 *     adds Start-menu entries and the manifest's jump list.
 *   · a generated shortcut otherwise. The prompt requires a secure origin, so a LAN deployment
 *     served over plain http never gets one — which is most installations of this system, and
 *     was the case where this button used to render nothing at all.
 *
 * Still renders nothing where neither can work: already installed (the browser stops offering),
 * or not Windows (the shortcut is a .cmd).
 */
export default function InstallAppButton() {
  const [available, setAvailable] = useState(canInstall)

  useEffect(() => onInstallAvailability(() => setAvailable(canInstall())), [])

  // Already running in the installed window — there is nothing left to install.
  const standalone = typeof window !== 'undefined' &&
    window.matchMedia?.('(display-mode: standalone)').matches

  if (standalone) return null
  if (!available && !isWindows()) return null

  return (
    <button
      className="shell__install"
      title={available
        ? 'تثبيت التطبيق على سطح المكتب'
        : 'إنشاء اختصار على سطح المكتب — يفتح النظام في نافذته الخاصة'}
      onClick={() => {
        if (available) { void promptInstall(); return }
        // A plain navigation, not fetch(): the browser's own download handling is what saves the
        // file, and the endpoint is anonymous so no header has to travel with it.
        window.location.href = DESKTOP_SHORTCUT_URL
      }}
    >
      <Download size={15} aria-hidden />
      <span className="shell__install-label">تثبيت</span>
    </button>
  )
}
