import { useSyncExternalStore } from 'react'
import { useTranslation } from 'react-i18next'
import { subscribeTablePrefs, getTablePrefs, resolveColumns } from '../lib/tableColumns'

/**
 * Per-user column layout for a data table. Reads the (cached) saved prefs and
 * merges them with the table's defaults. Re-renders when the user saves changes.
 */
export function useTableColumns(tableKey: string) {
  const { t } = useTranslation()
  useSyncExternalStore(subscribeTablePrefs, getTablePrefs)
  const all = resolveColumns(tableKey, t)
  const visibleSet = new Set(all.filter((c) => c.visible).map((c) => c.key))
  const labelMap = new Map(all.map((c) => [c.key, c.label]))
  return {
    columns: all.filter((c) => c.visible),   // ordered, visible-only
    isVisible: (key: string) => visibleSet.has(key),
    labelFor: (key: string) => labelMap.get(key) ?? key,
  }
}
