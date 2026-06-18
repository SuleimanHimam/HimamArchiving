import { useEffect, useState, useCallback } from 'react'
import { useParams, Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { auth } from '../../lib/auth'
import {
  outgoingMail, type OutgoingMailDetail, type OutgoingAction,
  OUT_STATUS_LABELS, OUT_ACTION_LABELS,
} from '../../lib/outgoingMail'
import { CONFIDENTIALITY_LABELS, PRIORITY_LABELS } from '../../lib/incomingMail'
import { useToast } from '../../components/toast'
import '../incoming/incoming.css'

// Which actions are offered depends on the current status (matches the backend state machine).
function availableActions(status: string): { code: OutgoingAction; label: string; perm: string; cls: string }[] {
  switch (status) {
    case 'Draft': return [{ code: 0, label: 'إرسال للاعتماد', perm: 'OutgoingMail.Edit', cls: 'btn-primary' }]
    case 'PendingApproval': return [{ code: 1, label: 'اعتماد', perm: 'OutgoingMail.Approve', cls: 'btn-primary' }]
    case 'Approved': return [{ code: 2, label: 'إرسال', perm: 'OutgoingMail.Archive', cls: 'btn-seal' }]
    case 'Sent': return [{ code: 3, label: 'أرشفة', perm: 'OutgoingMail.Edit', cls: 'btn-ghost' }]
    default: return []
  }
}

export default function OutgoingDetailPage() {
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
    catch { setError('تعذّر تحميل الكتاب (قد لا تملك صلاحية الوصول)') }
  }, [mailId])

  useEffect(() => { load() }, [load])

  async function act(code: OutgoingAction) {
    setBusy(true); setError('')
    try { setMail(await outgoingMail.act(mailId, code, note || null)); setNote(''); toast.success('تم تنفيذ الإجراء') }
    catch { toast.error('تعذّر تنفيذ الإجراء (تحقق من صلاحياتك أو حالة الكتاب)') }
    finally { setBusy(false) }
  }

  if (error && !mail) return (
    <div><Link to="/app/outgoing" className="btn btn-ghost">← رجوع</Link><p className="login__error">{error}</p></div>
  )
  if (!mail) return <p className="muted">…جارٍ التحميل</p>

  const c = CONFIDENTIALITY_LABELS[mail.confidentiality] ?? { ar: mail.confidentiality, cls: 'internal' }
  const actions = availableActions(mail.status).filter((a) => auth.hasPermission(a.perm))

  return (
    <div>
      <div className="print-header">
        <h2>{mail.subject}</h2>
        <div className="print-sub mono">{mail.letterNumber} · {mail.recipientEntity} · {new Date().toLocaleString('ar')}</div>
        <hr />
      </div>

      <header className="page__head">
        <div>
          <span className="kicker mono">{mail.letterNumber}</span>
          <h1>{mail.subject}</h1>
        </div>
        <div className="page__headactions">
          {auth.hasPermission('OutgoingMail.Print') && (
            <button className="btn btn-ghost" onClick={() => window.print()}>🖨 طباعة</button>
          )}
          <Link to="/app/outgoing" className="btn btn-ghost">← رجوع للقائمة</Link>
        </div>
      </header>

      <div className="detail-grid">
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <div className="detail-badges">
            <span className={`status-pill s-${mail.status.toLowerCase()}`}>{OUT_STATUS_LABELS[mail.status] ?? mail.status}</span>
            <span className={`badge ${c.cls}`}>{c.ar}</span>
            <span className="badge internal">أولوية: {PRIORITY_LABELS[mail.priority] ?? mail.priority}</span>
          </div>
          <dl className="detail-list">
            <dt>الجهة المرسل إليها</dt><dd>{mail.recipientEntity}</dd>
            <dt>اسم المستلم</dt><dd>{mail.recipientName ?? '—'}</dd>
            <dt>تاريخ الإرسال</dt><dd className="mono">{mail.sentDate ? new Date(mail.sentDate).toLocaleString('ar') : '—'}</dd>
            <dt>تاريخ الاعتماد</dt><dd className="mono">{mail.approvedAt ? new Date(mail.approvedAt).toLocaleString('ar') : '—'}</dd>
            <dt>المتن</dt><dd>{mail.body ?? '—'}</dd>
          </dl>

          <h3 className="detail-h3">إجراءات</h3>
          {actions.length === 0 ? (
            <p className="muted">لا توجد إجراءات متاحة في هذه الحالة.</p>
          ) : (
            <>
              <textarea className="action-note" rows={2} placeholder="ملاحظة (اختياري)…" value={note} onChange={(e) => setNote(e.target.value)} />
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
          <span className="kicker">TIMELINE · سجل الحركة</span>
          <ol className="timeline">
            {mail.timeline.map((t) => (
              <li key={t.id} className="timeline__item">
                <span className="timeline__dot" />
                <div>
                  <div className="timeline__action">{OUT_ACTION_LABELS[t.action] ?? t.action}</div>
                  {t.note && <div className="timeline__note">{t.note}</div>}
                  <div className="timeline__meta mono">{new Date(t.at).toLocaleString('ar')}</div>
                </div>
              </li>
            ))}
          </ol>
        </motion.aside>
      </div>
    </div>
  )
}
