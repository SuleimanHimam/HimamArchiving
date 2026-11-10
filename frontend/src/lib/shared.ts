/* Cross-cutting types and labels used across modules. These previously lived in
 * lib/incomingMail.ts and were imported app-wide (documents, archive, destruction,
 * external, users); they were relocated here when the correspondence-mail domain
 * was removed, so the rest of the app no longer depends on a deleted module. */

/** 0 Public · 1 Internal · 2 Confidential · 3 HighlyConfidential */
export type Confidentiality = 0 | 1 | 2 | 3

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export const CONFIDENTIALITY_LABELS: Record<string, { ar: string; cls: string }> = {
  Public: { ar: 'عام', cls: 'public' },
  Internal: { ar: 'داخلي', cls: 'internal' },
  Confidential: { ar: 'سري', cls: 'confidential' },
  HighlyConfidential: { ar: 'سري للغاية', cls: 'secret' },
}

/** Enum order — index is the level, matching the catalogue's sortOrder. */
export const CONFIDENTIALITY_ORDER = ['Public', 'Internal', 'Confidential', 'HighlyConfidential'] as const

/**
 * Renames the four levels from أنواع التصنيف الأمني in Settings.
 *
 * The names were hardcoded here while an administrator could rename them in Settings, so the
 * screen edited a catalogue nothing read. Everything that displays a clearance already funnels
 * through `clearanceLabel`, so overriding the table is enough to make the rename real. Levels
 * beyond these four cannot be applied this way: the field is a 0–3 enum on both DiwanMail and
 * Document, so a fifth row in the catalogue has no value to store.
 */
export function applyClassificationLabels(rows: { nameAr: string; sortOrder: number }[]) {
  for (const row of rows) {
    const key = CONFIDENTIALITY_ORDER[row.sortOrder]
    if (key && row.nameAr?.trim()) CONFIDENTIALITY_LABELS[key].ar = row.nameAr.trim()
  }
}

/**
 * Arabic label for a clearance/classification enum, falling back to the raw value.
 * The API sends PascalCase enum names ("HighlyConfidential"); rendering those
 * straight into an Arabic-first UI is the most visible polish defect there is,
 * so anywhere a clearance is displayed should go through here.
 */
export const clearanceLabel = (v: string | null | undefined): string =>
  (v && CONFIDENTIALITY_LABELS[v]?.ar) || v || '—'

/** Matching CSS modifier for the badge that carries a clearance. */
export const clearanceClass = (v: string | null | undefined): string =>
  (v && CONFIDENTIALITY_LABELS[v]?.cls) || 'internal'
