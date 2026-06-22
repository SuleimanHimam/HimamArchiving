import { useEffect, useState, useCallback } from 'react'
import { useParams, Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import { auth } from '../../lib/auth'
import {
  incomingMail, type IncomingMailDetail, type MailAction,
  CONFIDENTIALITY_LABELS, STATUS_LABELS, PRIORITY_LABELS,
} from '../../lib/incomingMail'
import { useToast } from '../../components/toast'
import CustomFieldsPanel from '../../components/CustomFieldsPanel'
import './incoming.css'

export default function IncomingDetailPage() {
  const { t, i18n } = useTranslation()
  const { id } = useParams()
  const mailId = Number(id)
  const [mail, setMail] = useState<IncomingMailDetail | null>(null)
  const [note, setNote] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const toast = useToast()

  const ACTIONS: { code: MailAction; label: string; perm: string; cls: string }[] = [
    { code: 0, label: t('incoming.detail.actions.assign'), perm: 'IncomingMail.Forward', cls: 'btn-primary' },
    { code: 1, label: t('workflow.actions.approve'), perm: 'IncomingMail.Approve', cls: 'btn-primary' },
    { code: 2, label: t('workflow.actions.hold'), perm: 'IncomingMail.Edit', cls: 'btn-ghost' },
    { code: 3, label: t('incoming.detail.actions.close'), perm: 'IncomingMail.Edit', cls: 'btn-ghost' },
    { code: 4, label: t('incoming.detail.actions.archive'), perm: 'IncomingMail.Archive', cls: 'btn-seal' },
  ]

  const load = useCallback(async () => {
    setError('')
    try { setMail(await incomingMail.get(mailId)) }
    catch { setError(t('incoming.detail.loadError')) }
  }, [mailId, t])

  useEffect(() => { load() }, [load])

  async function act(code: MailAction) {
    setBusy(true); setError('')
    try {
      const updated = await incomingMail.act(mailId, code, { note: note || null })
      setMail(updated); setNote(''); toast.success(t('workflow.actions.approve'))
    } catch { toast.error(t('incoming.detail.loadError')) }
    finally { setBusy(false) }
  }

  if (error && !mail) return (
    <div>
      <Link to="/app/incoming" className="btn btn-ghost">{t('incoming.detail.back')}</Link>
      <p className="login__error">{error}</p>
    </div>
  )
  if (!mail) return <p className="muted">{t('common.loading')}</p>

  const c = CONFIDENTIALITY_LABELS[mail.confidentiality] ?? { ar: mail.confidentiality, cls: 'internal' }
  const locale = i18n.language === 'ar' ? 'ar' : 'en'

  return (
    <div>
      <div className="print-header">
        <h2>{mail.subject}</h2>
        <div className="print-sub mono">{mail.transactionNumber} · {mail.senderEntity} · {new Date().toLocaleString(locale)}</div>
        <hr />
      </div>

      <header className="page__head">
        <div>
          <span className="kicker mono">{mail.transactionNumber}</span>
          <h1>{mail.subject}</h1>
        </div>
        <div className="page__headactions">
          {auth.hasPermission('IncomingMail.Print') && (
            <button className="btn btn-ghost" onClick={() => window.print()}>🖨 {t('common.actions.print')}</button>
          )}
          <Link to="/app/incoming" className="btn btn-ghost">{t('incoming.detail.back')}</Link>
        </div>
      </header>

      <div className="detail-grid">
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <div className="detail-badges">
            <span className={`status-pill s-${mail.status.toLowerCase()}`}>{STATUS_LABELS[mail.status] ?? mail.status}</span>
            <span className={`badge ${c.cls}`}>{c.ar}</span>
            <span className="badge internal">{t('incoming.columns.priority')}: {PRIORITY_LABELS[mail.priority] ?? mail.priority}</span>
          </div>
          <dl className="detail-list">
            <dt>{t('incoming.detail.fields.sender')}</dt><dd>{mail.senderEntity}</dd>
            <dt>{t('incoming.detail.fields.senderName')}</dt><dd>{mail.senderName ?? '—'}</dd>
            <dt>{t('incoming.detail.fields.senderRef')}</dt><dd className="mono">{mail.senderReference ?? '—'}</dd>
            <dt>{t('incoming.detail.fields.receivedDate')}</dt><dd className="mono">{mail.receivedDate}</dd>
            <dt>{t('incoming.detail.fields.issueDate')}</dt><dd className="mono">{mail.issueDate ?? '—'}</dd>
            <dt>{t('incoming.detail.fields.keywords')}</dt><dd>{mail.keywords ?? '—'}</dd>
            <dt>{t('incoming.detail.fields.body')}</dt><dd>{mail.body ?? '—'}</dd>
          </dl>

          <textarea
            className="action-note" rows={2} placeholder={t('common.optional') + '…'}
            value={note} onChange={(e) => setNote(e.target.value)}
          />
          <div className="action-bar">
            {ACTIONS.filter((a) => auth.hasPermission(a.perm)).map((a) => (
              <button key={a.code} className={`btn ${a.cls}`} disabled={busy} onClick={() => act(a.code)}>
                {a.label}
              </button>
            ))}
          </div>
          {error && <p className="login__error">{error}</p>}
        </motion.section>

        <motion.aside className="doc-card timeline-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.08 }}>
          <span className="kicker">{t('timeline.kicker')}</span>
          <ol className="timeline">
            {mail.timeline.map((ev) => (
              <li key={ev.id} className="timeline__item">
                <span className="timeline__dot" />
                <div>
                  <div className="timeline__action">{t(`timeline.actions.${ev.action}`, { defaultValue: ev.action })}</div>
                  {ev.note && <div className="timeline__note">{ev.note}</div>}
                  <div className="timeline__meta mono">{new Date(ev.at).toLocaleString(locale)}</div>
                </div>
              </li>
            ))}
          </ol>
        </motion.aside>
      </div>
      <CustomFieldsPanel entityType="IncomingMail" entityId={mailId} canEdit={auth.hasPermission('IncomingMail.Edit')} />
    </div>
  )
}
