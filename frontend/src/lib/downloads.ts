// Direct URL to the scan-agent installer (served by the API; anonymous so the login page can link it).
const apiBase = import.meta.env.VITE_API_URL ?? 'http://localhost:5105/api'

export const SCAN_AGENT_DOWNLOAD_URL = `${apiBase}/downloads/scan-agent`

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
