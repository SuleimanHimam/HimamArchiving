/* Appearance customization for the Marasil UI.
   Lets the user recolor the sidebar (background / text / accent), change the sidebar
   font, and override the system background + foreground. Persisted per-browser in
   localStorage and applied as CSS variables on <html>, so it overrides the design
   tokens live (the sidebar & body read these vars) without any React re-render. */

const KEY = 'marasil.appearance'

export interface Appearance {
  railBg: string | null      // sidebar background
  railText: string | null    // sidebar text colour
  railAccent: string | null  // sidebar icons + active bar
  railFont: string | null    // sidebar font-family (CSS value)
  appBg: string | null        // system page background
  appText: string | null      // system foreground (text)
}

export const EMPTY_APPEARANCE: Appearance = {
  railBg: null, railText: null, railAccent: null, railFont: null, appBg: null, appText: null,
}

// Defaults shown in the pickers (match the live Marasil tokens) when nothing is set.
// appBg tracks the system --m-bg so "reset appearance" returns to the real default.
export const APPEARANCE_DEFAULTS = {
  railBg: '#0E1A30', railText: '#F4ECD8', railAccent: '#C8A24B',
  appBg: '#FFFFFF', appText: '#222634',
}

export const SIDEBAR_FONTS: { label: string; value: string }[] = [
  { label: 'الافتراضي (Noto Sans Arabic)', value: '' },
  { label: 'Noto Sans Arabic', value: '"Noto Sans Arabic", sans-serif' },
  { label: 'Archivo', value: '"Archivo", sans-serif' },
  { label: 'Inter', value: '"Inter", sans-serif' },
  { label: 'IBM Plex Mono', value: '"IBM Plex Mono", monospace' },
  { label: 'Tahoma', value: 'Tahoma, "Noto Sans Arabic", sans-serif' },
  { label: 'النظام (System UI)', value: 'system-ui, sans-serif' },
]

let _appearance: Appearance = read()
const _listeners = new Set<() => void>()

export function getAppearance(): Appearance { return _appearance }

export function subscribeAppearance(fn: () => void) {
  _listeners.add(fn)
  return () => _listeners.delete(fn)
}

function read(): Appearance {
  try {
    const raw = localStorage.getItem(KEY)
    if (raw) return { ...EMPTY_APPEARANCE, ...(JSON.parse(raw) as Partial<Appearance>) }
  } catch { /* ignore malformed */ }
  return { ...EMPTY_APPEARANCE }
}

function setVar(name: string, value: string | null) {
  const root = document.documentElement
  if (value) root.style.setProperty(name, value)
  else root.style.removeProperty(name)
}

export function applyAppearance(a: Appearance, persist = true): void {
  _appearance = a
  setVar('--rail-bg', a.railBg)
  setVar('--rail-text', a.railText)
  setVar('--rail-text-active', a.railText)   // active/hover label uses the same picked colour
  setVar('--rail-accent', a.railAccent)
  setVar('--rail-font', a.railFont)
  // System background goes through a dedicated --app-bg (the body falls back to it) so it
  // survives branding.applyBranding(), which clears --parchment on startup. Foreground
  // overrides --text directly (branding never touches it), cascading to all themed text.
  setVar('--app-bg', a.appBg)
  setVar('--text', a.appText)
  if (persist) {
    try { localStorage.setItem(KEY, JSON.stringify(a)) } catch { /* quota / disabled */ }
  }
  _listeners.forEach((fn) => fn())
}

export function resetAppearance(): void {
  applyAppearance({ ...EMPTY_APPEARANCE })
}

// Apply any saved appearance immediately on import — before the shell first paints.
applyAppearance(_appearance, false)
