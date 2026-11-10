// Client for the local scan agent running on the user's PC.
//
// The cloud SPA cannot reach a USB/TWAIN scanner directly (browser sandbox), so a small agent
// runs on the user's machine and exposes scanning over loopback. Browsers permit calls from an
// HTTPS page to http://127.0.0.1, so this works even when the system is hosted on the cloud.
//
// Contract (see tools/scan-agent):
//   GET  {base}/status          -> { status, authorized, requiresToken, session{..}, scanners[], printers[] }
//   POST {base}/scan {format}   -> binary body (application/pdf | image/jpeg) of the scanned page(s)
//   POST {base}/print           -> body = file bytes
//
// ── Remote Desktop ───────────────────────────────────────────────────────────────────────────
// On a Terminal Server the page runs *inside* an RDP session, so 127.0.0.1 is the server, and every
// session on that server shares one loopback and one port space. Two consequences shape this file:
//
//   1. The port is not fixed. Each session's agent takes the first free port in a range, so the
//      client has to find its agent rather than assume 8765. `discover()` probes the range.
//   2. Several agents answer on that range — one per signed-in user — and they are indistinguishable
//      by address. The agent that accepts our token is ours; the rest reject it. The token is put
//      into this browser's localStorage once, by the agent opening the pairing page from inside the
//      user's own session, which is what ties the two together.
//
// On an ordinary single-user PC none of this is visible: the agent sits on 8765, reports
// `requiresToken: false`, and the very first probe succeeds with no token at all.

const DEFAULT_PORT = 8765
const PORT_RANGE = 20            // must match AgentServer.PortRange
const TOKEN_KEY = 'archiving.scanAgentToken'
const PORT_KEY = 'archiving.scanAgentPort'

/** An explicit override pins the agent and turns discovery off entirely — for a bespoke deployment
 *  that runs the agent somewhere unusual and knows exactly where. */
const OVERRIDE: string | undefined = import.meta.env.VITE_SCAN_AGENT_URL

export interface ScannerCapabilities {
  flatbed: boolean
  feeder: boolean
  duplex: boolean
}

/** Which Windows session answered. Present from agent v1.3+; undefined from older agents. */
export interface AgentSession {
  id: number
  user: string
  machine: string
  /** True when the agent is itself inside a Remote Desktop session. */
  remote: boolean
}

export interface ScanAgentStatus {
  status: string
  scanners: string[]   // TWAIN / WIA capture devices
  printers?: string[]  // Windows print-spooler queues (agent v1.1+)
  mock: boolean
  // agent v1.2+ — absent on older agents, so every consumer must tolerate undefined.
  capabilities?: ScannerCapabilities
  dpiOptions?: number[]
  colorModes?: string[]
  paperSizes?: string[]
  maxPages?: number
  // agent v1.3+ — the Remote Desktop fields.
  /** False when an agent answered but this browser is not the one paired with it. */
  authorized?: boolean
  /** True when the agent is on a shared host and will not act without a token. */
  requiresToken?: boolean
  session?: AgentSession
  /** Where this status came from. Filled in by the client, not by the agent. */
  baseUrl?: string
}

/** Acquisition options; all optional — omitting them gives one flatbed page, as before. */
export interface ScanRequest {
  scanner?: string
  dpi?: number
  color?: 'blackwhite' | 'grayscale' | 'color'
  paper?: 'a4' | 'a5' | 'letter' | 'legal'
  source?: 'flatbed' | 'feeder'
  duplex?: boolean
  /** Cap on pages pulled from the feeder. */
  maxPages?: number
  /** Review pages in the agent's own window before it answers. */
  preview?: boolean
}

/** Thrown when an agent answered but refused us — the browser is not paired with this session. */
export class ScanAgentUnauthorizedError extends Error {
  constructor(message?: string) {
    super(message || 'هذا الوكيل يخصّ جلسة أخرى. افتح نافذة الوكيل واضغط «ربط المتصفح».')
    this.name = 'ScanAgentUnauthorizedError'
  }
}

/**
 * Why the browser may be refusing to let this page reach 127.0.0.1 at all.
 *
 * Private Network Access: a page is only allowed to call into a more private address space than
 * its own when it is a secure context. Opened from the server's address over plain http —
 * http://46.43.95.122:5080, say — the page is a non-secure *public* origin and Chromium blocks
 * every request to the loopback agent before it leaves the browser. Nothing reaches the agent, so
 * the symptom is an agent that is plainly running and still reports no devices.
 *
 * Opened as http://localhost:5080 on the machine itself there is no such gap — page and agent are
 * in the same address space — which is why the same build works there and only there.
 *
 * Returns null when the browser is not the obstacle, so the caller can fall back to the ordinary
 * "install and run the agent" advice.
 */
