// User-local device preferences (devices live on the user's own PC → stored per-browser).

const KEYS = {
  scanner: 'archiving.defaultScanner',
  printer:  'archiving.defaultPrinter',
} as const

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

  // Backward-compat alias (old code used .get() / .set())
  get: (): string | null => localStorage.getItem(KEYS.scanner),
  set: (name: string | null) => {
    if (name) localStorage.setItem(KEYS.scanner, name)
    else localStorage.removeItem(KEYS.scanner)
  },
}
