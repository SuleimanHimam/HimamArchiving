/* Gregorian date rendering (§14.2, §16.2 C20).
 *
 * The whole system reads Gregorian — no calendar preference, no Hijri. Note that
 * `toLocaleString('ar-SA')` silently derives Hijri from the locale, so date tags
 * are pinned to `ca-gregory` here rather than left to the language default.
 *
 * All timestamps on the wire are UTC ISO-8601 (§0.2). Conversion happens here,
 * at the presentation layer, and nowhere else. */

const LEGACY_KEY = 'sanad.calendar'

/** Drop the old per-user calendar choice so a stored 'hijri' can't resurface. */
try { localStorage.removeItem(LEGACY_KEY) } catch { /* private mode / no storage */ }

/** Force Western (Latin) digits — 0-9 — regardless of language. The whole system
 * uses Latin numerals; Arabic keeps its script for month/weekday names, digits stay Latin.
 * Appends the `nu-latn` Unicode extension, merging into an existing `-u-` block if present. */
export function withLatnDigits(locale: string): string {
  if (/-nu-/.test(locale)) return locale
  return /-u-/.test(locale) ? `${locale}-nu-latn` : `${locale}-u-nu-latn`
}

/** BCP-47 tag for the UI language, pinned to the Gregorian calendar and Latin digits. */
function localeTag(lang: string): string {
  return `${lang.startsWith('ar') ? 'ar' : 'en'}-u-ca-gregory-nu-latn`
}

const DATE_OPTS: Intl.DateTimeFormatOptions = { year: 'numeric', month: 'short', day: 'numeric' }
const TIME_OPTS: Intl.DateTimeFormatOptions = { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false }

/** Date only, in the user's language. */
export function formatDate(iso: string | null | undefined, lang: string): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleDateString(localeTag(lang), DATE_OPTS)
}

/** Date + time. */
export function formatDateTime(iso: string | null | undefined, lang: string): string {
  if (!iso) return '—'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleString(localeTag(lang), { ...DATE_OPTS, ...TIME_OPTS })
}
