import { useState, type FormEvent } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { incomingMail, type Confidentiality, type PriorityLevel } from '../../lib/incomingMail'
import './incoming.css'

const today = new Date().toISOString().slice(0, 10)

export default function IncomingCreatePage() {
  const navigate = useNavigate()
  const [form, setForm] = useState({
    senderEntity: '', senderName: '', senderReference: '',
    subject: '', body: '', issueDate: '', receivedDate: today,
    confidentiality: 1 as Confidentiality, priority: 1 as PriorityLevel, keywords: '',
  })
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }))

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError('')
    if (!form.senderEntity || !form.subject) { setError('الجهة المرسلة والموضوع حقول مطلوبة'); return }
    setSaving(true)
    try {
      const created = await incomingMail.create({
        ...form,
        confidentiality: Number(form.confidentiality) as Confidentiality,
        priority: Number(form.priority) as PriorityLevel,
        issueDate: form.issueDate || null,
        senderName: form.senderName || null,
        senderReference: form.senderReference || null,
        body: form.body || null,
        keywords: form.keywords || null,
      })
      navigate(`/app/incoming/${created.id}`, { replace: true })
    } catch (err) {
      const ax = err as AxiosError<{ error?: string }>
      setError(ax.response?.data?.error ?? 'تعذّر إنشاء المعاملة')
    } finally { setSaving(false) }
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">NEW · تسجيل وارد</span>
          <h1>تسجيل معاملة واردة</h1>
        </div>
        <Link to="/app/incoming" className="btn btn-ghost">← رجوع للقائمة</Link>
      </header>

      <motion.form
        className="doc-card form-card"
        onSubmit={submit}
        initial={{ opacity: 0, y: 14 }} animate={{ opacity: 1, y: 0 }}
      >
        <div className="form-grid">
          <label className="field"><span>الجهة المرسلة *</span>
            <input value={form.senderEntity} onChange={set('senderEntity')} placeholder="وزارة / مؤسسة" /></label>
          <label className="field"><span>اسم المرسل</span>
            <input value={form.senderName} onChange={set('senderName')} /></label>
          <label className="field"><span>الرقم المرجعي للمرسل</span>
            <input value={form.senderReference} onChange={set('senderReference')} dir="ltr" /></label>
          <label className="field"><span>تاريخ الورود *</span>
            <input type="date" value={form.receivedDate} onChange={set('receivedDate')} dir="ltr" /></label>
          <label className="field"><span>تاريخ الإصدار</span>
            <input type="date" value={form.issueDate} onChange={set('issueDate')} dir="ltr" /></label>
          <label className="field"><span>السرية</span>
            <select value={form.confidentiality} onChange={set('confidentiality')}>
              <option value={0}>عام</option><option value={1}>داخلي</option>
              <option value={2}>سري</option><option value={3}>سري للغاية</option>
            </select></label>
          <label className="field"><span>الأولوية</span>
            <select value={form.priority} onChange={set('priority')}>
              <option value={0}>منخفضة</option><option value={1}>عادية</option>
              <option value={2}>عالية</option><option value={3}>عاجلة</option>
            </select></label>
          <label className="field field--wide"><span>الموضوع *</span>
            <input value={form.subject} onChange={set('subject')} /></label>
          <label className="field field--wide"><span>الكلمات المفتاحية</span>
            <input value={form.keywords} onChange={set('keywords')} placeholder="مفصولة بمسافات" /></label>
          <label className="field field--wide"><span>المتن</span>
            <textarea rows={4} value={form.body} onChange={set('body')} /></label>
        </div>

        {error && <p className="login__error">{error}</p>}

        <div className="form-actions">
          <button className="btn btn-primary" disabled={saving}>{saving ? '…جارٍ الحفظ' : 'تسجيل المعاملة'}</button>
          <Link to="/app/incoming" className="btn btn-ghost">إلغاء</Link>
        </div>
      </motion.form>
    </div>
  )
}
