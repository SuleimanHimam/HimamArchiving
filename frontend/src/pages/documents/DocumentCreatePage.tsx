import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { documents, type DocumentTypeDto, type OrgUnitDto } from '../../lib/documents'
import { type Confidentiality } from '../../lib/incomingMail'
import '../incoming/incoming.css'

const today = new Date().toISOString().slice(0, 10)

export default function DocumentCreatePage() {
  const navigate = useNavigate()
  const [types, setTypes] = useState<DocumentTypeDto[]>([])
  const [units, setUnits] = useState<OrgUnitDto[]>([])
  const [form, setForm] = useState({
    title: '', description: '', documentTypeId: '', owningOrgUnitId: '',
    confidentiality: 1 as Confidentiality, keywords: '', documentDate: today,
  })
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    Promise.all([documents.types(), documents.orgUnits()])
      .then(([t, u]) => {
        setTypes(t)
        setUnits(u)
        setForm((f) => ({
          ...f,
          documentTypeId: t[0] ? String(t[0].id) : '',
          owningOrgUnitId: u[0] ? String(u[0].id) : '',
        }))
      })
      .catch(() => setError('تعذّر تحميل أنواع الوثائق والوحدات التنظيمية'))
  }, [])

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }))

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError('')
    if (!form.title || !form.documentTypeId || !form.owningOrgUnitId) {
      setError('العنوان ونوع الوثيقة والوحدة المالكة حقول مطلوبة'); return
    }
    setSaving(true)
    try {
      const created = await documents.create({
        title: form.title,
        description: form.description || null,
        documentTypeId: Number(form.documentTypeId),
        owningOrgUnitId: Number(form.owningOrgUnitId),
        confidentiality: Number(form.confidentiality) as Confidentiality,
        keywords: form.keywords || null,
        documentDate: form.documentDate || null,
      })
      navigate(`/app/documents/${created.id}`, { replace: true })
    } catch (err) {
      const ax = err as AxiosError<{ error?: string }>
      setError(ax.response?.data?.error ?? 'تعذّر إنشاء الوثيقة')
    } finally { setSaving(false) }
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">NEW · تسجيل وثيقة</span>
          <h1>تسجيل وثيقة جديدة</h1>
        </div>
        <Link to="/app/documents" className="btn btn-ghost">← رجوع للقائمة</Link>
      </header>

      <motion.form
        className="doc-card form-card"
        onSubmit={submit}
        initial={{ opacity: 0, y: 14 }} animate={{ opacity: 1, y: 0 }}
      >
        <div className="form-grid">
          <label className="field field--wide"><span>العنوان *</span>
            <input value={form.title} onChange={set('title')} /></label>
          <label className="field"><span>نوع الوثيقة *</span>
            <select value={form.documentTypeId} onChange={set('documentTypeId')}>
              {types.length === 0 && <option value="">— لا توجد أنواع —</option>}
              {types.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select></label>
          <label className="field"><span>الوحدة المالكة *</span>
            <select value={form.owningOrgUnitId} onChange={set('owningOrgUnitId')}>
              {units.length === 0 && <option value="">— لا توجد وحدات —</option>}
              {units.map((u) => <option key={u.id} value={u.id}>{u.name}</option>)}
            </select></label>
          <label className="field"><span>السرية</span>
            <select value={form.confidentiality} onChange={set('confidentiality')}>
              <option value={0}>عام</option><option value={1}>داخلي</option>
              <option value={2}>سري</option><option value={3}>سري للغاية</option>
            </select></label>
          <label className="field"><span>تاريخ الوثيقة</span>
            <input type="date" value={form.documentDate} onChange={set('documentDate')} dir="ltr" /></label>
          <label className="field field--wide"><span>الكلمات المفتاحية</span>
            <input value={form.keywords} onChange={set('keywords')} placeholder="مفصولة بمسافات" /></label>
          <label className="field field--wide"><span>الوصف</span>
            <textarea rows={4} value={form.description} onChange={set('description')} /></label>
        </div>

        {error && <p className="login__error">{error}</p>}

        <div className="form-actions">
          <button className="btn btn-primary" disabled={saving}>{saving ? '…جارٍ الحفظ' : 'تسجيل الوثيقة'}</button>
          <Link to="/app/documents" className="btn btn-ghost">إلغاء</Link>
        </div>
      </motion.form>
    </div>
  )
}
