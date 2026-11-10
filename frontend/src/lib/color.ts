/* Small colour helpers for the branding editor.
   Kept framework-free so both the settings UI and any preview can use them. */

export interface Rgb { r: number; g: number; b: number }

/** Parses #rgb / #rrggbb. Returns null for anything else. */
export function parseHex(hex: string): Rgb | null {
  const m = /^#?([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(hex.trim())
  if (!m) return null
  let h = m[1]
  if (h.length === 3) h = h.split('').map((c) => c + c).join('')
  const n = parseInt(h, 16)
  return { r: (n >> 16) & 0xff, g: (n >> 8) & 0xff, b: n & 0xff }
}

export const isHex = (v: string) => parseHex(v) !== null

/** WCAG relative luminance. */
export function luminance({ r, g, b }: Rgb): number {
  const f = (v: number) => {
    const c = v / 255
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
  }
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b)
}

/** WCAG 2.1 contrast ratio, 1–21. Returns null if either colour is unparseable. */
export function contrastRatio(a: string, b: string): number | null {
  const ca = parseHex(a), cb = parseHex(b)
  if (!ca || !cb) return null
  const la = luminance(ca), lb = luminance(cb)
  return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05)
}

export type ContrastGrade = 'AAA' | 'AA' | 'AA-large' | 'fail'

/** Grades a ratio for normal body text (AA 4.5, AAA 7); 3.0 passes for large text only. */
export function grade(ratio: number): ContrastGrade {
  if (ratio >= 7) return 'AAA'
  if (ratio >= 4.5) return 'AA'
  if (ratio >= 3) return 'AA-large'
  return 'fail'
}

/** Black or white — whichever reads better on the given background. */
export function readableOn(bg: string): '#000000' | '#ffffff' {
  const c = parseHex(bg)
  if (!c) return '#000000'
  return luminance(c) > 0.42 ? '#000000' : '#ffffff'
}
