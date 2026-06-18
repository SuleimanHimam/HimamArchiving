// Client for the local scan agent running on the user's PC.
//
// The cloud SPA cannot reach a USB/TWAIN scanner directly (browser sandbox), so a small agent
// runs on the user's machine and exposes scanning over loopback. Browsers permit calls from an
// HTTPS page to http://127.0.0.1, so this works even when the system is hosted on the cloud.
//
// Contract (see tools/scan-agent):
//   GET  {base}/status          -> { status: "ok", scanners: string[] }
//   POST {base}/scan {format}   -> binary body (application/pdf | image/jpeg) of the scanned page(s)

const BASE = import.meta.env.VITE_SCAN_AGENT_URL ?? 'http://127.0.0.1:8765'

export interface ScanAgentStatus { status: string; scanners: string[]; mock: boolean }

export const scanAgent = {
  baseUrl: BASE,

  // Probe the agent quickly; returns null if it isn't installed/running.
  async status(timeoutMs = 1500): Promise<ScanAgentStatus | null> {
    try {
      const ctrl = new AbortController()
      const t = setTimeout(() => ctrl.abort(), timeoutMs)
      const res = await fetch(`${BASE}/status`, { signal: ctrl.signal })
      clearTimeout(t)
      if (!res.ok) return null
      return (await res.json()) as ScanAgentStatus
    } catch {
      return null
    }
  },

  // Acquire a scan; resolves to the file blob + a suggested filename.
  async scan(format: 'pdf' | 'jpeg' = 'pdf', scanner?: string): Promise<{ blob: Blob; fileName: string }> {
    const res = await fetch(`${BASE}/scan`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ format, scanner }),
    })
    if (!res.ok) {
      const msg = await res.text().catch(() => '')
      throw new Error(msg || `فشل المسح الضوئي (${res.status})`)
    }
    const blob = await res.blob()
    const ext = format === 'pdf' ? 'pdf' : 'jpg'
    const stamp = new Date().toISOString().replace(/[:.]/g, '-')
    return { blob, fileName: `scan-${stamp}.${ext}` }
  },
}