export function localAgentBlockedByBrowser(): 'insecure-remote-origin' | null {
  if (typeof window === 'undefined') return null
  if (window.isSecureContext) return null            // https (or localhost) — the preflight route works
  const host = window.location.hostname
  if (host === 'localhost' || host === '127.0.0.1' || host === '[::1]' || host === '::1') return null
  return 'insecure-remote-origin'
}

function getToken(): string | null {
  try { return localStorage.getItem(TOKEN_KEY) } catch { return null }
}

function urlFor(port: number): string {
  return `http://127.0.0.1:${port}`
}

/** The port that answered last time. Tried first so the common case costs one request, not twenty. */
function cachedPort(): number | null {
  try {
    const raw = localStorage.getItem(PORT_KEY)
    const n = raw ? Number(raw) : NaN
    return Number.isInteger(n) ? n : null
  } catch { return null }
}

function rememberPort(port: number) {
  try { localStorage.setItem(PORT_KEY, String(port)) } catch { /* storage unavailable */ }
}

async function probe(port: number, timeoutMs: number, scanner?: string): Promise<ScanAgentStatus | null> {
  const token = getToken()
  const qs = new URLSearchParams()
  if (token) qs.set('token', token)          // query, not header: keeps the probe a CORS-simple
  if (scanner) qs.set('scanner', scanner)    // request, so twenty of them cost twenty round trips
  const suffix = qs.toString() ? `?${qs}` : ''

  try {
    const ctrl = new AbortController()
    const t = setTimeout(() => ctrl.abort(), timeoutMs)
    const res = await fetch(`${urlFor(port)}/status${suffix}`, { signal: ctrl.signal })
    clearTimeout(t)
    if (!res.ok) return null
    const body = (await res.json()) as ScanAgentStatus
    return { ...body, baseUrl: urlFor(port) }
  } catch {
    return null
  }
}

/** The agent this browser should be talking to, or null if none answered anywhere in the range.
 *
 *  Preference order matters on a shared host: an agent that accepts our token is unambiguously ours,
 *  so it wins even if a stranger's agent answered on a lower port. Only when nothing accepts the
 *  token do we fall back to reporting a rejecting agent, so the UI can say "you need to pair"
 *  instead of the much less helpful "no scanner found". */
async function discover(timeoutMs: number, scanner?: string): Promise<ScanAgentStatus | null> {
  if (OVERRIDE) {
    // Pinned deployment: one address, no searching, and a failure is a real failure.
    const token = getToken()
    const qs = new URLSearchParams()
    if (token) qs.set('token', token)
    if (scanner) qs.set('scanner', scanner)
    try {
      const ctrl = new AbortController()
      const t = setTimeout(() => ctrl.abort(), timeoutMs)
      const res = await fetch(`${OVERRIDE}/status${qs.toString() ? `?${qs}` : ''}`, { signal: ctrl.signal })
      clearTimeout(t)
      if (!res.ok) return null
      return { ...((await res.json()) as ScanAgentStatus), baseUrl: OVERRIDE }
    } catch { return null }
  }

  // Fast path: whoever answered last time, if they still accept us.
  const cached = cachedPort()
  if (cached !== null) {
    const hit = await probe(cached, timeoutMs, scanner)
    if (hit && hit.authorized !== false) return hit
  }

  const ports: number[] = []
  for (let p = DEFAULT_PORT; p < DEFAULT_PORT + PORT_RANGE; p++) if (p !== cached) ports.push(p)

  const results = await Promise.all(ports.map((p) => probe(p, timeoutMs, scanner)))
  const answered = results.filter((r): r is ScanAgentStatus => r !== null)
  if (answered.length === 0) return null

  const mine = answered.find((r) => r.authorized !== false)
  const chosen = mine ?? answered[0]
  if (mine) {
    const port = Number(new URL(mine.baseUrl!).port)
    if (Number.isInteger(port)) rememberPort(port)
  }
  return chosen
}

// The address resolved by the last successful discovery. Requests reuse it so a scan does not
// re-probe the whole range; a 401 or a network error clears it and the next call searches again.
let resolvedBase: string | null = null

