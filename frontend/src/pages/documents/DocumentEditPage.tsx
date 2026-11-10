import { useEffect, useState, type FormEvent } from 'react'
import { ArrowRight } from 'lucide-react'
import { useNavigate, useParams, Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { documents } from '../../lib/documents'
import { auth } from '../../lib/auth'
import { type Confidentiality } from '../../lib/shared'
import LocationPicker from '../../components/LocationPicker'
import { LabeledInput, LabeledSelect, LabeledTextarea } from '../../components/ui/labeled-input'
import '../../styles/forms.css'

const CONF_LEVEL: Record<string, number> = { Public: 0, Internal: 1, Confidential: 2, HighlyConfidential: 3 }

export default function DocumentEditPage() {
  const { id } = useParams()
  const docId = Number(id)
  const navigate = useNavigate()
  const [docNumber, setDocNumber] = useState('')
  const [ownerPositionId, setOwnerPositionId] = useState<number | null>(null)
  const [form, setForm] = useState({
    title: '', description: '', confidentiality: 1 as number,
    keywords: '', documentDate: '', expiryDate: '',
  })
  const [boxId, setBoxId] = useState<number | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    documents.get(docId)
      .then((d) => {
        setDocNumber(d.documentNumber)
        setOwnerPositionId(d.ownerPositionId)
        setBoxId(d.boxId ?? null)
        setForm({
          title: d.title,
          description: d.description ?? '',
          confidentiality: CONF_LEVEL[d.confidentiality] ?? 1,
          keywords: d.keywords ?? '',
          documentDate: d.documentDate ?? '',
          expiryDate: d.expiryDate ?? '',
        })
      })
      .catch(() => setError('تعذّر تحميل الوثيقة (قد لا تملك صلاحية الوصول)'))
      .finally(() => setLoading(false))
  }, [docId])

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }))

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError('')
    if (!form.title) { setError('العنوان حقل مطلوب'); return }
    setSaving(true)
    try {
      await documents.update(docId, {
        title: form.title,
        description: form.description || null,
        confidentiality: Number(form.confidentiality) as Confidentiality,
        keywords: form.keywords || null,
        documentDate: form.documentDate || null,
        expiryDate: form.expiryDate || null,
        ownerPositionId,
        boxId,
      })
      navigate(`/app/documents/${docId}`, { replace: true })
    } catch (err) {
      const ax = err as AxiosError<{ error?: string }>
      setError(ax.response?.data?.error ?? 'تعذّر حفظ التعديلات')
    } finally { setSaving(false) }
  }

  if (loading) return <div className="page__head"><h1>…جارٍ التحميل</h1></div>

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="mono">EDIT · {docNumber}</span>
          <h1>تعديل الوثيقة</h1>
        </div>
        <Link to={`/app/documents/${docId}`} className="btn btn-ghost"><ArrowRight size={16} className="icon-dir" aria-hidden /> رجوع للوثيقة</Link>
      </header>

      <motion.form className="doc-card form-card" onSubmit={submit}
        initial={{ opacity: 0, y: 14 }} animate={{ opacity: 1, y: 0 }}>
        <div className="form-grid">
          <LabeledInput label="العنوان *" containerClassName="field--wide" value={form.title} onChange={set('title')} />
          <LabeledSelect label="السرية" value={form.confidentiality} onChange={set('confidentiality')}>
            <option value={0}>عام</option><option value={1}>داخلي</option>
            <option value={2}>سري</option><option value={3}>سري للغاية</option>
          </LabeledSelect>
          <LabeledInput label="تاريخ الوثيقة" type="date" dir="ltr" value={form.documentDate} onChange={set('documentDate')} />
          <LabeledInput label="تاريخ انتهاء الحفظ" type="date" dir="ltr" value={form.expiryDate} onChange={set('expiryDate')}
            hint="اتركه فارغًا ليُحتسب تلقائيًا من مدة الحفظ" />
          <LabeledInput label="الكلمات المفتاحية" containerClassName="field--wide" value={form.keywords} onChange={set('keywords')} />
          <LabeledTextarea label="الوصف" containerClassName="field--wide" rows={4} value={form.description} onChange={set('description')} />
          {/* Only the archive may move a paper original between places; everyone else edits the
              metadata and leaves the location as it stands (the API refuses a change either way). */}
          {auth.hasPermission('Archive.Archive') && <LocationPicker value={boxId} onChange={setBoxId} />}
        </div>

        {error && <p className="login__error">{error}</p>}

        <div className="form-actions">
          <button className="btn btn-primary" disabled={saving}>{saving ? '…جارٍ الحفظ' : 'حفظ التعديلات'}</button>
          <Link to={`/app/documents/${docId}`} className="btn btn-ghost">إلغاء</Link>
        </div>
      </motion.form>
    </div>
  )
}
