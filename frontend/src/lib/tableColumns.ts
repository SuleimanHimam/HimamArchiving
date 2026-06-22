import { api } from './api'
import type { TFunction } from 'i18next'
import { customFields } from './customFields'

// A column definition: stable key + label (i18n key or literal). `defaultHidden`
// columns are extra data fields, off by default, that a user can add via show/hide.
export interface ColDef { key: string; labelKey?: string; label?: string; defaultHidden?: boolean }
// A saved per-user override entry (order is significant).
export interface ColPref { key: string; label?: string; hidden?: boolean }
// A resolved column ready for rendering.
export interface ResolvedCol { key: string; label: string; visible: boolean }

// Registry of customizable tables → their columns (in default order).
export const TABLE_DEFS: Record<string, ColDef[]> = {
  documents: [
    { key: 'title', labelKey: 'documents.columns.title' },
    { key: 'type', labelKey: 'documents.columns.type' },
    { key: 'confidentiality', labelKey: 'documents.columns.confidentiality' },
    { key: 'status', labelKey: 'documents.columns.status' },
    { key: 'location', labelKey: 'documents.columns.location' },
    { key: 'date', labelKey: 'documents.columns.date' },
    // Extra data fields (off by default):
    { key: 'documentNumber', label: 'رقم الوثيقة', defaultHidden: true },
    { key: 'documentDate', label: 'تاريخ الوثيقة', defaultHidden: true },
    { key: 'version', label: 'الإصدار', defaultHidden: true },
    { key: 'box', label: 'رقم الصندوق', defaultHidden: true },
    { key: 'file', label: 'رقم الملف', defaultHidden: true },
    { key: 'createdAt', label: 'تاريخ الإضافة', defaultHidden: true },
  ],
  incoming: [
    { key: 'number', labelKey: 'incoming.columns.number' },
    { key: 'sender', labelKey: 'incoming.columns.sender' },
    { key: 'subject', labelKey: 'incoming.columns.subject' },
    { key: 'confidentiality', labelKey: 'incoming.columns.confidentiality' },
    { key: 'priority', labelKey: 'incoming.columns.priority' },
    { key: 'status', labelKey: 'incoming.columns.status' },
    { key: 'date', labelKey: 'incoming.columns.date' },
    { key: 'createdAt', label: 'تاريخ التسجيل', defaultHidden: true },
  ],
  outgoing: [
    { key: 'number', labelKey: 'outgoing.columns.number' },
    { key: 'recipient', labelKey: 'outgoing.columns.recipient' },
    { key: 'subject', labelKey: 'outgoing.columns.subject' },
    { key: 'confidentiality', labelKey: 'outgoing.columns.confidentiality' },
    { key: 'priority', labelKey: 'outgoing.columns.priority' },
    { key: 'status', labelKey: 'outgoing.columns.status' },
    { key: 'date', labelKey: 'outgoing.columns.date' },
    { key: 'createdAt', label: 'تاريخ الإنشاء', defaultHidden: true },
  ],
  archiveLocations: [
    { key: 'name', labelKey: 'archive.columns.name' },
    { key: 'type', labelKey: 'archive.columns.type' },
    { key: 'code', labelKey: 'archive.columns.code' },
    { key: 'status', labelKey: 'archive.columns.status' },
    { key: 'rfidTag', label: 'بطاقة RFID', defaultHidden: true },
  ],
  archiveDocuments: [
    { key: 'document', labelKey: 'archive.columns.document' },
    { key: 'location', labelKey: 'archive.columns.location' },
    { key: 'box', labelKey: 'archive.columns.box' },
    { key: 'file', labelKey: 'archive.columns.file' },
    { key: 'title', label: 'عنوان الوثيقة', defaultHidden: true },
    { key: 'archivedAt', label: 'تاريخ الأرشفة', defaultHidden: true },
    { key: 'notes', label: 'ملاحظات', defaultHidden: true },
  ],
}

