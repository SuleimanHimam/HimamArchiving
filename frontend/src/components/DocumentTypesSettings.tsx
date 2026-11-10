import { useEffect, useState, useCallback, useMemo, Fragment, type FormEvent } from 'react'
import { Pencil, Trash2, Globe } from 'lucide-react'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { auth } from '../lib/auth'
import {
  useExpandedRow, ExpandCell, ExpandHeadCell, ExpandedRow, DetailFields, DetailField, DetailActions,
} from './ui/expandable-row'
import { useExcelTable, type ColumnDef } from './ui/use-excel-table'
import { LabeledInput, LabeledSelect } from './ui/labeled-input'
import { ColumnHeaderMenu } from './ui/column-filter'
import {
  documents, RETENTION_UNITS, RETENTION_UNIT_AR, RETENTION_UNIT_VALUE,
  type DocumentTypeDto, type OrgUnitDto,
} from '../lib/documents'
import { useToast } from './toast'
import { useConfirmDialog } from './ui/confirm-dialog'
import '../pages/documents/documents.css'
import '../pages/settings/settings.css'

const CONF = [
  { v: 0, ar: 'عام' }, { v: 1, ar: 'داخلي' }, { v: 2, ar: 'سري' }, { v: 3, ar: 'سري للغاية' },
]
const CONF_LEVEL: Record<string, number> = { Public: 0, Internal: 1, Confidential: 2, HighlyConfidential: 3 }
const confAr = (s: string) => CONF[CONF_LEVEL[s] ?? 1].ar

interface FormState {
  name: string; nameEn: string; code: string
  confidentiality: number; requiresApproval: boolean; scannerOnly: boolean
  isActive: boolean
  /** مدة الحفظ as entered, plus its unit (0 days · 1 months · 2 years). */
  retentionValue: number; retentionUnit: number
  /** '' = عام (available to every department) */
  orgUnitId: number | ''
  number: string
  note: string
}
const EMPTY: FormState = {
  name: '', nameEn: '', code: '',
  confidentiality: 1, requiresApproval: false, scannerOnly: false, isActive: true,
  retentionValue: 10, retentionUnit: 2,   // 10 سنوات
  orgUnitId: '', number: '', note: '',
}

const errOf = (err: unknown, fallback: string) =>
  (err as AxiosError<{ error?: string }>).response?.data?.error ?? fallback

