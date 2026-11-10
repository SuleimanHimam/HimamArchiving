import { useEffect, useState, useCallback, useMemo } from 'react'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { auth } from '../../lib/auth'
import { disposition, type DispositionRequest, DISPOSITION_STATUS, DISPOSITION_ACTION } from '../../lib/disposition'
import { useToast } from '../../components/toast'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { Card, CardContent, CardHeader, CardTitle } from '../../components/ui/card'
import { ShieldCheck, Gavel, CheckCircle2, User, RefreshCw, Award, X, Search, Inbox } from 'lucide-react'
import DispositionTimeline from '../../components/DispositionTimeline'
import DestructionCertificate from '../../components/DestructionCertificate'
import RequestDispositionModal from '../../components/RequestDispositionModal'
import SearchSelect from '../../components/SearchSelect'
import '../../styles/forms.css'
import './disposition.css'

const errOf = (e: unknown, f: string) => (e as AxiosError<{ error?: string }>).response?.data?.error ?? f

const STATUS_OPTIONS = [
  { value: String(DISPOSITION_STATUS.PendingVerification), label: 'بانتظار موافقة مدير القسم' },
  { value: String(DISPOSITION_STATUS.PendingFinalApproval), label: 'بانتظار مصادقة مدير الأرشيف' },
  { value: String(DISPOSITION_STATUS.Approved), label: 'معتمد' },
  { value: String(DISPOSITION_STATUS.Completed), label: 'مكتمل' },
  { value: String(DISPOSITION_STATUS.Rejected), label: 'مرفوض' },
]

/** One queue of request cards. Module-level (not defined inside the page) so React sees the
 *  same component type across renders instead of remounting every card on each keystroke. */
function Queue({ rows, action, filtering, onOpen, onCertificate }: {
  rows: DispositionRequest[]
  action?: string
  filtering: boolean
  onOpen: (r: DispositionRequest) => void
  onCertificate: (id: number) => void
}) {
  return (
    rows.length === 0
      ? <p className="dispo-empty">{filtering ? 'لا توجد طلبات مطابقة للتصفية' : 'لا توجد طلبات'}</p>
      : (
        <div className="dispo-grid">
          {rows.map((r) => (
            <div key={r.id} className="dispo-card">
              <div className="dispo-card__top">
                <span className="dispo-card__no">{r.documentNumber ?? `#${r.documentId}`}</span>
                <span className={`badge ${r.requestedAction === DISPOSITION_ACTION.Destroy ? 'secret' : 'internal'}`}>{r.requestedActionLabel}</span>
              </div>
              {r.documentTitle && <div className="dispo-card__title">{r.documentTitle}</div>}
              <div className="dispo-card__reason">{r.reason}</div>
              <div className="dispo-card__meta">
                <span><User size={13} aria-hidden /> {r.requestedByName ?? '—'}</span>
                <span>{r.boxCode ?? '—'}</span>
              </div>
              {r.status === DISPOSITION_STATUS.Rejected && r.rejectionReason && (
                <div className="dispo-card__rejected">سبب الرفض: {r.rejectionReason}</div>
              )}
              <div className="dispo-card__foot">
                <span className="status-pill">{r.statusLabel}</span>
                {action
                  ? <button className="btn btn-primary btn-sm" onClick={() => onOpen(r)}>{action}</button>
                  : r.certificateNumber
                    ? <button className="btn btn-ghost btn-sm" onClick={() => onCertificate(r.id)}><Award size={14} aria-hidden /> {r.certificateNumber}</button>
                    : null}
              </div>
            </div>
          ))}
        </div>
      )
  )
}

