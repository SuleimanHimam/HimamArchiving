import { useEffect, useState, useCallback, useMemo, type FormEvent } from 'react'
import { Pencil, Trash2 } from 'lucide-react'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { auth } from '../lib/auth'
import {
  diwan, MAIL_SCOPE_AR, MAIL_SCOPE_VALUE,
  type MailCategory, type MailScopeName,
} from '../lib/diwan'
import { LabeledInput, LabeledSelect } from './ui/labeled-input'
import { useExcelTable, type ColumnDef } from './ui/use-excel-table'
import { ColumnHeaderMenu } from './ui/column-filter'
import { useToast } from './toast'
import { useConfirmDialog } from './ui/confirm-dialog'
import '../pages/documents/documents.css'

const errOf = (e: unknown, f: string) => (e as AxiosError<{ error?: string }>).response?.data?.error ?? f

const SCOPES: MailScopeName[] = ['External', 'Internal', 'Both']
const EMPTY = { name: '', nameEn: '', scope: 0, sortOrder: 0, isActive: true }

/**
 * تصنيفات البريد — the kinds of letter the institution deals in. External types classify the
 * Diwan register (مطالبة مالية); internal ones classify what staff send each other
 * (قسيمة راتب، سند قبض، سند دفع). Each type can carry its own letterhead in the print designer.
 */
export default function MailTypesSettings() {
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const canEdit = auth.hasPermission('Organization.Edit')
  const [rows, setRows] = useState<MailCategory[]>([])
  const [form, setForm] = useState<typeof EMPTY>(EMPTY)
  const [editId, setEditId] = useState<number | null>(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    // Inactive types are shown here — this is where they get re-enabled.
    try { setRows(await diwan.categories({ includeInactive: true })) }
    catch { toast.error('تعذّر تحميل التصنيفات') }
  }, [toast])
  useEffect(() => { load() }, [load])

  function reset() { setForm(EMPTY); setEditId(null) }
  function edit(c: MailCategory) {
    setEditId(c.id)
    setForm({
      name: c.name, nameEn: c.nameEn ?? '', scope: MAIL_SCOPE_VALUE[c.scope],
      sortOrder: c.sortOrder, isActive: c.isActive,
    })
  }

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!form.name.trim()) { toast.error('اسم التصنيف مطلوب'); return }
    setBusy(true)
    try {
      const body = {
        name: form.name.trim(), nameEn: form.nameEn.trim() || null,
        scope: form.scope, sortOrder: form.sortOrder, isActive: form.isActive,
      }
      if (editId) { await diwan.updateCategory(editId, body); toast.success('تم حفظ التصنيف') }
      else { await diwan.createCategory(body); toast.success('تم إنشاء التصنيف') }
      reset(); await load()
    } catch (err) { toast.error(errOf(err, 'تعذّر الحفظ')) }
    finally { setBusy(false) }
  }

  async function remove(c: MailCategory) {
    if (!(await confirm({ message: `حذف التصنيف «${c.name}»؟`, destructive: true }))) return
    setBusy(true)
    try { await diwan.removeCategory(c.id); if (editId === c.id) reset(); await load(); toast.success('تم حذف التصنيف') }
    catch (err) { toast.error(errOf(err, 'تعذّر الحذف')) }
    finally { setBusy(false) }
  }

  const columns = useMemo<ColumnDef<MailCategory>[]>(() => [
    { key: 'name',   accessor: (c) => c.name },
    { key: 'nameEn', accessor: (c) => c.nameEn ?? '—' },
    { key: 'scope',  accessor: (c) => MAIL_SCOPE_AR[c.scope] },
    { key: 'status', accessor: (c) => (c.isActive ? 'مفعّل' : 'معطّل') },
  ], [])
  const { viewRows, getColumnProps } = useExcelTable(rows, columns)

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
      <h3 className="detail-h3">تصنيفات البريد</h3>
      <p className="muted">
        نوع الكتاب أو المراسلة. التصنيف الخارجي يخصّ الديوان (مثل «مطالبة مالية»)، والداخلي يخصّ
        المراسلات بين الموظفين (مثل «قسيمة راتب» و«سند قبض» و«سند دفع»). يمكن لكل تصنيف أن يكون له
        تصميم طباعة خاص من مصمّم التقارير.
      </p>

      <div className="table-scroll" style={{ marginTop: '1rem' }}>
        <table className="reg-table">
          <thead>
            <tr>
              <th><ColumnHeaderMenu label="الاسم" {...getColumnProps('name')} /></th>
              <th><ColumnHeaderMenu label="بالإنجليزية" {...getColumnProps('nameEn')} /></th>
              <th><ColumnHeaderMenu label="النطاق" {...getColumnProps('scope')} /></th>
              <th><ColumnHeaderMenu label="الحالة" {...getColumnProps('status')} /></th>
              {canEdit && <th></th>}
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 && <tr><td colSpan={5} className="reg-empty">لا توجد تصنيفات</td></tr>}
            {viewRows.map((c) => (
              <tr key={c.id} className={editId === c.id ? 'reg-row is-editing' : ''}>
                <td>{c.name}</td>
                <td>{c.nameEn ?? '—'}</td>
                <td><span className="badge internal">{MAIL_SCOPE_AR[c.scope]}</span></td>
                <td>{c.isActive ? <span className="badge internal">مفعّل</span> : <span className="badge">معطّل</span>}</td>
                {canEdit && (
                  <td className="row-actions">
                    <button className="btn btn-ghost btn-sm" title="تعديل" onClick={() => edit(c)}><Pencil size={14} aria-hidden /></button>
                    <button className="btn btn-ghost btn-sm" title="حذف" onClick={() => remove(c)}><Trash2 size={14} aria-hidden /></button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {canEdit && (
        <form className="form-grid" onSubmit={submit} style={{ marginTop: '1rem' }}>
          <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>
            {editId ? 'تعديل تصنيف' : 'إضافة تصنيف'}
          </h4>
          <LabeledInput label="الاسم" required value={form.name}
            onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} />
          <LabeledInput label="الاسم بالإنجليزية" value={form.nameEn} dir="ltr"
            onChange={(e) => setForm((f) => ({ ...f, nameEn: e.target.value }))} />
          <LabeledSelect label="النطاق" value={form.scope}
            hint="يحدّد أي سجلّ يعرض هذا التصنيف."
            onChange={(e) => setForm((f) => ({ ...f, scope: Number(e.target.value) }))}>
            {SCOPES.map((s) => <option key={s} value={MAIL_SCOPE_VALUE[s]}>{MAIL_SCOPE_AR[s]}</option>)}
          </LabeledSelect>
          <LabeledInput label="الترتيب" type="number" dir="ltr" value={form.sortOrder}
            onChange={(e) => setForm((f) => ({ ...f, sortOrder: Number(e.target.value) }))} />
          {editId && (
            <LabeledSelect label="الحالة" value={form.isActive ? '1' : '0'}
              onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.value === '1' }))}>
              <option value="1">مفعّل</option><option value="0">معطّل</option>
            </LabeledSelect>
          )}
          <div className="form-actions">
            <button className="btn btn-primary" disabled={busy}>{editId ? 'حفظ' : '+ إضافة تصنيف'}</button>
            {editId && <button type="button" className="btn btn-ghost" onClick={reset}>إلغاء</button>}
          </div>
        </form>
      )}
    </motion.section>
  )
}
