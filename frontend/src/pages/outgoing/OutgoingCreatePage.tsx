import { useState, type FormEvent } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { outgoingMail } from '../../lib/outgoingMail'
import { type Confidentiality, type PriorityLevel } from '../../lib/incomingMail'
import '../incoming/incoming.css'

export default function OutgoingCreatePage() {
  const navigate = useNavigate()
  const [form, setForm] = useState({
    recipientEntity: '', recipientName: '', subject: '', body: '',
    confidentiality: 1 as Confidentiality, priority: 1 as PriorityLevel,
  })
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }))

  async function submit(e: FormEvent) {
    e.preventDefault()
    setError('')
    if (!form.recipientEntity || !form.subject) { setError('الجهة المرسل إليها والموضوع حقول مطلوبة'); return }
    setSaving(true)
    try {
      const created = await outgoingMail.create({
        recipientEntity: form.recipientEntity,
        recipientName: form.recipientName || null,
        subject: form.subject,
        body: form.body || null,
        confidentiality: Number(form.confidentiality) as Confidentiality,
        priority: Number(form.priority) as PriorityLevel,
      })
      navigate(`/app/outgoing/${created.id}`, { replace: true })
    } catch (err) {
      const ax = err as AxiosError<{ error?: string }>
      setError(ax.response?.data?.error ?? 'تعذّر إنشاء الكتاب')
    } finally { setSaving(false) }
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">NEW · تحرير كتاب صادر</span>
          <h1>تحرير كتاب صادر</h1>
        </div>
        <Link to="/app/outgoing" className="btn btn-ghost">← رجوع للقائمة</Link>
      </header>

      <motion.form className="doc-card form-card" onSubmit={submit} initial={{ opacity: 0, y: 14 }} animate={{ opacity: 1, y: 0 }}>
        <div className="form-grid">
          <label className="field"><span>الجهة المرسل إليها *</span>
            <input value={form.recipientEntity} onChange={set('recipientEntity')} placeholder="وزارة / مؤسسة" /></label>
          <label className="field"><span>اسم المستلم</span>
            <input value={form.recipientName} onChange={set('recipientName')} /></label>
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
          <label className="field field--wide"><span>المتن</span>
            <textarea rows={6} value={form.body} onChange={set('body')} /></label>
        </div>

        {error && <p className="login__error">{error}</p>}

        <div className="form-actions">
          <button className="btn btn-primary" disabled={saving}>{saving ? '…جارٍ الحفظ' : 'حفظ كمسودة'}</button>
          <Link to="/app/outgoing" className="btn btn-ghost">إلغاء</Link>
        </div>
      </motion.form>
    </div>
  )
}