// Resolve a column's default display label (literal label wins over an i18n key).
export function defaultLabel(d: ColDef, t: TFunction): string {
  return d.label ?? (d.labelKey ? t(d.labelKey) : d.key)
}

// ---- Admin-defined custom fields surfaced as addable columns ----
// Maps a table to the custom-field entity type whose fields become columns (key `cf_<id>`).
const ENTITY_FOR_TABLE: Record<string, string> = { documents: 'Document' }
let _customCols: Record<string, ColDef[]> = {}

export async function loadCustomFieldCols() {
  const entries = Object.entries(ENTITY_FOR_TABLE)
  const results = await Promise.all(entries.map(([, et]) => customFields.list(et).catch(() => [])))
  const cols: Record<string, ColDef[]> = {}
  entries.forEach(([tableKey], i) => {
    cols[tableKey] = results[i].filter((f) => f.isActive)
      .map((f) => ({ key: `cf_${f.id}`, label: f.label, defaultHidden: true }))
  })
  _customCols = cols
  emit()
}

// Static defaults + any custom-field columns for this table.
export function tableDefs(tableKey: string): ColDef[] {
  return [...(TABLE_DEFS[tableKey] ?? []), ...(_customCols[tableKey] ?? [])]
}

// Friendly names for each table (shown in the settings panel).
export const TABLE_LABELS: Record<string, string> = {
  documents: 'الوثائق',
  incoming: 'الوارد',
  outgoing: 'الصادر',
  archiveLocations: 'الأرشيف — مواقع الحفظ',
  archiveDocuments: 'الأرشيف — البنود المؤرشفة',
}

let _cache: Record<string, ColPref[]> = {}
let _loaded = false
const listeners = new Set<() => void>()

export function subscribeTablePrefs(fn: () => void) { listeners.add(fn); return () => { listeners.delete(fn) } }
function emit() { listeners.forEach((fn) => fn()) }

export function getTablePrefs(): Record<string, ColPref[]> { return _cache }

export async function loadTablePrefs() {
  try {
    const map = await api.get<Record<string, string>>('/table-columns').then((r) => r.data)
    const parsed: Record<string, ColPref[]> = {}
    for (const k of Object.keys(map)) {
      try { const v = JSON.parse(map[k]); if (Array.isArray(v)) parsed[k] = v } catch { /* ignore */ }
    }
    _cache = parsed
  } catch { _cache = {} }
  _loaded = true
  emit()
}

export async function saveTablePref(tableKey: string, prefs: ColPref[]) {
  await api.put(`/table-columns/${tableKey}`, { configJson: JSON.stringify(prefs) })
  _cache = { ..._cache, [tableKey]: prefs }
  emit()
}

export function isLoaded() { return _loaded }

// Merge defaults with a user's saved prefs into an ordered, resolved column list.
export function resolveColumns(tableKey: string, t: TFunction, prefs?: ColPref[]): ResolvedCol[] {
  const defs = tableDefs(tableKey)
  const known = new Set(defs.map((d) => d.key))
  const saved = (prefs ?? _cache[tableKey] ?? []).filter((p) => known.has(p.key))
  const savedMap = new Map(saved.map((p) => [p.key, p]))
  // Order: saved order first, then any default columns not present in saved.
  const order = saved.length
    ? [...saved.map((p) => p.key), ...defs.map((d) => d.key).filter((k) => !savedMap.has(k))]
    : defs.map((d) => d.key)
  const seen = new Set<string>()
  return order
    .filter((k) => known.has(k) && !seen.has(k) && (seen.add(k), true))
    .map((k) => {
      const d = defs.find((x) => x.key === k)!
      const o = savedMap.get(k)
      // A saved entry fully controls visibility; defaultHidden only applies with no entry.
      const visible = o ? !o.hidden : !d.defaultHidden
      return { key: k, label: (o?.label && o.label.trim()) || defaultLabel(d, t), visible }
    })
}
