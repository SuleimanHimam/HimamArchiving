import { useEffect, useState, useCallback, useMemo, type FormEvent } from 'react'
import { Pencil, Trash2 } from 'lucide-react'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { auth } from '../lib/auth'
import { customFields, CF_ENTITIES, FIELD_TYPE_LABELS, type CustomFieldDef } from '../lib/customFields'
import { documents, type DocumentTypeDto } from '../lib/documents'
import { LabeledInput, LabeledSelect, LabeledTextarea } from './ui/labeled-input'
import { useExcelTable, type ColumnDef } from './ui/use-excel-table'
import { ColumnHeaderMenu } from './ui/column-filter'
import { useToast } from './toast'
import { useConfirmDialog } from './ui/confirm-dialog'
import '../pages/documents/documents.css'

const errOf = (e: unknown, f: string) => (e as AxiosError<{ error?: string }>).response?.data?.error ?? f

const EMPTY = { label: '', fieldType: 0, options: '', searchable: true, isActive: true }

/** The "which document type" selector's value: shared fields, or one type's id. */
const SHARED = 'shared'

export default function CustomFieldsSettings() {
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const canEdit = auth.hasPermission('Organization.Edit')
  const [entity, setEntity] = useState(CF_ENTITIES[0].key)
  const [docTypes, setDocTypes] = useState<DocumentTypeDto[]>([])
  const [scope, setScope] = useState<string>(SHARED)
  const [fields, setFields] = useState<CustomFieldDef[]>([])
  const [form, setForm] = useState<typeof EMPTY>(EMPTY)
  const [editId, setEditId] = useState<number | null>(null)
  const [busy, setBusy] = useState(false)

  // Only documents are typed; the mail registers keep one shared field set.
  const typed = entity === 'Document'
  const scopeTypeId = typed && scope !== SHARED ? Number(scope) : null

  useEffect(() => {
    documents.types().then(setDocTypes).catch(() => setDocTypes([]))
  }, [])

  const load = useCallback(async () => {
    try {
      setFields(await customFields.list(entity, scopeTypeId === null
        ? { sharedOnly: typed }          // "مشترك" — the fields every document type carries
        : { documentTypeId: scopeTypeId })) // that type's own fields, plus the shared ones
    } catch { toast.error('تعذّر تحميل الحقول') }
  }, [entity, scopeTypeId, typed, toast])
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
        // Editing keeps the field where it lives — a shared field stays shared even while a
        // specific type is on screen, since it is shown here only as context.
        await customFields.update(editId, { label: form.label.trim(), fieldType: form.fieldType, options: opts, searchable: form.searchable, sortOrder: cur.sortOrder, isActive: form.isActive, documentTypeId: cur.documentTypeId })
        toast.success('تم حفظ الحقل')
      } else {
        await customFields.create({ entityType: entity, label: form.label.trim(), fieldType: form.fieldType, options: opts, searchable: form.searchable, documentTypeId: scopeTypeId })
        toast.success('تم إنشاء الحقل')
      }
      reset(); await load()
    } catch (err) { toast.error(errOf(err, 'تعذّر الحفظ')) }
    finally { setBusy(false) }
  }

  async function remove(f: CustomFieldDef) {
    if (!(await confirm({ message: `حذف الحقل «${f.label}» وكل قيمه؟`, destructive: true }))) return
    setBusy(true)
    try { await customFields.remove(f.id); if (editId === f.id) reset(); await load(); toast.success('تم حذف الحقل') }
    catch (err) { toast.error(errOf(err, 'تعذّر الحذف')) }
    finally { setBusy(false) }
  }

  const appliesTo = (f: CustomFieldDef) => f.documentTypeName ?? 'مشترك — كل الأنواع'

  const columns = useMemo<ColumnDef<CustomFieldDef>[]>(() => [
    { key: 'name',       accessor: (f) => f.label },
    { key: 'type',       accessor: (f) => FIELD_TYPE_LABELS[f.fieldType] },
    { key: 'appliesTo',  accessor: appliesTo },
    { key: 'searchable', accessor: (f) => (f.searchable ? 'نعم' : '—') },
    { key: 'status',     accessor: (f) => (f.isActive ? 'مفعّل' : 'معطّل') },
  ], [])
  const { viewRows, getColumnProps } = useExcelTable(fields, columns)

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
      <h3 className="detail-h3">الحقول المخصصة</h3>
      <p className="muted">
        عرّف حقولًا إضافية لكل نوع سجل، وعدّل أسماءها. الحقول القابلة للبحث تظهر في بحث الوثائق.
        {typed && ' وللوثائق يمكن ربط الحقل بنوع واحد — فاتورة أو عقد أو راتب — فلا يظهر إلا في وثائق ذلك النوع.'}
      </p>

      <div className="form-grid" style={{ maxWidth: 680 }}>
        <LabeledSelect label="نوع السجل" value={entity}
          onChange={(e) => { setEntity(e.target.value); setScope(SHARED); reset() }}>
          {CF_ENTITIES.map((x) => <option key={x.key} value={x.key}>{x.label}</option>)}
        </LabeledSelect>
        {typed && (
          <LabeledSelect label="نوع الوثيقة" value={scope}
            onChange={(e) => { setScope(e.target.value); reset() }}>
            <option value={SHARED}>مشترك — كل الأنواع</option>
            {docTypes.map((t) => <option key={t.id} value={String(t.id)}>{t.name}</option>)}
          </LabeledSelect>
        )}
      </div>
      {scopeTypeId !== null && (
        <p className="muted" style={{ marginTop: '.4rem' }}>
          تعرض القائمة حقول هذا النوع مع الحقول المشتركة. أي حقل جديد يُضاف هنا يخصّ هذا النوع وحده.
        </p>
      )}

      <div className="table-scroll" style={{ marginTop: '1rem' }}>
        <table className="reg-table">
          <thead>
            <tr><th><ColumnHeaderMenu label="الاسم" {...getColumnProps('name')} /></th><th><ColumnHeaderMenu label="النوع" {...getColumnProps('type')} /></th>{typed && <th><ColumnHeaderMenu label="يُطبَّق على" {...getColumnProps('appliesTo')} /></th>}<th><ColumnHeaderMenu label="قابل للبحث" {...getColumnProps('searchable')} /></th><th><ColumnHeaderMenu label="الحالة" {...getColumnProps('status')} /></th>{canEdit && <th></th>}</tr>
          </thead>
          <tbody>
            {fields.length === 0 && <tr><td colSpan={6} className="reg-empty">لا توجد حقول مخصصة لهذا السجل</td></tr>}
            {viewRows.map((f) => (
              <tr key={f.id} className={editId === f.id ? 'reg-row is-editing' : ''}>
                <td>{f.label}</td>
                <td>{FIELD_TYPE_LABELS[f.fieldType]}</td>
                {typed && (
                  <td>{f.documentTypeName
                    ? <span className="badge internal">{f.documentTypeName}</span>
                    : <span className="badge">مشترك</span>}</td>
                )}
                <td>{f.searchable ? 'نعم' : '—'}</td>
                <td>{f.isActive ? <span className="badge internal">مفعّل</span> : <span className="badge">معطّل</span>}</td>
                {canEdit && (
                  <td className="row-actions">
                    <button className="btn btn-ghost btn-sm" title="تعديل" onClick={() => edit(f)}><Pencil size={14} aria-hidden /></button>
                    <button className="btn btn-ghost btn-sm" title="حذف" onClick={() => remove(f)}><Trash2 size={14} aria-hidden /></button>
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
            {editId ? 'تعديل حقل' : scopeTypeId !== null
              ? `إضافة حقل إلى «${docTypes.find((t) => t.id === scopeTypeId)?.name ?? ''}»`
              : 'إضافة حقل مشترك'}
          </h4>
          <LabeledInput label="اسم الحقل" required value={form.label}
            onChange={(e) => setForm((f) => ({ ...f, label: e.target.value }))} />
          <LabeledSelect label="النوع" value={form.fieldType}
            onChange={(e) => setForm((f) => ({ ...f, fieldType: Number(e.target.value) }))}>
            {Object.entries(FIELD_TYPE_LABELS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
          </LabeledSelect>
          {form.fieldType === 3 && (
            <LabeledTextarea label="الخيارات (خيار في كل سطر)" containerClassName="field--wide" rows={3}
              value={form.options} onChange={(e) => setForm((f) => ({ ...f, options: e.target.value }))} />
          )}
          <LabeledSelect label="قابل للبحث" value={form.searchable ? '1' : '0'}
            onChange={(e) => setForm((f) => ({ ...f, searchable: e.target.value === '1' }))}>
            <option value="1">نعم</option><option value="0">لا</option>
          </LabeledSelect>
          {editId && (
            <LabeledSelect label="الحالة" value={form.isActive ? '1' : '0'}
              onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.value === '1' }))}>
              <option value="1">مفعّل</option><option value="0">معطّل</option>
            </LabeledSelect>
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
