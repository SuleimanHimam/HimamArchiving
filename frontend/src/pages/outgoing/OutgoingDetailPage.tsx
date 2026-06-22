import { useEffect, useState, useCallback } from 'react'
import { useParams, Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import { auth } from '../../lib/auth'
import {
  outgoingMail, type OutgoingMailDetail, type OutgoingAction,
  OUT_STATUS_LABELS,
} from '../../lib/outgoingMail'
import { CONFIDENTIALITY_LABELS, PRIORITY_LABELS } from '../../lib/incomingMail'
import { useToast } from '../../components/toast'
import CustomFieldsPanel from '../../components/CustomFieldsPanel'
import '../incoming/incoming.css'

function availableActions(status: string, t: (k: string) => string): { code: OutgoingAction; label: string; perm: string; cls: string }[] {
  switch (status) {
    case 'Draft': return [{ code: 0, label: t('outgoing.statuses.pendingApproval'), perm: 'OutgoingMail.Edit', cls: 'btn-primary' }]
    case 'PendingApproval': return [{ code: 1, label: t('workflow.actions.approve'), perm: 'OutgoingMail.Approve', cls: 'btn-primary' }]
    case 'Approved': return [{ code: 2, label: t('outgoing.statuses.sent'), perm: 'OutgoingMail.Archive', cls: 'btn-seal' }]
    case 'Sent': return [{ code: 3, label: t('outgoing.statuses.archived'), perm: 'OutgoingMail.Edit', cls: 'btn-ghost' }]
    default: return []
  }
}

export default function OutgoingDetailPage() {
  const { t, i18n } = useTranslation()
  const { id } = useParams()
  const mailId = Number(id)
  const [mail, setMail] = useState<OutgoingMailDetail | null>(null)
  const [note, setNote] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const toast = useToast()

  const load = useCallback(async () => {
    setError('')
    try { setMail(await outgoingMail.get(mailId)) }
    catch { setError(t('outgoing.loadError')) }
  }, [mailId, t])

  useEffect(() => { load() }, [load])

  async function act(code: OutgoingAction) {
    setBusy(true); setError('')
    try { setMail(await outgoingMail.act(mailId, code, note || null)); setNote(''); toast.success(t('workflow.actions.approve')) }
    catch { toast.error(t('outgoing.loadError')) }
    finally { setBusy(false) }
  }

  if (error && !mail) return (
    <div><Link to="/app/outgoing" className="btn btn-ghost">{t('common.actions.back')}</Link><p className="login__error">{error}</p></div>
  )
  if (!mail) return <p className="muted">{t('common.loading')}</p>

  const locale = i18n.language === 'ar' ? 'ar' : 'en'
  const c = CONFIDENTIALITY_LABELS[mail.confidentiality] ?? { ar: mail.confidentiality, cls: 'internal' }
  const actions = availableActions(mail.status, t).filter((a) => auth.hasPermission(a.perm))

  return (
    <div>
      <div className="print-header">
        <h2>{mail.subject}</h2>
        <div className="print-sub mono">{mail.letterNumber} · {mail.recipientEntity} · {new Date().toLocaleString(locale)}</div>
        <hr />
      </div>

      <header className="page__head">
        <div>
          <span className="kicker mono">{mail.letterNumber}</span>
          <h1>{mail.subject}</h1>
        </div>
        <div className="page__headactions">
          {auth.hasPermission('OutgoingMail.Print') && (
            <button className="btn btn-ghost" onClick={() => window.print()}>🖨 {t('common.actions.print')}</button>
          )}
          <Link to="/app/outgoing" className="btn btn-ghost">{t('common.actions.back')}</Link>
        </div>
      </header>

      <div className="detail-grid">
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <div className="detail-badges">
            <span className={`status-pill s-${mail.status.toLowerCase()}`}>{OUT_STATUS_LABELS[mail.status] ?? mail.status}</span>
            <span className={`badge ${c.cls}`}>{c.ar}</span>
            <span className="badge internal">{t('incoming.columns.priority')}: {PRIORITY_LABELS[mail.priority] ?? mail.priority}</span>
          </div>
          <dl className="detail-list">
            <dt>{t('outgoing.columns.recipient')}</dt><dd>{mail.recipientEntity}</dd>
            <dt>{t('outgoing.create.recipientName')}</dt><dd>{mail.recipientName ?? '—'}</dd>
            <dt>{t('outgoing.columns.date')}</dt><dd className="mono">{mail.sentDate ? new Date(mail.sentDate).toLocaleString(locale) : '—'}</dd>
            <dt>{t('outgoing.statuses.approved')}</dt><dd className="mono">{mail.approvedAt ? new Date(mail.approvedAt).toLocaleString(locale) : '—'}</dd>
            <dt>{t('outgoing.create.body')}</dt><dd>{mail.body ?? '—'}</dd>
          </dl>

          {actions.length === 0 ? (
            <p className="muted">{t('workflow.empty')}</p>
          ) : (
            <>
              <textarea className="action-note" rows={2} placeholder={t('common.optional') + '…'} value={note} onChange={(e) => setNote(e.target.value)} />
              <div className="action-bar">
                {actions.map((a) => (
                  <button key={a.code} className={`btn ${a.cls}`} disabled={busy} onClick={() => act(a.code)}>{a.label}</button>
                ))}
              </div>
            </>
          )}
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
      <CustomFieldsPanel entityType="OutgoingMail" entityId={mailId} canEdit={auth.hasPermission('OutgoingMail.Edit')} />
    </div>
  )
}
