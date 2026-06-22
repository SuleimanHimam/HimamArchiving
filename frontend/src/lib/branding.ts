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
}

export const brandingApi = {
  get: () => api.get<BrandingData>('/branding').then((r) => r.data),
  update: (body: BrandingData) => api.put<BrandingData>('/branding', body).then((r) => r.data),
}

let _branding: BrandingData = {
  nameAr: '', nameEn: null, code: null, address: null, phone: null, email: null,
  logoBase64: null, colorPrimary: null, colorAccent: null, colorSeal: null, colorBg: null,
}
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
  if (b.colorAccent) {
    root.style.setProperty('--brass',      b.colorAccent)
    root.style.setProperty('--brass-soft', adjustHex(b.colorAccent, 18))
  } else {
    root.style.removeProperty('--brass')
    root.style.removeProperty('--brass-soft')
  }
  if (b.colorSeal) {
    root.style.setProperty('--seal',      b.colorSeal)
    root.style.setProperty('--seal-deep', adjustHex(b.colorSeal, -20))
  } else {
    root.style.removeProperty('--seal')
    root.style.removeProperty('--seal-deep')
  }
  if (b.colorBg) {
    root.style.setProperty('--parchment',   b.colorBg)
    root.style.setProperty('--parchment-2', adjustHex(b.colorBg, -9))
    root.style.setProperty('--parchment-3', adjustHex(b.colorBg, -18))
  } else {
    root.style.removeProperty('--parchment')
    root.style.removeProperty('--parchment-2')
    root.style.removeProperty('--parchment-3')
  }
  _listeners.forEach((fn) => fn())
}

export async function loadBranding() {
  try {
    const b = await brandingApi.get()
    applyBranding(b)
  } catch {
    // silently ignore — fall back to CSS defaults
  }
}

function adjustHex(hex: string, amount: number): string {
  const n = parseInt(hex.replace('#', ''), 16)
  const r = Math.min(255, Math.max(0, (n >> 16) + amount))
  const g = Math.min(255, Math.max(0, ((n >> 8) & 0xff) + amount))
  const b = Math.min(255, Math.max(0, (n & 0xff) + amount))
  return `#${[r, g, b].map((v) => v.toString(16).padStart(2, '0')).join('')}`
}
