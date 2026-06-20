import { useState, type FormEvent } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { useTranslation } from 'react-i18next'
import { incomingMail, type Confidentiality, type PriorityLevel } from '../../lib/incomingMail'
import './incoming.css'

const today = new Date().toISOString().slice(0, 10)

export default function IncomingCreatePage() {
  const { t } = useTranslation()
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
    if (!form.senderEntity || !form.subject) { setError(t('incoming.create.errors.required')); return }
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
      setError(ax.response?.data?.error ?? t('incoming.create.errors.failed'))
    } finally { setSaving(false) }
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">{t('incoming.create.kicker')}</span>
          <h1>{t('incoming.create.title')}</h1>
        </div>
        <Link to="/app/incoming" className="btn btn-ghost">{t('incoming.create.back')}</Link>
      </header>

      <motion.form
        className="doc-card form-card"
        onSubmit={submit}
        initial={{ opacity: 0, y: 14 }} animate={{ opacity: 1, y: 0 }}
      >
        <div className="form-grid">
          <label className="field"><span>{t('incoming.create.senderEntity')} *</span>
            <input value={form.senderEntity} onChange={set('senderEntity')} placeholder={t('incoming.create.senderEntityPlaceholder')} /></label>
          <label className="field"><span>{t('incoming.create.senderName')}</span>
            <input value={form.senderName} onChange={set('senderName')} /></label>
          <label className="field"><span>{t('incoming.create.senderReference')}</span>
            <input value={form.senderReference} onChange={set('senderReference')} dir="ltr" /></label>
          <label className="field"><span>{t('incoming.create.receivedDate')} *</span>
            <input type="date" value={form.receivedDate} onChange={set('receivedDate')} dir="ltr" /></label>
          <label className="field"><span>{t('incoming.create.issueDate')}</span>
            <input type="date" value={form.issueDate} onChange={set('issueDate')} dir="ltr" /></label>
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
          <label className="field field--wide"><span>{t('incoming.create.subject')} *</span>
            <input value={form.subject} onChange={set('subject')} /></label>
          <label className="field field--wide"><span>{t('incoming.create.keywords')}</span>
            <input value={form.keywords} onChange={set('keywords')} placeholder={t('incoming.create.keywordsPlaceholder')} /></label>
          <label className="field field--wide"><span>{t('incoming.create.body')}</span>
            <textarea rows={4} value={form.body} onChange={set('body')} /></label>
        </div>

        {error && <p className="login__error">{error}</p>}

        <div className="form-actions">
          <button className="btn btn-primary" disabled={saving}>
            {saving ? t('incoming.create.submitting') : t('incoming.create.submit')}
          </button>
          <Link to="/app/incoming" className="btn btn-ghost">{t('incoming.create.cancel')}</Link>
        </div>
      </motion.form>
    </div>
  )
}
