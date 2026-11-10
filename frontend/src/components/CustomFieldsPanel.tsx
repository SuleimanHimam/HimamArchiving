import { useEffect, useState, useCallback } from 'react'
import { customFields, optionList, type CustomFieldDef } from '../lib/customFields'
import { LabeledInput, LabeledSelect } from './ui/labeled-input'
import { useToast } from './toast'
import '../pages/documents/documents.css'

interface Props {
  entityType: string
  entityId: number
  canEdit?: boolean
  /** The record's document type. Narrows the fields to that type's own plus the shared ones,
   *  so an invoice shows its amount and a contract its party — not each other's. */
  documentTypeId?: number | null
}

/** Renders + edits the admin-defined custom fields for one record. Hidden if no active fields exist. */
export default function CustomFieldsPanel({ entityType, entityId, canEdit = true, documentTypeId }: Props) {
  const toast = useToast()
  const [defs, setDefs] = useState<CustomFieldDef[]>([])
  const [vals, setVals] = useState<Record<number, string>>({})
  const [busy, setBusy] = useState(false)
  const [dirty, setDirty] = useState(false)

  const load = useCallback(async () => {
    try {
      const [d, v] = await Promise.all([
        customFields.list(entityType, { documentTypeId }),
        customFields.values(entityType, entityId),
      ])
      setDefs(d.filter((x) => x.isActive))
      const map: Record<number, string> = {}
      v.forEach((x) => { map[x.fieldId] = x.value })
      setVals(map); setDirty(false)
    } catch { /* ignore — panel just stays empty */ }
  }, [entityType, entityId, documentTypeId])
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
          if (!canEdit) return <LabeledInput key={f.id} label={f.label} value={v} readOnly />
          if (f.fieldType === 1) return <LabeledInput key={f.id} label={f.label} type="number" dir="ltr" value={v} onChange={(e) => set(f.id, e.target.value)} />
          if (f.fieldType === 2) return <LabeledInput key={f.id} label={f.label} type="date" dir="ltr" value={v} onChange={(e) => set(f.id, e.target.value)} />
          if (f.fieldType === 3) return (
            <LabeledSelect key={f.id} label={f.label} value={v} onChange={(e) => set(f.id, e.target.value)}>
              <option value="">—</option>
              {optionList(f).map((o) => <option key={o} value={o}>{o}</option>)}
            </LabeledSelect>
          )
          return <LabeledInput key={f.id} label={f.label} value={v} onChange={(e) => set(f.id, e.target.value)} />
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