async function baseFor(timeoutMs = 1500): Promise<string> {
  if (resolvedBase) return resolvedBase
  const status = await discover(timeoutMs)
  if (!status?.baseUrl) throw new Error('لم يُعثر على وكيل المسح الضوئي على هذا الجهاز.')
  if (status.authorized === false) throw new ScanAgentUnauthorizedError()
  resolvedBase = status.baseUrl
  return resolvedBase
}

function authHeaders(): Record<string, string> {
  const token = getToken()
  return token ? { 'X-Agent-Token': token } : {}
}

export const scanAgent = {
  /** The address in use, or the one that will be tried first. Display only — call `status()` to
   *  find out what is actually there. */
  get baseUrl(): string {
    return resolvedBase ?? OVERRIDE ?? urlFor(cachedPort() ?? DEFAULT_PORT)
  },

  // ── pairing ────────────────────────────────────────────────────────────────
  getToken,

  /** Stores the token this browser will present. Called by the pairing page the agent opens, and by
   *  the manual paste box in scanner settings. Clears the resolved address so the next call rediscovers
   *  — the agent that accepts the new token is very likely a different one from the last. */
  setToken(token: string | null) {
    try {
      if (token) localStorage.setItem(TOKEN_KEY, token.trim())
      else localStorage.removeItem(TOKEN_KEY)
    } catch { /* storage unavailable */ }
    resolvedBase = null
    try { localStorage.removeItem(PORT_KEY) } catch { /* ignore */ }
  },

  /** Forget the discovered address so the next request searches the range again. */
  reset() {
    resolvedBase = null
    try { localStorage.removeItem(PORT_KEY) } catch { /* ignore */ }
  },

  // Probe the agent quickly; returns null if it isn't installed/running. A non-null result with
  // `authorized: false` means an agent is there but belongs to another session.
  async status(timeoutMs = 1500, scanner?: string): Promise<ScanAgentStatus | null> {
    const found = await discover(timeoutMs, scanner)
    resolvedBase = found?.authorized === false ? null : (found?.baseUrl ?? null)
    return found
  },

  // Acquire a scan; resolves to the file blob, a suggested filename, and how many pages came back.
  // `opts` accepts a plain scanner name for the original two-argument call shape.
  async scan(
    format: 'pdf' | 'jpeg' = 'pdf',
    opts?: string | ScanRequest,
  ): Promise<{ blob: Blob; fileName: string; pages: number }> {
    const req: ScanRequest = typeof opts === 'string' ? { scanner: opts } : (opts ?? {})
    const base = await baseFor()
    const res = await fetch(`${base}/scan`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...authHeaders() },
      body: JSON.stringify({ format, ...req }),
    })
    if (res.status === 401) {
      // The agent we had is someone else's now — most likely it restarted and the ports shuffled.
      resolvedBase = null
      throw new ScanAgentUnauthorizedError(await res.text().catch(() => '') || undefined)
    }
    if (!res.ok) {
      const msg = await res.text().catch(() => '')
      // 499 is the agent's "the user cancelled in the preview window" — not a failure to report
      // as an error banner, so it is given a recognisable message.
      throw new Error(msg || `فشل المسح الضوئي (${res.status})`)
    }
    const blob = await res.blob()
    const ext = format === 'pdf' ? 'pdf' : 'jpg'
    const stamp = new Date().toISOString().replace(/[:.]/g, '-')
    const pages = Number(res.headers.get('X-Scan-Pages') ?? '1') || 1
    return { blob, fileName: `scan-${stamp}.${ext}`, pages }
  },

  // Print a file (PDF or image) on a local printer queue via the agent.
  async print(file: Blob, opts: { printer?: string | null; ext?: string } = {}): Promise<{ status: string; printer: string; pages: number }> {
    const params = new URLSearchParams()
    if (opts.printer) params.set('printer', opts.printer)
    params.set('ext', (opts.ext ?? 'pdf').replace(/^\./, '').toLowerCase())
    const base = await baseFor()
    const res = await fetch(`${base}/print?${params.toString()}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/octet-stream', ...authHeaders() },
      body: file,
    })
    if (res.status === 401) {
      resolvedBase = null
      throw new ScanAgentUnauthorizedError(await res.text().catch(() => '') || undefined)
    }
    if (!res.ok) {
      const msg = await res.text().catch(() => '')
      throw new Error(msg || `فشل الطباعة (${res.status})`)
    }
    return (await res.json()) as { status: string; printer: string; pages: number }
  },
}