export default function DispositionPage() {
  const toast = useToast()
  /* Authority now belongs to a seat (رئيس القسم، مدير قسم الأرشيف), not to a permission code, and
     the seat holder may hold neither Edit nor Approve. So a queue tab appears when the server
     actually returned rows for it — the API only ever returns what this user may see — and the
     permission just keeps an empty queue visible to the person who owns it. */
  const canVerify = auth.hasPermission('Disposition.Edit')
  const canApprove = auth.hasPermission('Disposition.Approve')
  const canCreate = auth.hasPermission('Disposition.Create')
  const meId = auth.getUser()?.id ?? 0
  const [newOpen, setNewOpen] = useState(false)

  const [verifyQ, setVerifyQ] = useState<DispositionRequest[]>([])
  const [approveQ, setApproveQ] = useState<DispositionRequest[]>([])
  const [doneQ, setDoneQ] = useState<DispositionRequest[]>([])
  const [allQ, setAllQ] = useState<DispositionRequest[]>([])

  // ── Filters (shared by every tab) ──
  const [q, setQ] = useState('')
  const [actionF, setActionF] = useState('')
  const [statusF, setStatusF] = useState('')
  const [requesterF, setRequesterF] = useState('')
  const [busy, setBusy] = useState(false)
  const [sel, setSel] = useState<DispositionRequest | null>(null)
  const [certFor, setCertFor] = useState<number | null>(null)
  const [notes, setNotes] = useState('')

  const load = useCallback(async () => {
    try {
      const [v, a, all] = await Promise.all([
        // 200 is the server's page cap. Filtering runs over what is loaded, so a 50-row default
        // would have let a filter quietly miss requests that exist.
        canVerify ? disposition.list('Verification', 1, 200) : Promise.resolve({ items: [] as DispositionRequest[] } as never),
        canApprove ? disposition.list('FinalApproval', 1, 200) : Promise.resolve({ items: [] as DispositionRequest[] } as never),
        disposition.list(undefined, 1, 200),
      ])
      setVerifyQ(v.items); setApproveQ(a.items); setAllQ(all.items)
      setDoneQ(all.items.filter((r) => r.status === DISPOSITION_STATUS.Completed || r.status === DISPOSITION_STATUS.Rejected))
    } catch { toast.error('تعذّر تحميل الطلبات') }
  }, [toast, canVerify, canApprove])
  useEffect(() => { load() }, [load])

  /* Requests raised by the signed-in user, whatever stage they are at. Without this a requester
     with only View+Create could raise a destruction request and then have nowhere to see whether
     it was approved or rejected — the two queues below are the reviewers' inboxes, not theirs. */
  const mineQ = useMemo(() => allQ.filter((r) => r.requestedByUserId === meId), [allQ, meId])

  /** Requesters present in the loaded set — the filter offers only names that can match. */
  const requesters = useMemo(() => {
    const seen = new Map<number, string>()
    for (const r of allQ) if (r.requestedByUserId) seen.set(r.requestedByUserId, r.requestedByName ?? `#${r.requestedByUserId}`)
    return [...seen].map(([id, name]) => ({ id, name }))
  }, [allQ])

  const filtering = !!(q || actionF || statusF || requesterF)
  function clearFilters() { setQ(''); setActionF(''); setStatusF(''); setRequesterF('') }

  /** One predicate for every tab, so a filter means the same thing wherever it is applied. */
  const applyFilters = useCallback((rows: DispositionRequest[]) => {
    const needle = q.trim().toLowerCase()
    return rows.filter((r) => {
      if (needle && ![r.documentNumber, r.documentTitle, r.reason, r.requestedByName, r.boxCode]
        .some((v) => v?.toLowerCase().includes(needle))) return false
      if (actionF && String(r.requestedAction) !== actionF) return false
      if (statusF && String(r.status) !== statusF) return false
      if (requesterF && String(r.requestedByUserId) !== requesterF) return false
      return true
    })
  }, [q, actionF, statusF, requesterF])

  const fVerify = useMemo(() => applyFilters(verifyQ), [applyFilters, verifyQ])
  const fApprove = useMemo(() => applyFilters(approveQ), [applyFilters, approveQ])
  const fDone = useMemo(() => applyFilters(doneQ), [applyFilters, doneQ])
  const fMine = useMemo(() => applyFilters(mineQ), [applyFilters, mineQ])

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
      decision === 'Verify' ? 'تمت الموافقة وأُحيل الطلب إلى مدير قسم الأرشيف' : 'تم رفض الطلب')
  }
  function doApprove(decision: 'Approve' | 'Reject') {
    if (!sel) return
    if (!notes.trim()) { toast.error('الملاحظات مطلوبة'); return }
    act(() => disposition.finalApprove(sel.id, decision, notes.trim()),
      decision === 'Approve' ? 'تم الاعتماد النهائي وتنفيذ التصرّف' : 'تم رفض الطلب')
  }

  const stage = sel ? (sel.status === DISPOSITION_STATUS.PendingVerification ? 'verify' : 'approve') : null

  return (
    <div>
      <header className="page__head">
        <div>
          <h1>الاحتفاظ والتصرّف بالوثائق</h1>
        </div>
        <div style={{ display: 'flex', gap: '.5rem' }}>
          {canCreate && <button className="btn btn-primary" onClick={() => setNewOpen(true)}>＋ طلب جديد</button>}
          <button className="btn btn-ghost" onClick={load}><RefreshCw size={15} aria-hidden /> تحديث</button>
        </div>
      </header>

      {newOpen && <RequestDispositionModal onClose={() => setNewOpen(false)} onDone={load} />}

      {/* ── Filters ── applied to every tab, so switching queues keeps the same question asked. */}
      <div className="dispo-filters">
        <div className="dispo-search">
          <Search size={15} className="dispo-search__icon" aria-hidden />
          <input
            value={q} onChange={(e) => setQ(e.target.value)}
            placeholder="ابحث برقم الوثيقة أو العنوان أو السبب أو مقدّم الطلب أو الصندوق…"
            aria-label="بحث في طلبات التصرّف"
          />
        </div>
        <select value={actionF} onChange={(e) => setActionF(e.target.value)} aria-label="تصفية حسب الإجراء">
          <option value="">كل الإجراءات</option>
          <option value={DISPOSITION_ACTION.Destroy}>إتلاف</option>
          <option value={DISPOSITION_ACTION.Renew}>تجديد الحفظ</option>
        </select>
        <select value={statusF} onChange={(e) => setStatusF(e.target.value)} aria-label="تصفية حسب الحالة">
          <option value="">كل الحالات</option>
          {STATUS_OPTIONS.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
        </select>
        {/* Searchable rather than a flat list: every requester in the register ends up here. */}
        <div className="dispo-requester">
          <SearchSelect
            options={requesters.map((u) => ({ id: u.id, label: u.name }))}
            value={requesters.find((u) => String(u.id) === requesterF)?.name ?? ''}
            linked={!!requesterF}
            showAllOnOpen
            onPick={(o) => setRequesterF(String(o.id))}
            onText={(txt) => { if (!txt.trim()) setRequesterF('') }}
            placeholder="كل مقدّمي الطلبات" ariaLabel="تصفية حسب مقدّم الطلب"
          />
        </div>
        {filtering && (
          <button type="button" className="btn btn-ghost btn-sm" onClick={clearFilters}>
            <X size={14} aria-hidden /> مسح التصفية
          </button>
        )}
      </div>

      <Tabs defaultValue={canVerify ? 'verify' : canApprove ? 'approve' : 'mine'} dir="rtl">
        <TabsList>
          {(canVerify || fVerify.length > 0) && <TabsTrigger value="verify"><ShieldCheck /> بانتظار موافقة مدير القسم ({fVerify.length})</TabsTrigger>}
          {(canApprove || fApprove.length > 0) && <TabsTrigger value="approve"><Gavel /> بانتظار مصادقة مدير الأرشيف ({fApprove.length})</TabsTrigger>}
          <TabsTrigger value="mine"><Inbox /> طلباتي ({fMine.length})</TabsTrigger>
          <TabsTrigger value="done"><CheckCircle2 /> المكتملة ({fDone.length})</TabsTrigger>
        </TabsList>

        {canVerify && (
          <TabsContent value="verify">
            <Card>
              <CardHeader><CardTitle>الخطوة الأولى — موافقة مدير القسم الذي قدّم الطلب</CardTitle></CardHeader>
              <CardContent><Queue rows={fVerify} action="مراجعة" filtering={filtering} onOpen={open} onCertificate={setCertFor} /></CardContent>
            </Card>
          </TabsContent>
        )}
        {canApprove && (
          <TabsContent value="approve">
            <Card>
              <CardHeader><CardTitle>الخطوة الثانية — مصادقة مدير قسم الأرشيف</CardTitle></CardHeader>
              <CardContent><Queue rows={fApprove} action="اعتماد" filtering={filtering} onOpen={open} onCertificate={setCertFor} /></CardContent>
            </Card>
          </TabsContent>
        )}
        <TabsContent value="mine">
          <Card>
            <CardHeader><CardTitle>الطلبات التي قدّمتها — وحالتها الحالية</CardTitle></CardHeader>
            <CardContent><Queue rows={fMine} filtering={filtering} onOpen={open} onCertificate={setCertFor} /></CardContent>
          </Card>
        </TabsContent>
        <TabsContent value="done">
          <Card>
            <CardHeader><CardTitle>الطلبات المكتملة والمرفوضة</CardTitle></CardHeader>
            <CardContent>
            {fDone.length === 0
              ? <p className="dispo-empty">{filtering ? 'لا توجد طلبات مطابقة للتصفية' : 'لا توجد طلبات'}</p>
              : (
                <div className="dispo-grid">
                  {fDone.map((r) => (
                    <div key={r.id} className="dispo-card">
                      <div className="dispo-card__top">
                        <span className="dispo-card__no">{r.documentNumber ?? `#${r.documentId}`}</span>
                        <span className={`badge ${r.requestedAction === DISPOSITION_ACTION.Destroy ? 'secret' : 'internal'}`}>{r.requestedActionLabel}</span>
                      </div>
                      <div className="dispo-card__reason">
                        {r.status === DISPOSITION_STATUS.Rejected ? r.rejectionReason : r.newExpiryDate ? `تجديد حتى ${r.newExpiryDate}` : '—'}
                      </div>
                      <div className="dispo-card__foot">
                        <span className="status-pill">{r.statusLabel}</span>
                        {r.certificateNumber && (
                          <button className="btn btn-ghost btn-sm" onClick={() => setCertFor(r.id)}><Award size={14} aria-hidden /> {r.certificateNumber}</button>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
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
              <button className="btn btn-ghost btn-sm" onClick={() => setSel(null)}><X size={14} aria-hidden /> إغلاق</button>
            </header>

            <div style={{ padding: '1rem', overflow: 'auto', color: 'var(--ink-text, #211d17)' }}>
              <DispositionTimeline status={sel.status} />

              <dl className="detail-list" style={{ marginTop: '1rem' }}>
                <dt>الوثيقة</dt><dd>{sel.documentTitle ?? '—'}</dd>
                <dt>الإجراء المطلوب</dt><dd><span className={`badge ${sel.requestedAction === DISPOSITION_ACTION.Destroy ? 'secret' : 'internal'}`}>{sel.requestedActionLabel}</span></dd>
                <dt>السبب</dt><dd>{sel.reason}</dd>
                <dt>الموقع الفعلي</dt><dd className="mono">{sel.boxCode ?? '—'}</dd>
                <dt>انتهاء الحفظ</dt><dd className="mono">{sel.expiryDate ?? '—'}</dd>
                <dt>مُقدِّم الطلب</dt><dd>{sel.requestedByName ?? '—'}</dd>
              </dl>

              {/* At step 2, the verifier's notes are read-only context. */}
              {stage === 'approve' && sel.verificationNotes && (
                <div className="doc-card" style={{ background: 'rgba(0,0,0,.03)', padding: '.7rem .9rem', marginBottom: '.8rem' }}>
                  <strong>ملاحظات موافقة مدير القسم</strong> <span className="muted">({sel.verifiedByName})</span>
                  <p style={{ margin: '.3rem 0 0' }}>{sel.verificationNotes}</p>
                </div>
              )}

              <label className="field field--wide">
                <span>{stage === 'verify' ? 'ملاحظات موافقة مدير القسم *' : 'ملاحظات المصادقة النهائية *'}</span>
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
