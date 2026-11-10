// Direct URL to the scan-agent installer (served by the API; anonymous so the login page can link it).
const apiBase = import.meta.env.VITE_API_URL ?? 'http://localhost:5105/api'

export const SCAN_AGENT_DOWNLOAD_URL = `${apiBase}/downloads/scan-agent`

/** One-click installer for the desktop app window (a .cmd generated for this server's address).
 *  Always available: unlike the browser's own install prompt it does not need a secure origin,
 *  which is exactly the case on a LAN deployment served over plain http. */
export const DESKTOP_SHORTCUT_URL = `${apiBase}/downloads/desktop-shortcut`

/** Windows is the only platform the generated shortcut means anything on. */
export const isWindows = () => /Windows/i.test(navigator.userAgent)

// Whether the server actually has the installer available (avoids a dead link).
export async function scanAgentDownloadAvailable(): Promise<boolean> {
  try {
    const res = await fetch(`${apiBase}/downloads/scan-agent/available`)
    if (!res.ok) return false
    const data = (await res.json()) as { available: boolean }
    return data.available
  } catch {
    return false
  }
}