export default function DocumentTypesSettings() {
  const { toggle, isExpanded } = useExpandedRow<number>()
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const [types, setTypes] = useState<DocumentTypeDto[]>([])
  const [units, setUnits] = useState<OrgUnitDto[]>([])
  const [form, setForm] = useState<FormState>(EMPTY)
  const [editId, setEditId] = useState<number | null>(null)
  const [busy, setBusy] = useState(false)
  /** Table filter: '' = كل الدوائر · 'shared' = العامة فقط · number = that department. */
  const [filter, setFilter] = useState<number | '' | 'shared'>('')

  const canEdit = auth.hasPermission('Classification.Edit')

  const load = useCallback(async () => {
    try {
      setTypes(await documents.types())
    } catch { toast.error('تعذّر تحميل أنواع الوثائق') }
  }, [toast])

  useEffect(() => { load() }, [load])
  useEffect(() => { documents.orgUnits().then(setUnits).catch(() => { /* department picker stays empty */ }) }, [])

  const shown = types.filter((t) =>
    filter === '' ? true : filter === 'shared' ? t.orgUnitId == null : t.orgUnitId === filter)

  const columns = useMemo<ColumnDef<DocumentTypeDto>[]>(() => [
    { key: 'number',    accessor: (d) => (d.number != null ? String(d.number) : '—') },
    { key: 'dept',      accessor: (d) => (d.orgUnitId == null ? 'عام' : d.orgUnitName ?? '—') },
    { key: 'name',      accessor: (d) => d.name },
    { key: 'code',      accessor: (d) => d.code ?? '—' },
    { key: 'retention', accessor: (d) => `${d.retentionValue} ${RETENTION_UNIT_AR[d.retentionUnit] ?? ''}`.trim(), sortValue: (d) => d.retentionValue },
    { key: 'conf',      accessor: (d) => confAr(d.defaultConfidentiality) },
    { key: 'approval',  accessor: (d) => (d.requiresApproval ? 'نعم' : '—') },
    { key: 'upload',    accessor: (d) => (d.allowedUploadSources === 'Scanner' ? 'ماسح فقط' : 'الكل') },
    { key: 'note',      accessor: (d) => d.note ?? '—' },
    { key: 'status',    accessor: (d) => (d.isActive ? 'مفعّل' : 'معطّل') },
  ], [])
  const { viewRows, getColumnProps } = useExcelTable(shown, columns)

  const set = <K extends keyof FormState>(k: K, v: FormState[K]) => setForm((f) => ({ ...f, [k]: v }))

  function reset() { setForm(EMPTY); setEditId(null) }

  function edit(t: DocumentTypeDto) {
    setEditId(t.id)
    setForm({
      name: t.name, nameEn: t.nameEn ?? '', code: t.code ?? '',
      confidentiality: CONF_LEVEL[t.defaultConfidentiality] ?? 1,
      retentionValue: t.retentionValue,
      retentionUnit: RETENTION_UNIT_VALUE[t.retentionUnit] ?? 1,
      requiresApproval: t.requiresApproval,
      scannerOnly: t.allowedUploadSources === 'Scanner',
      isActive: t.isActive,
      orgUnitId: t.orgUnitId ?? '',
      number: t.number == null ? '' : String(t.number),
      note: t.note ?? '',
    })
  }

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!form.name.trim()) { toast.error('اسم النوع مطلوب'); return }
    const body = {
      name: form.name.trim(),
      nameEn: form.nameEn.trim() || null,
      code: form.code.trim() || null,
      defaultConfidentiality: Number(form.confidentiality),
      // retentionMonths is derived server-side from the pair; sent for older API compatibility.
      retentionValue: Number(form.retentionValue) || 1,
      retentionUnit: Number(form.retentionUnit),
      retentionMonths: 0,
      requiresApproval: form.requiresApproval,
      allowedUploadSources: form.scannerOnly ? 1 : 15, // Scanner only | All
      isActive: form.isActive,
      orgUnitId: form.orgUnitId === '' ? null : Number(form.orgUnitId), // null = عام
      number: form.number.trim() === '' ? null : Number(form.number),
      note: form.note.trim() || null,
    }
    setBusy(true)
    try {
      if (editId) { await documents.updateType(editId, body); toast.success('تم حفظ النوع') }
      else { await documents.createType(body); toast.success('تم إنشاء النوع') }
      reset(); await load()
    } catch (err) { toast.error(errOf(err, 'تعذّر حفظ النوع')) }
    finally { setBusy(false) }
  }

  async function remove(t: DocumentTypeDto) {
    if (!(await confirm({ message: `حذف نوع الوثيقة «${t.name}»؟`, destructive: true }))) return
    setBusy(true)
    try { await documents.deleteType(t.id); if (editId === t.id) reset(); await load(); toast.success('تم حذف النوع') }
    catch (err) { toast.error(errOf(err, 'تعذّر حذف النوع')) }
    finally { setBusy(false) }
  }

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
      <h3 className="detail-h3">أنواع الوثائق ({shown.length})</h3>
      <p className="muted">
        نوع الوثيقة يحدّد مدة الحفظ، السرية الافتراضية، ومصادر الرفع المسموحة.
        يمكن أن يتبع النوع دائرة معيّنة، أو يكون <strong>عامًا</strong> متاحًا لكل الدوائر.
      </p>

      <div className="filters" style={{ gap: '.5rem', marginBlockEnd: '.75rem' }}>
        <select className="filters__status" value={filter === '' ? '' : String(filter)}
          onChange={(e) => setFilter(e.target.value === '' ? '' : e.target.value === 'shared' ? 'shared' : Number(e.target.value))}>
          <option value="">كل الدوائر</option>
          <option value="shared">عام (كل الدوائر)</option>
          {units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
        </select>
      </div>

      <div className="table-scroll">
        <table className="reg-table">
          <thead>
            <tr>
              <ExpandHeadCell />
              <th><ColumnHeaderMenu label="الرقم" {...getColumnProps('number')} /></th>
              <th><ColumnHeaderMenu label="الدائرة" {...getColumnProps('dept')} /></th>
              <th><ColumnHeaderMenu label="الاسم" {...getColumnProps('name')} /></th>
              <th><ColumnHeaderMenu label="رمز التعريف" {...getColumnProps('code')} /></th>
              <th><ColumnHeaderMenu label="مدة الحفظ" {...getColumnProps('retention')} /></th>
              <th><ColumnHeaderMenu label="السرية الافتراضية" {...getColumnProps('conf')} /></th>
              <th><ColumnHeaderMenu label="يتطلب اعتماد" {...getColumnProps('approval')} /></th>
              <th><ColumnHeaderMenu label="الرفع" {...getColumnProps('upload')} /></th>
              <th><ColumnHeaderMenu label="ملاحظة" {...getColumnProps('note')} /></th>
              <th><ColumnHeaderMenu label="الحالة" {...getColumnProps('status')} /></th>
              {canEdit && <th></th>}
            </tr>
          </thead>
          <tbody>
            {shown.length === 0 && <tr><td colSpan={12} className="reg-empty">لا توجد أنواع</td></tr>}
            {viewRows.map((t) => {
              const open = isExpanded(t.id)
              return (
              <Fragment key={t.id}>
              <tr className={editId === t.id ? 'reg-row is-editing' : ''}>
                <ExpandCell expanded={open} onToggle={() => toggle(t.id)} />
                <td className="mono">{t.number ?? '—'}</td>
                <td>{t.orgUnitId == null ? <span className="badge"><Globe size={12} aria-hidden /> عام</span> : t.orgUnitName}</td>
                <td>{t.name}{t.nameEn && <span className="muted" style={{ marginInlineStart: 6 }} dir="ltr">{t.nameEn}</span>}</td>
                <td className="mono">{t.code ?? '—'}</td>
                <td className="mono">{t.retentionValue} {RETENTION_UNIT_AR[t.retentionUnit] ?? ''}</td>
                <td>{confAr(t.defaultConfidentiality)}</td>
                <td>{t.requiresApproval ? 'نعم' : '—'}</td>
                <td>{t.allowedUploadSources === 'Scanner' ? 'ماسح فقط' : 'الكل'}</td>
                <td>{t.note ?? '—'}</td>
                <td>{t.isActive ? <span className="badge internal">مفعّل</span> : <span className="badge">معطّل</span>}</td>
                {canEdit && (
                  <td className="row-actions">
                    <button className="btn btn-ghost btn-sm" title="تعديل" onClick={() => edit(t)}><Pencil size={14} aria-hidden /></button>
                    <button className="btn btn-ghost btn-sm" title="حذف" onClick={() => remove(t)}><Trash2 size={14} aria-hidden /></button>
                  </td>
                )}
              </tr>
              {/* Eleven columns do not fit a laptop — this is where the row stays legible. */}
              <ExpandedRow colSpan={12} expanded={open}>
                <DetailFields>
                  <DetailField label="الرقم"><span className="mono">{t.number ?? '—'}</span></DetailField>
                  <DetailField label="الدائرة">{t.orgUnitId == null ? 'عام' : t.orgUnitName}</DetailField>
                  <DetailField label="رمز التعريف"><span className="mono">{t.code ?? '—'}</span></DetailField>
                  <DetailField label="مدة الحفظ">
                    <span className="mono">{t.retentionValue} {RETENTION_UNIT_AR[t.retentionUnit] ?? ''}</span>
                  </DetailField>
                  <DetailField label="السرية الافتراضية">{confAr(t.defaultConfidentiality)}</DetailField>
                  <DetailField label="يتطلب اعتماد">{t.requiresApproval ? 'نعم' : 'لا'}</DetailField>
                  <DetailField label="الرفع">{t.allowedUploadSources === 'Scanner' ? 'ماسح فقط' : 'الكل'}</DetailField>
                  <DetailField label="الحالة">
                    <span className={`status-pill ${t.isActive ? 's-active' : 's-onhold'}`}>{t.isActive ? 'مفعّل' : 'معطّل'}</span>
                  </DetailField>
                  <DetailField label="الاسم" wide>
                    {t.name}{t.nameEn && <span className="muted" style={{ marginInlineStart: 6 }} dir="ltr">{t.nameEn}</span>}
                  </DetailField>
                  <DetailField label="ملاحظة" wide>{t.note || '—'}</DetailField>
                </DetailFields>
                {canEdit && (
                  <DetailActions>
                    <button type="button" className="btn btn-ghost btn-sm" onClick={() => edit(t)}>تعديل</button>
                    <button type="button" className="btn btn-ghost btn-sm text-destructive" onClick={() => remove(t)}>حذف</button>
                  </DetailActions>
                )}
              </ExpandedRow>
              </Fragment>
            )})}
          </tbody>
        </table>
      </div>

      {canEdit && (
        <form className="form-grid form-grid--4" onSubmit={submit} style={{ marginTop: '1rem' }}>
          <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{editId ? 'تعديل نوع' : 'إضافة نوع'}</h4>
          <LabeledSelect label="الدائرة" value={form.orgUnitId === '' ? '' : String(form.orgUnitId)}
            onChange={(e) => set('orgUnitId', e.target.value === '' ? '' : Number(e.target.value))}>
            <option value="">عام — متاح لكل الدوائر</option>
            {units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
          </LabeledSelect>
          <LabeledInput label="الرقم (اختياري)" type="number" min={1} dir="ltr" value={form.number}
            onChange={(e) => set('number', e.target.value)} />
          <LabeledInput label="الاسم" required value={form.name} onChange={(e) => set('name', e.target.value)} />
          <LabeledInput label="الاسم بالإنجليزية" dir="ltr" value={form.nameEn} onChange={(e) => set('nameEn', e.target.value)} />
          <LabeledInput label="رمز التعريف (اختياري)" dir="ltr" value={form.code} onChange={(e) => set('code', e.target.value)} />
          <LabeledSelect label="السرية الافتراضية" value={form.confidentiality} onChange={(e) => set('confidentiality', Number(e.target.value))}>
            {CONF.map((c) => <option key={c.v} value={c.v}>{c.ar}</option>)}
          </LabeledSelect>
          <LabeledInput label="مدة الحفظ" type="number" min={1} dir="ltr" value={form.retentionValue}
            onChange={(e) => set('retentionValue', Number(e.target.value))} />
          <LabeledSelect label="وحدة المدة" value={form.retentionUnit} onChange={(e) => set('retentionUnit', Number(e.target.value))}>
            {RETENTION_UNITS.map((u) => <option key={u.v} value={u.v}>{u.ar}</option>)}
          </LabeledSelect>
          <LabeledSelect label="مصادر الرفع" value={form.scannerOnly ? '1' : '0'} onChange={(e) => set('scannerOnly', e.target.value === '1')}>
            <option value="0">الكل</option><option value="1">الماسح الضوئي فقط</option>
          </LabeledSelect>
          <LabeledSelect label="يتطلب اعتماد" value={form.requiresApproval ? '1' : '0'} onChange={(e) => set('requiresApproval', e.target.value === '1')}>
            <option value="0">لا</option><option value="1">نعم</option>
          </LabeledSelect>
          <LabeledInput label="ملاحظة" value={form.note} onChange={(e) => set('note', e.target.value)} />
          {editId && (
            <LabeledSelect label="الحالة" value={form.isActive ? '1' : '0'} onChange={(e) => set('isActive', e.target.value === '1')}>
              <option value="1">مفعّل</option><option value="0">معطّل</option>
            </LabeledSelect>
          )}
          <div className="form-actions">
            <button className="btn btn-primary" disabled={busy}>{editId ? 'حفظ' : '+ إضافة نوع'}</button>
            {editId && <button type="button" className="btn btn-ghost" onClick={reset}>إلغاء</button>}
          </div>
        </form>
      )}
    </motion.section>
  )
}
