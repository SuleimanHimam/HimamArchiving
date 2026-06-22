import { useEffect, useState, useCallback, type FormEvent } from 'react'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { auth } from '../lib/auth'
import { customFields, CF_ENTITIES, FIELD_TYPE_LABELS, type CustomFieldDef } from '../lib/customFields'
import { useToast } from './toast'
import '../pages/documents/documents.css'

const errOf = (e: unknown, f: string) => (e as AxiosError<{ error?: string }>).response?.data?.error ?? f

const EMPTY = { label: '', fieldType: 0, options: '', searchable: true, isActive: true }

export default function CustomFieldsSettings() {
  const toast = useToast()
  const canEdit = auth.hasPermission('Organization.Edit')
  const [entity, setEntity] = useState(CF_ENTITIES[0].key)
  const [fields, setFields] = useState<CustomFieldDef[]>([])
  const [form, setForm] = useState<typeof EMPTY>(EMPTY)
  const [editId, setEditId] = useState<number | null>(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try { setFields(await customFields.list(entity)) } catch { toast.error('تعذّر تحميل الحقول') }
  }, [entity, toast])
  useEffect(() => { load() }, [load])

  function reset() { setForm(EMPTY); setEditId(null) }
  function edit(f: CustomFieldDef) {
    setEditId(f.id)
    setForm({ label: f.label, fieldType: f.fieldType, options: f.options ?? '', searchable: f.searchable, isActive: f.isActive })
  }

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!form.label.trim()) { toast.error('اسم الحقل مطلوب'); return }
    setBusy(true)
    try {
      const opts = form.fieldType === 3 ? form.options : null
      if (editId) {
        const cur = fields.find((x) => x.id === editId)!
        await customFields.update(editId, { label: form.label.trim(), fieldType: form.fieldType, options: opts, searchable: form.searchable, sortOrder: cur.sortOrder, isActive: form.isActive })
        toast.success('تم حفظ الحقل')
      } else {
        await customFields.create({ entityType: entity, label: form.label.trim(), fieldType: form.fieldType, options: opts, searchable: form.searchable })
        toast.success('تم إنشاء الحقل')
      }
      reset(); await load()
    } catch (err) { toast.error(errOf(err, 'تعذّر الحفظ')) }
    finally { setBusy(false) }
  }

  async function remove(f: CustomFieldDef) {
    if (!window.confirm(`حذف الحقل «${f.label}» وكل قيمه؟`)) return
    setBusy(true)
    try { await customFields.remove(f.id); if (editId === f.id) reset(); await load(); toast.success('تم حذف الحقل') }
    catch (err) { toast.error(errOf(err, 'تعذّر الحذف')) }
    finally { setBusy(false) }
  }

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
      <h3 className="detail-h3">الحقول المخصصة</h3>
      <p className="muted">عرّف حقولًا إضافية لكل نوع سجل، وعدّل أسماءها. الحقول القابلة للبحث تظهر في بحث الوثائق.</p>

      <label className="field" style={{ maxWidth: 320 }}>
        <span>نوع السجل</span>
        <select value={entity} onChange={(e) => { setEntity(e.target.value); reset() }}>
          {CF_ENTITIES.map((x) => <option key={x.key} value={x.key}>{x.label}</option>)}
        </select>
      </label>

      <div className="table-scroll" style={{ marginTop: '1rem' }}>
        <table className="reg-table">
          <thead>
            <tr><th>الاسم</th><th>النوع</th><th>قابل للبحث</th><th>الحالة</th>{canEdit && <th></th>}</tr>
          </thead>
          <tbody>
            {fields.length === 0 && <tr><td colSpan={5} className="reg-empty">لا توجد حقول مخصصة لهذا السجل</td></tr>}
            {fields.map((f) => (
              <tr key={f.id} className={editId === f.id ? 'reg-row is-editing' : ''}>
                <td>{f.label}</td>
                <td>{FIELD_TYPE_LABELS[f.fieldType]}</td>
                <td>{f.searchable ? 'نعم' : '—'}</td>
                <td>{f.isActive ? <span className="badge internal">مفعّل</span> : <span className="badge">معطّل</span>}</td>
                {canEdit && (
                  <td className="row-actions">
                    <button className="btn btn-ghost btn-sm" title="تعديل" onClick={() => edit(f)}>✏️</button>
                    <button className="btn btn-ghost btn-sm" title="حذف" onClick={() => remove(f)}>🗑</button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {canEdit && (
        <form className="form-grid" onSubmit={submit} style={{ marginTop: '1rem' }}>
          <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{editId ? 'تعديل حقل' : 'إضافة حقل'}</h4>
          <label className="field"><span>اسم الحقل *</span>
            <input value={form.label} onChange={(e) => setForm((f) => ({ ...f, label: e.target.value }))} /></label>
          <label className="field"><span>النوع</span>
            <select value={form.fieldType} onChange={(e) => setForm((f) => ({ ...f, fieldType: Number(e.target.value) }))}>
              {Object.entries(FIELD_TYPE_LABELS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
            </select></label>
          {form.fieldType === 3 && (
            <label className="field field--wide"><span>الخيارات (خيار في كل سطر)</span>
              <textarea rows={3} value={form.options} onChange={(e) => setForm((f) => ({ ...f, options: e.target.value }))} /></label>
          )}
          <label className="field"><span>قابل للبحث</span>
            <select value={form.searchable ? '1' : '0'} onChange={(e) => setForm((f) => ({ ...f, searchable: e.target.value === '1' }))}>
              <option value="1">نعم</option><option value="0">لا</option>
            </select></label>
          {editId && (
            <label className="field"><span>الحالة</span>
              <select value={form.isActive ? '1' : '0'} onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.value === '1' }))}>
                <option value="1">مفعّل</option><option value="0">معطّل</option>
              </select></label>
          )}
          <div className="form-actions">
            <button className="btn btn-primary" disabled={busy}>{editId ? 'حفظ' : '+ إضافة حقل'}</button>
            {editId && <button type="button" className="btn btn-ghost" onClick={reset}>إلغاء</button>}
          </div>
        </form>
      )}
    </motion.section>
  )
}
