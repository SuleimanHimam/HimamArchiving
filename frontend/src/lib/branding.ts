import { api } from './api'

export interface BrandingData {
  nameAr:       string
  nameEn:       string | null
  code:         string | null
  address:      string | null
  phone:        string | null
  email:        string | null
  logoBase64:   string | null
  colorPrimary: string | null
  colorAccent:  string | null
  colorSeal:    string | null
  colorBg:      string | null
  // Shell chrome — institution-wide defaults. Applied to --brand-* variables, which the
  // shell reads *behind* the per-browser --rail-*/--nav-* overrides from lib/appearance.ts,
  // so a user's personal colours still win over the institution's.
  colorRailBg:     string | null
  colorRailText:   string | null
  colorRailAccent: string | null
  colorNavBg:      string | null
  colorNavText:    string | null
  colorLoginBg:     string | null
  colorLoginAccent: string | null
}

/** The design system's own values — what each picker shows, and reverts to, when unset. */
export const BRANDING_DEFAULTS = {
  colorPrimary: '#1B4C9E',
  colorAccent:  '#ebe9e6',
  colorSeal:    '#9B2226',
  colorBg:      '#e6e6e6',
  colorRailBg:     '#0E2A5C',
  colorRailText:   '#F4ECD8',
  colorRailAccent: '#F5A623',
  colorNavBg:      '#FFFFFF',
  colorNavText:    '#222634',
  colorLoginBg:     '#0E2A5C',
  colorLoginAccent: '#2C6FD6',
} as const

export const EMPTY_BRANDING: BrandingData = {
  nameAr: '', nameEn: null, code: null, address: null, phone: null, email: null,
  logoBase64: null, colorPrimary: null, colorAccent: null, colorSeal: null, colorBg: null,
  colorRailBg: null, colorRailText: null, colorRailAccent: null,
  colorNavBg: null, colorNavText: null, colorLoginBg: null, colorLoginAccent: null,
}

export const brandingApi = {
  get: () => api.get<BrandingData>('/branding').then((r) => r.data),
  update: (body: BrandingData) => api.put<BrandingData>('/branding', body).then((r) => r.data),
}

let _branding: BrandingData = { ...EMPTY_BRANDING }
const _listeners = new Set<() => void>()

export function getBranding() { return _branding }

export function subscribeBranding(fn: () => void) {
  _listeners.add(fn)
  return () => _listeners.delete(fn)
}

export function applyBranding(b: BrandingData) {
  _branding = b
  const root = document.documentElement
  if (b.colorPrimary) {
    root.style.setProperty('--ink',      b.colorPrimary)
    root.style.setProperty('--ink-soft', adjustHex(b.colorPrimary, 20))
    root.style.setProperty('--ink-deep', adjustHex(b.colorPrimary, -15))
  } else {
    root.style.removeProperty('--ink')
    root.style.removeProperty('--ink-soft')
    root.style.removeProperty('--ink-deep')
  }
  if (b.colorSeal) {
    root.style.setProperty('--seal',      b.colorSeal)
    root.style.setProperty('--seal-deep', adjustHex(b.colorSeal, -20))
  } else {
    root.style.removeProperty('--seal')
    root.style.removeProperty('--seal-deep')
  }

  // NOTE: the Marasil design system now owns the background (--parchment*) and accent (--brass*)
  // tokens so they stay theme-aware (light/dark). Branding no longer pins them — otherwise its
  // inline styles would override the stylesheet and break dark mode. Always clear any stale values.
  for (const v of ['--brass', '--brass-soft', '--parchment', '--parchment-2', '--parchment-3'])
    root.style.removeProperty(v)

  // Shell chrome. Written to the --brand-* layer, never to --rail-*/--nav-*: those belong to
  // the per-browser appearance override, which must stay able to win (see appearance.ts).
  setVar('--brand-rail-bg', b.colorRailBg)
  setVar('--brand-rail-text', b.colorRailText)
  setVar('--brand-rail-accent', b.colorRailAccent)
  setVar('--brand-nav-bg', b.colorNavBg)
  setVar('--brand-nav-text', b.colorNavText)
  setVar('--brand-login-bg', b.colorLoginBg)
  setVar('--brand-login-accent', b.colorLoginAccent)
  // The login field's second accent is derived, not picked: one accent plus a hue-rotated
  // partner keeps the gradient reading as a pair without asking the admin for two colours.
  setVar('--brand-login-accent-2', b.colorLoginAccent ? shiftHue(b.colorLoginAccent, 55) : null)

  _listeners.forEach((fn) => fn())
}

function setVar(name: string, value: string | null) {
  const root = document.documentElement
  if (value) root.style.setProperty(name, value)
  else root.style.removeProperty(name)
}

export async function loadBranding() {
  try {
    const b = await brandingApi.get()
    applyBranding(b)
  } catch {
    // silently ignore — fall back to CSS defaults
  }
}

/** Rotates a hex colour's hue by `deg`, keeping its saturation and lightness. */
export function shiftHue(hex: string, deg: number): string {
  const n = parseInt(hex.replace('#', ''), 16)
  const r = ((n >> 16) & 0xff) / 255, g = ((n >> 8) & 0xff) / 255, b = (n & 0xff) / 255
  const max = Math.max(r, g, b), min = Math.min(r, g, b), d = max - min
  const l = (max + min) / 2
  const s = d === 0 ? 0 : d / (1 - Math.abs(2 * l - 1))
  let h = 0
  if (d !== 0) {
    if (max === r) h = ((g - b) / d) % 6
    else if (max === g) h = (b - r) / d + 2
    else h = (r - g) / d + 4
  }
  h = (((h * 60 + deg) % 360) + 360) % 360

  const c = (1 - Math.abs(2 * l - 1)) * s
  const x = c * (1 - Math.abs(((h / 60) % 2) - 1))
  const m = l - c / 2
  const [r2, g2, b2] =
    h < 60  ? [c, x, 0] : h < 120 ? [x, c, 0] : h < 180 ? [0, c, x] :
    h < 240 ? [0, x, c] : h < 300 ? [x, 0, c] : [c, 0, x]
  return `#${[r2, g2, b2].map((v) => Math.round((v + m) * 255).toString(16).padStart(2, '0')).join('')}`
}

function adjustHex(hex: string, amount: number): string {
  const n = parseInt(hex.replace('#', ''), 16)
  const r = Math.min(255, Math.max(0, (n >> 16) + amount))
  const g = Math.min(255, Math.max(0, ((n >> 8) & 0xff) + amount))
  const b = Math.min(255, Math.max(0, (n & 0xff) + amount))
  return `#${[r, g, b].map((v) => v.toString(16).padStart(2, '0')).join('')}`
}
