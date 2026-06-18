// User-local scanner preference (the scanner lives on the user's own PC, so this is per-browser).

const KEY = 'archiving.defaultScanner'

export const scannerSettings = {
  get: (): string | null => localStorage.getItem(KEY),
  set: (name: string | null) => {
    if (name) localStorage.setItem(KEY, name)
    else localStorage.removeItem(KEY)
  },
}
