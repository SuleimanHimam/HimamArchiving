import { useEffect, useState, useCallback } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { correspondence, type Correspondence, CORR_EVENT_TONE } from '../../lib/correspondence'
import { useToast } from '../../components/toast'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { Inbox, Send, RefreshCw, X } from 'lucide-react'
import '../incoming/incoming.css'

const errOf = (e: unknown, f: string) => (e as AxiosError<{ error?: string }>).response?.data?.error ?? f
const fmt = (s: string | null) => (s ? new Date(s).toLocaleString('ar') : '—')

export default function CorrespondencePage() {
  const toast = useToast()
  const [box, setBox] = useState<'inbox' | 'outbox'>('inbox')
  const [items, setItems] = useState<Correspondence[]>([])
  const [sel, setSel] = useState<Correspondence | null>(null)
  const [comment, setComment] = useState('')
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try { setItems((await correspondence.list(box, 1, 100)).items) }
    catch { toast.error('تعذّر تحميل المراسلات') }
  }, [box, toast])
  useEffect(() => { load() }, [load])

  function open(c: Correspondence) { setSel(c); setComment('') }

  async function respond(kind: 'ack' | 'obj') {
    if (!sel) return
    if (kind === 'obj' && !comment.trim()) { toast.error('سبب الاعتراض مطلوب'); return }
    setBusy(true)
    try {
      const updated = kind === 'ack'
        ? await correspondence.acknowledge(sel.id, comment.trim() || null)
        : await correspondence.object(sel.id, comment.trim())
      setSel(updated); await load()
      toast.success(kind === 'ack' ? 'تم الإقرار بالاستلام' : 'تم تسجيل الاعتراض')
    } catch (e) { toast.error(errOf(e, 'تعذّر تنفيذ الإجراء')) }
    finally { setBusy(false) }
  }

  const List = ({ rows }: { rows: Correspondence[] }) => (
    <table className="reg-table">
      <thead><tr><th>المرجع</th><th>الحدث</th><th>الموضوع</th><th>من</th><th>إلى</th><th>المطلوب</th><th>الموعد</th><th>الحالة</th></tr></thead>
      <tbody>
        {rows.length === 0 && <tr><td colSpan={8} className="reg-empty">لا توجد مراسلات</td></tr>}
        {rows.map((c) => (
          <tr key={c.id} className="reg-row" style={{ cursor: 'pointer' }} onClick={() => open(c)}>
            <td className="mono">{c.reference}</td>
            <td><span className={`badge ${CORR_EVENT_TONE[c.eventType] ?? 'internal'}`}>{c.eventTypeLabel}</span></td>
            <td className="reg-subject">{c.subject}</td>
            <td>{c.fromOrgUnitName ?? '—'}</td>
            <td>{c.recipients.map((r) => r.orgUnitName).filter(Boolean).join('، ') || '—'}</td>
            <td className="muted" style={{ fontSize: '.8rem' }}>{c.actionLabel}</td>
            <td className="mono">{c.responseDeadline ?? '—'}</td>
            <td>{c.isOverdue ? <span className="badge secret">متأخرة</span> : <span className="status-pill">{c.statusLabel}</span>}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )

  // The caller may respond when viewing the inbox and at least one of their dept rows is still "Delivered".
  const canRespond = box === 'inbox' && !!sel && sel.recipients.some((r) => r.status === 0)

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">المراسلات الرسمية</span>
          <h1>مراسلات الأقسام</h1>
        </div>
        <button className="btn btn-ghost" onClick={load}><RefreshCw size={15} aria-hidden /> تحديث</button>
      </header>

      <Tabs value={box} onValueChange={(v) => setBox(v as 'inbox' | 'outbox')} dir="rtl">
        {/* TabsList takes no variant/size — it renders the one pill style app-wide. */}
        <TabsList>
          <TabsTrigger value="inbox"><Inbox /> الوارد ({box === 'inbox' ? items.length : '—'})</TabsTrigger>
          <TabsTrigger value="outbox"><Send /> الصادر ({box === 'outbox' ? items.length : '—'})</TabsTrigger>
        </TabsList>
        <TabsContent value="inbox">
          <motion.section className="doc-card table-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
            <List rows={items} />
          </motion.section>
        </TabsContent>
        <TabsContent value="outbox">
          <motion.section className="doc-card table-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
            <List rows={items} />
          </motion.section>
        </TabsContent>
      </Tabs>

      {sel && (
        <div className="preview-overlay" onClick={() => !busy && setSel(null)}>
          <motion.div className="preview-modal" onClick={(e) => e.stopPropagation()}
            style={{ width: 'min(640px, 95vw)', height: 'auto', maxHeight: '92vh' }}
            initial={{ opacity: 0, scale: 0.97 }} animate={{ opacity: 1, scale: 1 }}>
            <header className="preview-head">
              <span className="preview-title mono">{sel.reference}</span>
              <button className="btn btn-ghost btn-sm" onClick={() => setSel(null)}><X size={14} aria-hidden /> إغلاق</button>
            </header>

            <div style={{ padding: '1rem', overflow: 'auto', color: 'var(--ink-text)' }}>
              <h3 className="detail-h3" style={{ textAlign: 'center', letterSpacing: 1 }}>مذكرة مراسلة رسمية</h3>
              <dl className="detail-list">
                <dt>التاريخ</dt><dd className="mono">{fmt(sel.createdAt)}</dd>
                <dt>الحدث</dt><dd><span className={`badge ${CORR_EVENT_TONE[sel.eventType] ?? 'internal'}`}>{sel.eventTypeLabel}</span></dd>
                <dt>من</dt><dd>{sel.fromOrgUnitName ?? '—'}</dd>
                <dt>إلى</dt><dd>{sel.recipients.map((r) => r.orgUnitName).filter(Boolean).join('، ')}</dd>
                <dt>الموضوع</dt><dd>{sel.subject}</dd>
                <dt>الوثيقة</dt><dd><Link to={`/app/documents/${sel.documentId}`}>#{sel.documentId} — {sel.documentTitle}</Link></dd>
                <dt>الإجراء المطلوب</dt><dd>{sel.actionLabel}</dd>
                <dt>موعد الرد</dt><dd className="mono">{sel.responseDeadline ?? 'لا ينطبق'}{sel.isOverdue && <span className="badge secret" style={{ marginInlineStart: 6 }}>متأخرة</span>}</dd>
                {sel.certificateReference && (<><dt>شهادة الإتلاف</dt><dd className="mono">{sel.certificateReference}</dd></>)}
                <dt>صادرة عن</dt><dd>{sel.responsibleOfficerName ?? 'النظام'}</dd>
              </dl>

              <div className="doc-card" style={{ background: 'rgba(0,0,0,.03)', padding: '.7rem .9rem', margin: '.6rem 0' }}>
                {sel.body}
              </div>

              <h4 className="detail-h3" style={{ fontSize: '.85rem' }}>سجل التسليم والإقرار</h4>
              <table className="reg-table">
                <thead><tr><th>القسم</th><th>التسليم</th><th>الحالة</th><th>بواسطة</th><th>ملاحظة</th></tr></thead>
                <tbody>
                  {sel.recipients.map((r) => (
                    <tr key={r.orgUnitId}>
                      <td>{r.orgUnitName ?? r.orgUnitId}</td>
                      <td className="mono" style={{ fontSize: '.76rem' }}>{fmt(r.deliveredAtUtc)}</td>
                      <td>{r.status === 2 ? <span className="badge secret">اعتراض</span> : r.status === 1 ? <span className="badge internal">تم الإقرار</span> : <span className="status-pill">تم التسليم</span>}</td>
                      <td>{r.acknowledgedByName ?? '—'}</td>
                      <td className="muted" style={{ fontSize: '.78rem' }}>{r.comment ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>

              {canRespond && (
                <label className="field field--wide" style={{ marginTop: '.8rem' }}>
                  <span>ملاحظة / سبب الاعتراض</span>
                  <textarea rows={2} value={comment} onChange={(e) => setComment(e.target.value)} placeholder="اختياري للإقرار، مطلوب للاعتراض" />
                </label>
              )}
            </div>

            {canRespond && (
              <div className="form-actions" style={{ padding: '0 1rem 1rem' }}>
                <button className="btn btn-primary" disabled={busy} onClick={() => respond('ack')}>إقرار بالاستلام</button>
                <button className="btn btn-ghost" disabled={busy} onClick={() => respond('obj')}>اعتراض</button>
              </div>
            )}
          </motion.div>
        </div>
      )}
    </div>
  )
}
