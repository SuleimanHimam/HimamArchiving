import { useEffect, useState, useCallback, useMemo, Fragment, type FormEvent } from 'react'
import { Pencil, Trash2 } from 'lucide-react'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { auth } from '../lib/auth'
import {
  useExpandedRow, ExpandCell, ExpandHeadCell, ExpandedRow, DetailFields, DetailField, DetailActions,
} from './ui/expandable-row'
import { destructionMethods, type DestructionMethodOption } from '../lib/destruction'
import { LabeledInput, LabeledSelect } from './ui/labeled-input'
import { useExcelTable, type ColumnDef } from './ui/use-excel-table'
import { ColumnHeaderMenu } from './ui/column-filter'
import { useToast } from './toast'
import { useConfirmDialog } from './ui/confirm-dialog'
import '../pages/documents/documents.css'

const errOf = (e: unknown, f: string) => (e as AxiosError<{ error?: string }>).response?.data?.error ?? f

export default function DestructionMethodsSettings() {
  const { toggle, isExpanded } = useExpandedRow<number>()
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const canEdit = auth.hasPermission('Destruction.Approve')
  const [items, setItems] = useState<DestructionMethodOption[]>([])
  const [form, setForm] = useState({ label: '', isActive: true })
  const [editId, setEditId] = useState<number | null>(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try { setItems(await destructionMethods.list()) } catch { toast.error('تعذّر تحميل الطرق') }
  }, [toast])
  useEffect(() => { load() }, [load])

  function reset() { setForm({ label: '', isActive: true }); setEditId(null) }
  function edit(m: DestructionMethodOption) { setEditId(m.id); setForm({ label: m.label, isActive: m.isActive }) }

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!form.label.trim()) { toast.error('اسم الطريقة مطلوب'); return }
    setBusy(true)
    try {
      if (editId) await destructionMethods.update(editId, { label: form.label.trim(), isActive: form.isActive })
      else await destructionMethods.create({ label: form.label.trim(), isActive: form.isActive })
      reset(); await load(); toast.success('تم حفظ الطريقة')
    } catch (err) { toast.error(errOf(err, 'تعذّر الحفظ')) }
    finally { setBusy(false) }
  }

  async function remove(m: DestructionMethodOption) {
    if (!(await confirm({ message: `حذف الطريقة «${m.label}»؟`, destructive: true }))) return
    setBusy(true)
    try { await destructionMethods.remove(m.id); if (editId === m.id) reset(); await load(); toast.success('تم الحذف') }
    catch (err) { toast.error(errOf(err, 'تعذّر الحذف')) }
    finally { setBusy(false) }
  }

  const columns = useMemo<ColumnDef<DestructionMethodOption>[]>(() => [
    { key: 'method', accessor: (m) => m.label },
    { key: 'status', accessor: (m) => (m.isActive ? 'مفعّلة' : 'معطّلة') },
  ], [])
  const { viewRows, getColumnProps } = useExcelTable(items, columns)

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
      <h3 className="detail-h3">طرق الإتلاف</h3>
      <p className="muted">عرّف طرق الإتلاف التي تظهر في نموذج طلب الإتلاف. (التقنية الرقمية الفعلية تبقى محو التشفير الآمن؛ هذه الطرق وصفية للتوثيق والشهادة.)</p>

      <div className="table-scroll">
        <table className="reg-table">
          <thead><tr><ExpandHeadCell /><th><ColumnHeaderMenu label="الطريقة" {...getColumnProps('method')} /></th><th><ColumnHeaderMenu label="الحالة" {...getColumnProps('status')} /></th>{canEdit && <th></th>}</tr></thead>
          <tbody>
            {items.length === 0 && <tr><td colSpan={4} className="reg-empty">لا توجد طرق</td></tr>}
            {viewRows.map((m) => {
              const open = isExpanded(m.id)
              return (
              <Fragment key={m.id}>
              <tr className={editId === m.id ? 'reg-row is-editing' : ''}>
                <ExpandCell expanded={open} onToggle={() => toggle(m.id)} />
                <td>{m.label}</td>
                <td>{m.isActive ? <span className="badge internal">مفعّلة</span> : <span className="badge">معطّلة</span>}</td>
                {canEdit && (
                  <td className="row-actions">
                    <button className="btn btn-ghost btn-sm" title="تعديل" onClick={() => edit(m)}><Pencil size={14} aria-hidden /></button>
                    <button className="btn btn-ghost btn-sm" title="حذف" onClick={() => remove(m)}><Trash2 size={14} aria-hidden /></button>
                  </td>
                )}
              </tr>
              <ExpandedRow colSpan={4} expanded={open}>
                <DetailFields>
                  <DetailField label="الطريقة" wide>{m.label}</DetailField>
                  <DetailField label="الحالة">
                    <span className={`status-pill ${m.isActive ? 's-active' : 's-onhold'}`}>{m.isActive ? 'مفعّلة' : 'معطّلة'}</span>
                  </DetailField>
                </DetailFields>
                {canEdit && (
                  <DetailActions>
                    <button type="button" className="btn btn-ghost btn-sm" onClick={() => edit(m)}>تعديل</button>
                    <button type="button" className="btn btn-ghost btn-sm text-destructive" onClick={() => remove(m)}>حذف</button>
                  </DetailActions>
                )}
              </ExpandedRow>
              </Fragment>
            )})}
          </tbody>
        </table>
      </div>

      {canEdit && (
        <form className="form-grid" onSubmit={submit} style={{ marginTop: '1rem' }}>
          <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{editId ? 'تعديل طريقة' : 'إضافة طريقة'}</h4>
          <LabeledInput label="اسم الطريقة" required containerClassName="field--wide" value={form.label}
            onChange={(e) => setForm((f) => ({ ...f, label: e.target.value }))} />
          <LabeledSelect label="الحالة" value={form.isActive ? '1' : '0'}
            onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.value === '1' }))}>
            <option value="1">مفعّلة</option><option value="0">معطّلة</option>
          </LabeledSelect>
          <div className="form-actions">
            <button className="btn btn-primary" disabled={busy}>{editId ? 'حفظ' : '+ إضافة'}</button>
            {editId && <button type="button" className="btn btn-ghost" onClick={reset}>إلغاء</button>}
          </div>
        </form>
      )}
    </motion.section>
  )
}
