import { useEffect, useState, useCallback } from 'react'
import { useParams, Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { auth } from '../../lib/auth'
import {
  incomingMail, type IncomingMailDetail, type MailAction,
  CONFIDENTIALITY_LABELS, STATUS_LABELS, PRIORITY_LABELS,
} from '../../lib/incomingMail'
import { useToast } from '../../components/toast'
import './incoming.css'

const ACTION_LABELS: Record<string, string> = {
  Created: 'تسجيل المعاملة', Forwarded: 'إحالة', Approved: 'اعتماد',
  Held: 'تعليق', Closed: 'إغلاق', Archived: 'أرشفة',
}

const ACTIONS: { code: MailAction; label: string; perm: string; cls: string }[] = [
  { code: 0, label: 'إحالة', perm: 'IncomingMail.Forward', cls: 'btn-primary' },
  { code: 1, label: 'اعتماد', perm: 'IncomingMail.Approve', cls: 'btn-primary' },
  { code: 2, label: 'تعليق', perm: 'IncomingMail.Edit', cls: 'btn-ghost' },
  { code: 3, label: 'إغلاق', perm: 'IncomingMail.Edit', cls: 'btn-ghost' },
  { code: 4, label: 'أرشفة', perm: 'IncomingMail.Archive', cls: 'btn-seal' },
]

export default function IncomingDetailPage() {
  const { id } = useParams()
  const mailId = Number(id)
  const [mail, setMail] = useState<IncomingMailDetail | null>(null)
  const [note, setNote] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const toast = useToast()

  const load = useCallback(async () => {
    setError('')
    try { setMail(await incomingMail.get(mailId)) }
    catch { setError('تعذّر تحميل المعاملة (قد لا تملك صلاحية الوصول)') }
  }, [mailId])

  useEffect(() => { load() }, [load])

  async function act(code: MailAction) {
    setBusy(true); setError('')
    try {
      const updated = await incomingMail.act(mailId, code, { note: note || null })
      setMail(updated); setNote(''); toast.success('تم تنفيذ الإجراء')
    } catch { toast.error('تعذّر تنفيذ الإجراء (تحقق من صلاحياتك)') }
    finally { setBusy(false) }
  }

  if (error && !mail) return (
    <div><Link to="/app/incoming" className="btn btn-ghost">← رجوع</Link><p className="login__error">{error}</p></div>
  )
  if (!mail) return <p className="muted">…جارٍ التحميل</p>

  const c = CONFIDENTIALITY_LABELS[mail.confidentiality] ?? { ar: mail.confidentiality, cls: 'internal' }

  return (
    <div>
      <div className="print-header">
        <h2>{mail.subject}</h2>
        <div className="print-sub mono">{mail.transactionNumber} · {mail.senderEntity} · {new Date().toLocaleString('ar')}</div>
        <hr />
      </div>

      <header className="page__head">
        <div>
          <span className="kicker mono">{mail.transactionNumber}</span>
          <h1>{mail.subject}</h1>
        </div>
        <div className="page__headactions">
          {auth.hasPermission('IncomingMail.Print') && (
            <button className="btn btn-ghost" onClick={() => window.print()}>🖨 طباعة</button>
          )}
          <Link to="/app/incoming" className="btn btn-ghost">← رجوع للقائمة</Link>
        </div>
      </header>

      <div className="detail-grid">
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <div className="detail-badges">
            <span className={`status-pill s-${mail.status.toLowerCase()}`}>{STATUS_LABELS[mail.status] ?? mail.status}</span>
            <span className={`badge ${c.cls}`}>{c.ar}</span>
            <span className="badge internal">أولوية: {PRIORITY_LABELS[mail.priority] ?? mail.priority}</span>
          </div>
          <dl className="detail-list">
            <dt>الجهة المرسلة</dt><dd>{mail.senderEntity}</dd>
            <dt>اسم المرسل</dt><dd>{mail.senderName ?? '—'}</dd>
            <dt>الرقم المرجعي</dt><dd className="mono">{mail.senderReference ?? '—'}</dd>
            <dt>تاريخ الورود</dt><dd className="mono">{mail.receivedDate}</dd>
            <dt>تاريخ الإصدار</dt><dd className="mono">{mail.issueDate ?? '—'}</dd>
            <dt>الكلمات المفتاحية</dt><dd>{mail.keywords ?? '—'}</dd>
            <dt>المتن</dt><dd>{mail.body ?? '—'}</dd>
          </dl>

          <h3 className="detail-h3">إجراءات سير العمل</h3>
          <textarea
            className="action-note" rows={2} placeholder="ملاحظة / تأشيرة (اختياري)…"
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
          <span className="kicker">TIMELINE · سجل الحركة</span>
          <ol className="timeline">
            {mail.timeline.map((t) => (
              <li key={t.id} className="timeline__item">
                <span className="timeline__dot" />
                <div>
                  <div className="timeline__action">{ACTION_LABELS[t.action] ?? t.action}</div>
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
