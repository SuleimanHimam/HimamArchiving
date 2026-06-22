import { useEffect, useState, useCallback } from 'react'
import { customFields, optionList, type CustomFieldDef } from '../lib/customFields'
import { useToast } from './toast'
import '../pages/documents/documents.css'

interface Props {
  entityType: string
  entityId: number
  canEdit?: boolean
}

/** Renders + edits the admin-defined custom fields for one record. Hidden if no active fields exist. */
export default function CustomFieldsPanel({ entityType, entityId, canEdit = true }: Props) {
  const toast = useToast()
  const [defs, setDefs] = useState<CustomFieldDef[]>([])
  const [vals, setVals] = useState<Record<number, string>>({})
  const [busy, setBusy] = useState(false)
  const [dirty, setDirty] = useState(false)

  const load = useCallback(async () => {
    try {
      const [d, v] = await Promise.all([customFields.list(entityType), customFields.values(entityType, entityId)])
      setDefs(d.filter((x) => x.isActive))
      const map: Record<number, string> = {}
      v.forEach((x) => { map[x.fieldId] = x.value })
      setVals(map); setDirty(false)
    } catch { /* ignore — panel just stays empty */ }
  }, [entityType, entityId])
  useEffect(() => { load() }, [load])

  function set(id: number, value: string) { setVals((m) => ({ ...m, [id]: value })); setDirty(true) }

  async function save() {
    setBusy(true)
    try { await customFields.saveValues(entityType, entityId, vals); setDirty(false); toast.success('تم حفظ الحقول') }
    catch { toast.error('تعذّر حفظ الحقول') }
    finally { setBusy(false) }
  }

  if (defs.length === 0) return null

  return (
    <section className="doc-card" style={{ marginTop: '1rem' }}>
      <h3 className="detail-h3">حقول مخصصة</h3>
      <div className="form-grid">
        {defs.map((f) => {
          const v = vals[f.id] ?? ''
          return (
            <label className="field" key={f.id}>
              <span>{f.label}</span>
              {!canEdit ? (
                <input value={v} readOnly placeholder="—" />
              ) : f.fieldType === 1 ? (
                <input type="number" value={v} dir="ltr" onChange={(e) => set(f.id, e.target.value)} />
              ) : f.fieldType === 2 ? (
                <input type="date" value={v} dir="ltr" onChange={(e) => set(f.id, e.target.value)} />
              ) : f.fieldType === 3 ? (
                <select value={v} onChange={(e) => set(f.id, e.target.value)}>
                  <option value="">—</option>
                  {optionList(f).map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              ) : (
                <input value={v} onChange={(e) => set(f.id, e.target.value)} />
              )}
            </label>
          )
        })}
      </div>
      {canEdit && (
        <div className="form-actions" style={{ marginTop: '.8rem' }}>
          <button className="btn btn-primary" disabled={busy || !dirty} onClick={save}>حفظ الحقول</button>
        </div>
      )}
    </section>
  )
}
