import { useEffect, useState, useCallback } from 'react'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { auth } from '../../lib/auth'
import { disposition, type DispositionRequest, DISPOSITION_STATUS, DISPOSITION_ACTION } from '../../lib/disposition'
import { useToast } from '../../components/toast'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { ShieldCheck, Gavel, CheckCircle2 } from 'lucide-react'
import DispositionTimeline from '../../components/DispositionTimeline'
import DestructionCertificate from '../../components/DestructionCertificate'
import RequestDispositionModal from '../../components/RequestDispositionModal'
import '../incoming/incoming.css'

const errOf = (e: unknown, f: string) => (e as AxiosError<{ error?: string }>).response?.data?.error ?? f

export default function DispositionPage() {
  const toast = useToast()
  const canVerify = auth.hasPermission('Disposition.Edit')
  const canApprove = auth.hasPermission('Disposition.Approve')
  const canCreate = auth.hasPermission('Disposition.Create')
  const [newOpen, setNewOpen] = useState(false)

  const [verifyQ, setVerifyQ] = useState<DispositionRequest[]>([])
  const [approveQ, setApproveQ] = useState<DispositionRequest[]>([])
  const [doneQ, setDoneQ] = useState<DispositionRequest[]>([])
  const [busy, setBusy] = useState(false)
  const [sel, setSel] = useState<DispositionRequest | null>(null)
  const [certFor, setCertFor] = useState<number | null>(null)
  const [notes, setNotes] = useState('')

  const load = useCallback(async () => {
    try {
      const [v, a, all] = await Promise.all([
        canVerify ? disposition.list('Verification') : Promise.resolve({ items: [] as DispositionRequest[] } as never),
        canApprove ? disposition.list('FinalApproval') : Promise.resolve({ items: [] as DispositionRequest[] } as never),
        disposition.list(undefined, 1, 100),
      ])
      setVerifyQ(v.items); setApproveQ(a.items)
      setDoneQ(all.items.filter((r) => r.status === DISPOSITION_STATUS.Completed || r.status === DISPOSITION_STATUS.Rejected))
    } catch { toast.error('تعذّر تحميل الطلبات') }
  }, [toast, canVerify, canApprove])
  useEffect(() => { load() }, [load])

  function open(r: DispositionRequest) { setSel(r); setNotes('') }

  async function act(fn: () => Promise<unknown>, ok: string) {
    setBusy(true)
    try { await fn(); setSel(null); await load(); toast.success(ok) }
    catch (e) { toast.error(errOf(e, 'تعذّر تنفيذ الإجراء')) }
    finally { setBusy(false) }
  }

  function doVerify(decision: 'Verify' | 'Reject') {
    if (!sel) return
    if (!notes.trim()) { toast.error('الملاحظات مطلوبة'); return }
    act(() => disposition.verify(sel.id, decision, notes.trim()),
      decision === 'Verify' ? 'تم التحقق وإحالته للموافقة النهائية' : 'تم رفض الطلب')
  }
  function doApprove(decision: 'Approve' | 'Reject') {
    if (!sel) return
    if (!notes.trim()) { toast.error('الملاحظات مطلوبة'); return }
    act(() => disposition.finalApprove(sel.id, decision, notes.trim()),
      decision === 'Approve' ? 'تم الاعتماد النهائي وتنفيذ التصرّف' : 'تم رفض الطلب')
  }

  const stage = sel ? (sel.status === DISPOSITION_STATUS.PendingVerification ? 'verify' : 'approve') : null

  const Queue = ({ rows, action }: { rows: DispositionRequest[]; action: string }) => (
    <table className="reg-table">
      <thead><tr><th>#</th><th>الوثيقة</th><th>الإجراء</th><th>السبب</th><th>مُقدِّم الطلب</th><th>الموقع</th><th>الحالة</th><th></th></tr></thead>
      <tbody>
        {rows.length === 0 && <tr><td colSpan={8} className="reg-empty">لا توجد طلبات</td></tr>}
        {rows.map((r) => (
          <tr key={r.id} className="reg-row">
            <td className="mono">{r.id}</td>
            <td className="reg-subject">{r.documentNumber ?? `#${r.documentId}`}<div className="muted" style={{ fontSize: '.78rem' }}>{r.documentTitle}</div></td>
            <td><span className={`badge ${r.requestedAction === DISPOSITION_ACTION.Destroy ? 'secret' : 'internal'}`}>{r.requestedActionLabel}</span></td>
            <td className="reg-subject">{r.reason}</td>
            <td>{r.requestedByName ?? '—'}</td>
            <td className="mono">{r.boxCode ? `📦 ${r.boxCode}` : '—'}</td>
            <td><span className="status-pill">{r.statusLabel}</span></td>
            <td className="row-actions"><button className="btn btn-primary btn-sm" onClick={() => open(r)}>{action}</button></td>
          </tr>
        ))}
      </tbody>
    </table>
  )

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">الحوكمة والامتثال</span>
          <h1>الاحتفاظ والتصرّف بالوثائق</h1>
        </div>
        <div style={{ display: 'flex', gap: '.5rem' }}>
          {canCreate && <button className="btn btn-primary" onClick={() => setNewOpen(true)}>＋ طلب جديد</button>}
          <button className="btn btn-ghost" onClick={load}>↻ تحديث</button>
        </div>
      </header>

      {newOpen && <RequestDispositionModal onClose={() => setNewOpen(false)} onDone={load} />}

      <Tabs defaultValue={canVerify ? 'verify' : 'approve'} dir="rtl">
        <TabsList variant="line" size="lg">
          {canVerify && <TabsTrigger value="verify"><ShieldCheck /> بانتظار التحقق ({verifyQ.length})</TabsTrigger>}
          {canApprove && <TabsTrigger value="approve"><Gavel /> بانتظار الموافقة النهائية ({approveQ.length})</TabsTrigger>}
          <TabsTrigger value="done"><CheckCircle2 /> المكتملة ({doneQ.length})</TabsTrigger>
        </TabsList>

        {canVerify && (
          <TabsContent value="verify">
            <motion.section className="doc-card table-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
              <h3 className="detail-h3">الخطوة الأولى — التحقق (مسؤول السجلات)</h3>
              <Queue rows={verifyQ} action="مراجعة" />
            </motion.section>
          </TabsContent>
        )}
        {canApprove && (
          <TabsContent value="approve">
            <motion.section className="doc-card table-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
              <h3 className="detail-h3">الخطوة الثانية — الموافقة النهائية (الشؤون القانونية / رئيس القسم)</h3>
              <Queue rows={approveQ} action="اعتماد" />
            </motion.section>
          </TabsContent>
        )}
        <TabsContent value="done">
          <motion.section className="doc-card table-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
            <h3 className="detail-h3">الطلبات المكتملة والمرفوضة</h3>
            <table className="reg-table">
              <thead><tr><th>#</th><th>الوثيقة</th><th>الإجراء</th><th>الحالة</th><th>النتيجة</th><th>الشهادة</th></tr></thead>
              <tbody>
                {doneQ.length === 0 && <tr><td colSpan={6} className="reg-empty">لا توجد طلبات</td></tr>}
                {doneQ.map((r) => (
                  <tr key={r.id} className="reg-row">
                    <td className="mono">{r.id}</td>
                    <td className="reg-subject">{r.documentNumber ?? `#${r.documentId}`}</td>
                    <td><span className={`badge ${r.requestedAction === DISPOSITION_ACTION.Destroy ? 'secret' : 'internal'}`}>{r.requestedActionLabel}</span></td>
                    <td><span className="status-pill">{r.statusLabel}</span></td>
                    <td className="muted" style={{ fontSize: '.82rem' }}>{r.status === DISPOSITION_STATUS.Rejected ? r.rejectionReason : r.newExpiryDate ? `تجديد حتى ${r.newExpiryDate}` : '—'}</td>
                    <td className="row-actions">
                      {r.certificateNumber
                        ? <button className="btn btn-ghost btn-sm" onClick={() => setCertFor(r.id)}>🏅 {r.certificateNumber}</button>
                        : '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </motion.section>
        </TabsContent>
      </Tabs>

      {certFor != null && <DestructionCertificate requestId={certFor} onClose={() => setCertFor(null)} />}

      {sel && (
        <div className="preview-overlay" onClick={() => !busy && setSel(null)}>
          <motion.div className="preview-modal" onClick={(e) => e.stopPropagation()}
            style={{ width: 'min(620px, 94vw)', height: 'auto', maxHeight: '92vh' }}
            initial={{ opacity: 0, scale: 0.97 }} animate={{ opacity: 1, scale: 1 }}>
            <header className="preview-head">
              <span className="preview-title">طلب تصرّف · {sel.documentNumber ?? `#${sel.documentId}`}</span>
              <button className="btn btn-ghost btn-sm" onClick={() => setSel(null)}>✕ إغلاق</button>
            </header>

            <div style={{ padding: '1rem', overflow: 'auto', color: 'var(--ink-text, #211d17)' }}>
              <DispositionTimeline status={sel.status} />

              <dl className="detail-list" style={{ marginTop: '1rem' }}>
                <dt>الوثيقة</dt><dd>{sel.documentTitle ?? '—'}</dd>
                <dt>الإجراء المطلوب</dt><dd><span className={`badge ${sel.requestedAction === DISPOSITION_ACTION.Destroy ? 'secret' : 'internal'}`}>{sel.requestedActionLabel}</span></dd>
                <dt>السبب</dt><dd>{sel.reason}</dd>
                <dt>الموقع الفعلي</dt><dd className="mono">{sel.boxCode ? `📦 ${sel.boxCode}` : '—'}</dd>
                <dt>انتهاء الحفظ</dt><dd className="mono">{sel.expiryDate ?? '—'}</dd>
                <dt>مُقدِّم الطلب</dt><dd>{sel.requestedByName ?? '—'}</dd>
              </dl>

              {/* At step 2, the verifier's notes are read-only context. */}
              {stage === 'approve' && sel.verificationNotes && (
                <div className="doc-card" style={{ background: 'rgba(0,0,0,.03)', padding: '.7rem .9rem', marginBottom: '.8rem' }}>
                  <strong>ملاحظات التحقق</strong> <span className="muted">({sel.verifiedByName})</span>
                  <p style={{ margin: '.3rem 0 0' }}>{sel.verificationNotes}</p>
                </div>
              )}

              <label className="field field--wide">
                <span>{stage === 'verify' ? 'ملاحظات التحقق *' : 'ملاحظات الموافقة النهائية *'}</span>
                <textarea rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="مطلوبة لكلا القرارين" />
              </label>
            </div>

            <div className="form-actions" style={{ padding: '0 1rem 1rem' }}>
              {stage === 'verify' && canVerify && (
                <>
                  <button className="btn btn-primary" disabled={busy} onClick={() => doVerify('Verify')}>تحقّق وإحالة</button>
                  <button className="btn btn-ghost" disabled={busy} onClick={() => doVerify('Reject')}>رفض</button>
                </>
              )}
              {stage === 'approve' && canApprove && (
                <>
                  <button className="btn btn-seal" disabled={busy} onClick={() => doApprove('Approve')}>
                    {sel.requestedAction === DISPOSITION_ACTION.Destroy ? 'اعتماد الإتلاف نهائيًا' : 'اعتماد التجديد'}
                  </button>
                  <button className="btn btn-ghost" disabled={busy} onClick={() => doApprove('Reject')}>رفض</button>
                </>
              )}
            </div>
          </motion.div>
        </div>
      )}
    </div>
  )
}
