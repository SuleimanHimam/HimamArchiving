import { useCallback, useEffect, useMemo, useState } from 'react'
import { customFields, type CustomFieldDef } from './customFields'

/**
 * Custom fields on a record that does not exist yet.
 *
 * The values can only be stored once the record has an id, so the draft holds them in memory
 * while the form is being filled and `save(id)` writes them straight after the record is created.
 * Existing records use CustomFieldsPanel instead, which loads and saves against their own id.
 */
export interface CustomFieldsDraft {
  defs: CustomFieldDef[]
  values: Record<number, string>
  set: (fieldId: number, value: string) => void
  /** Persist the collected values against the freshly created record. No-op when nothing was filled. */
  save: (entityId: number) => Promise<void>
  hasFields: boolean
}

/** Loads the field definitions that apply to `documentTypeId` and tracks their values. */
export function useCustomFieldsDraft(entityType: string, documentTypeId: number | null): CustomFieldsDraft {
  const [defs, setDefs] = useState<CustomFieldDef[]>([])
  const [values, setValues] = useState<Record<number, string>>({})

  useEffect(() => {
    let cancelled = false
    if (documentTypeId === null) { setDefs([]); return }
    customFields.list(entityType, { documentTypeId })
      .then((d) => { if (!cancelled) setDefs(d.filter((x) => x.isActive)) })
      .catch(() => { if (!cancelled) setDefs([]) })
    return () => { cancelled = true }
  }, [entityType, documentTypeId])

  const set = useCallback((fieldId: number, value: string) => {
    setValues((m) => ({ ...m, [fieldId]: value }))
  }, [])

  const save = useCallback(async (entityId: number) => {
    // Only what the user can currently see is saved. Switching type mid-form swaps the visible
    // fields, and values left behind by the previous type would otherwise be written to a record
    // whose form never showed them.
    const visible = new Set(defs.map((d) => d.id))
    const filled = Object.entries(values)
      .filter(([id, v]) => v.trim() !== '' && visible.has(Number(id)))
    if (filled.length === 0) return
    await customFields.saveValues(entityType, entityId, Object.fromEntries(filled))
  }, [entityType, values, defs])

  return useMemo(
    () => ({ defs, values, set, save, hasFields: defs.length > 0 }),
    [defs, values, set, save],
  )
}
