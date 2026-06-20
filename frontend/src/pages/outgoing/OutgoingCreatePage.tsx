import { useState, type FormEvent } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { useTranslation } from 'react-i18next'
import { outgoingMail } from '../../lib/outgoingMail'
import { type Confidentiality, type PriorityLevel } from '../../lib/incomingMail'
import '../incoming/incoming.css'

export default function OutgoingCreatePage() {
  const { t } = useTranslation()
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
    if (!form.recipientEntity || !form.subject) { setError(t('outgoing.create.errors.required')); return }
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
      setError(ax.response?.data?.error ?? t('outgoing.create.errors.failed'))
    } finally { setSaving(false) }
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">{t('outgoing.create.kicker')}</span>
          <h1>{t('outgoing.create.title')}</h1>
        </div>
        <Link to="/app/outgoing" className="btn btn-ghost">{t('outgoing.create.back')}</Link>
      </header>

      <motion.form className="doc-card form-card" onSubmit={submit} initial={{ opacity: 0, y: 14 }} animate={{ opacity: 1, y: 0 }}>
        <div className="form-grid">
          <label className="field"><span>{t('outgoing.create.recipientEntity')} *</span>
            <input value={form.recipientEntity} onChange={set('recipientEntity')} placeholder={t('outgoing.create.recipientEntityPlaceholder')} /></label>
          <label className="field"><span>{t('outgoing.create.recipientName')}</span>
            <input value={form.recipientName} onChange={set('recipientName')} /></label>
          <label className="field"><span>{t('incoming.columns.confidentiality')}</span>
            <select value={form.confidentiality} onChange={set('confidentiality')}>
              <option value={0}>{t('common.confidentiality.public')}</option>
              <option value={1}>{t('common.confidentiality.internal')}</option>
              <option value={2}>{t('common.confidentiality.confidential')}</option>
              <option value={3}>{t('common.confidentiality.highlyConfidential')}</option>
            </select></label>
          <label className="field"><span>{t('incoming.columns.priority')}</span>
            <select value={form.priority} onChange={set('priority')}>
              <option value={0}>{t('common.priority.low')}</option>
              <option value={1}>{t('common.priority.normal')}</option>
              <option value={2}>{t('common.priority.high')}</option>
              <option value={3}>{t('common.priority.urgent')}</option>
            </select></label>
          <label className="field field--wide"><span>{t('outgoing.create.subject')} *</span>
            <input value={form.subject} onChange={set('subject')} /></label>
          <label className="field field--wide"><span>{t('outgoing.create.body')}</span>
            <textarea rows={6} value={form.body} onChange={set('body')} /></label>
        </div>

        {error && <p className="login__error">{error}</p>}

        <div className="form-actions">
          <button className="btn btn-primary" disabled={saving}>
            {saving ? t('outgoing.create.submitting') : t('outgoing.create.submit')}
          </button>
          <Link to="/app/outgoing" className="btn btn-ghost">{t('outgoing.create.cancel')}</Link>
        </div>
      </motion.form>
    </div>
  )
}
