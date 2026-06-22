import { useEffect, useState, useCallback, type FormEvent } from 'react'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { auth } from '../lib/auth'
import { documents, type DocumentTypeDto } from '../lib/documents'
import { useToast } from './toast'
import '../pages/documents/documents.css'
import '../pages/settings/settings.css'

const CONF = [
  { v: 0, ar: 'عام' }, { v: 1, ar: 'داخلي' }, { v: 2, ar: 'سري' }, { v: 3, ar: 'سري للغاية' },
]
const CONF_LEVEL: Record<string, number> = { Public: 0, Internal: 1, Confidential: 2, HighlyConfidential: 3 }
const confAr = (s: string) => CONF[CONF_LEVEL[s] ?? 1].ar

interface FormState {
  name: string; nameEn: string; code: string
  confidentiality: number; retentionMonths: number; requiresApproval: boolean; scannerOnly: boolean
  isActive: boolean
}
const EMPTY: FormState = {
  name: '', nameEn: '', code: '',
  confidentiality: 1, retentionMonths: 120, requiresApproval: false, scannerOnly: false, isActive: true,
}

const errOf = (err: unknown, fallback: string) =>
  (err as AxiosError<{ error?: string }>).response?.data?.error ?? fallback

export default function DocumentTypesSettings() {
  const toast = useToast()
  const [types, setTypes] = useState<DocumentTypeDto[]>([])
  const [form, setForm] = useState<FormState>(EMPTY)
  const [editId, setEditId] = useState<number | null>(null)
  const [busy, setBusy] = useState(false)

  const canEdit = auth.hasPermission('Classification.Edit')

  const load = useCallback(async () => {
    try {
      setTypes(await documents.types())
    } catch { toast.error('تعذّر تحميل أنواع الوثائق') }
  }, [toast])

  useEffect(() => { load() }, [load])

  const set = <K extends keyof FormState>(k: K, v: FormState[K]) => setForm((f) => ({ ...f, [k]: v }))

  function reset() { setForm(EMPTY); setEditId(null) }

  function edit(t: DocumentTypeDto) {
    setEditId(t.id)
    setForm({
      name: t.name, nameEn: t.nameEn ?? '', code: t.code ?? '',
      confidentiality: CONF_LEVEL[t.defaultConfidentiality] ?? 1,
      retentionMonths: t.retentionMonths,
      requiresApproval: t.requiresApproval,
      scannerOnly: t.allowedUploadSources === 'Scanner',
      isActive: t.isActive,
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
      retentionMonths: Number(form.retentionMonths) || 120,
      requiresApproval: form.requiresApproval,
      allowedUploadSources: form.scannerOnly ? 1 : 15, // Scanner only | All
      isActive: form.isActive,
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
    if (!window.confirm(`حذف نوع الوثيقة «${t.name}»؟`)) return
    setBusy(true)
    try { await documents.deleteType(t.id); if (editId === t.id) reset(); await load(); toast.success('تم حذف النوع') }
    catch (err) { toast.error(errOf(err, 'تعذّر حذف النوع')) }
    finally { setBusy(false) }
  }

  return (
    <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
      <h3 className="detail-h3">أنواع الوثائق ({types.length})</h3>
      <p className="muted">نوع الوثيقة يحدّد مدة الحفظ، السرية الافتراضية، ومصادر الرفع المسموحة.</p>

      <div className="table-scroll">
        <table className="reg-table">
          <thead>
            <tr>
              <th>الاسم</th><th>رمز التعريف</th><th>مدة الحفظ (شهر)</th><th>السرية الافتراضية</th>
              <th>يتطلب اعتماد</th><th>الرفع</th><th>الحالة</th>{canEdit && <th></th>}
            </tr>
          </thead>
          <tbody>
            {types.length === 0 && <tr><td colSpan={8} className="reg-empty">لا توجد أنواع</td></tr>}
            {types.map((t) => (
              <tr key={t.id} className={editId === t.id ? 'reg-row is-editing' : ''}>
                <td>{t.name}{t.nameEn && <span className="muted" style={{ marginInlineStart: 6 }} dir="ltr">{t.nameEn}</span>}</td>
                <td className="mono">{t.code ?? '—'}</td>
                <td className="mono">{t.retentionMonths}</td>
                <td>{confAr(t.defaultConfidentiality)}</td>
                <td>{t.requiresApproval ? 'نعم' : '—'}</td>
                <td>{t.allowedUploadSources === 'Scanner' ? 'ماسح فقط' : 'الكل'}</td>
                <td>{t.isActive ? <span className="badge internal">مفعّل</span> : <span className="badge">معطّل</span>}</td>
                {canEdit && (
                  <td className="row-actions">
                    <button className="btn btn-ghost btn-sm" title="تعديل" onClick={() => edit(t)}>✏️</button>
                    <button className="btn btn-ghost btn-sm" title="حذف" onClick={() => remove(t)}>🗑</button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {canEdit && (
        <form className="form-grid" onSubmit={submit} style={{ marginTop: '1rem' }}>
          <h4 className="detail-h3" style={{ gridColumn: '1 / -1', margin: 0 }}>{editId ? 'تعديل نوع' : 'إضافة نوع'}</h4>
          <label className="field"><span>الاسم *</span>
            <input value={form.name} onChange={(e) => set('name', e.target.value)} /></label>
          <label className="field"><span>الاسم بالإنجليزية</span>
            <input value={form.nameEn} onChange={(e) => set('nameEn', e.target.value)} dir="ltr" /></label>
          <label className="field"><span>رمز التعريف (اختياري)</span>
            <input value={form.code} onChange={(e) => set('code', e.target.value)} dir="ltr" placeholder="اتركه فارغًا إن لم يلزم" /></label>
          <label className="field"><span>السرية الافتراضية</span>
            <select value={form.confidentiality} onChange={(e) => set('confidentiality', Number(e.target.value))}>
              {CONF.map((c) => <option key={c.v} value={c.v}>{c.ar}</option>)}
            </select></label>
          <label className="field"><span>مدة الحفظ (شهر)</span>
            <input type="number" min={1} dir="ltr" value={form.retentionMonths} onChange={(e) => set('retentionMonths', Number(e.target.value))} /></label>
          <label className="field"><span>مصادر الرفع</span>
            <select value={form.scannerOnly ? '1' : '0'} onChange={(e) => set('scannerOnly', e.target.value === '1')}>
              <option value="0">الكل</option><option value="1">الماسح الضوئي فقط</option>
            </select></label>
          <label className="field"><span>يتطلب اعتماد</span>
            <select value={form.requiresApproval ? '1' : '0'} onChange={(e) => set('requiresApproval', e.target.value === '1')}>
              <option value="0">لا</option><option value="1">نعم</option>
            </select></label>
          {editId && (
            <label className="field"><span>الحالة</span>
              <select value={form.isActive ? '1' : '0'} onChange={(e) => set('isActive', e.target.value === '1')}>
                <option value="1">مفعّل</option><option value="0">معطّل</option>
              </select></label>
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
