// User-local device preferences (devices live on the user's own PC → stored per-browser).

const KEYS = {
  scanner: 'archiving.defaultScanner',
  printer:  'archiving.defaultPrinter',
  scanPrefs: 'archiving.scanPrefs',
} as const

/** How a scan should be acquired — mirrors the agent's /scan body (tools/scan-agent). */
export interface ScanPrefs {
  dpi: number
  color: 'blackwhite' | 'grayscale' | 'color'
  paper: 'a4' | 'a5' | 'letter' | 'legal'
  source: 'flatbed' | 'feeder'
  duplex: boolean
  /** Review pages in the agent's window before they are handed back. */
  preview: boolean
}

/** Matches the agent's own defaults, so an unconfigured browser and an unconfigured
 *  agent agree on what a plain scan means. */
export const DEFAULT_SCAN_PREFS: ScanPrefs = {
  dpi: 200, color: 'color', paper: 'a4', source: 'flatbed', duplex: false, preview: false,
}

export const scannerSettings = {
  // Scanner (TWAIN / WIA capture device)
  getScanner: (): string | null => localStorage.getItem(KEYS.scanner),
  setScanner: (name: string | null) => {
    if (name) localStorage.setItem(KEYS.scanner, name)
    else localStorage.removeItem(KEYS.scanner)
  },

  // Printer (Windows print-spooler queue)
  getPrinter: (): string | null => localStorage.getItem(KEYS.printer),
  setPrinter: (name: string | null) => {
    if (name) localStorage.setItem(KEYS.printer, name)
    else localStorage.removeItem(KEYS.printer)
  },

  // Scan acquisition preferences. Merged over the defaults so a stored blob written by an older
  // build (missing `duplex`, say) still yields a complete object.
  getScanPrefs: (): ScanPrefs => {
    try {
      const raw = localStorage.getItem(KEYS.scanPrefs)
      return raw ? { ...DEFAULT_SCAN_PREFS, ...(JSON.parse(raw) as Partial<ScanPrefs>) } : DEFAULT_SCAN_PREFS
    } catch { return DEFAULT_SCAN_PREFS }
  },
  setScanPrefs: (prefs: ScanPrefs) => {
    try { localStorage.setItem(KEYS.scanPrefs, JSON.stringify(prefs)) } catch { /* storage unavailable */ }
  },

  // Backward-compat alias (old code used .get() / .set())
  get: (): string | null => localStorage.getItem(KEYS.scanner),
  set: (name: string | null) => {
    if (name) localStorage.setItem(KEYS.scanner, name)
    else localStorage.removeItem(KEYS.scanner)
  },
}
