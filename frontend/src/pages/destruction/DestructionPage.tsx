import { useEffect, useState, useCallback } from 'react'
import { motion } from 'motion/react'
import { AxiosError } from 'axios'
import { auth } from '../../lib/auth'
import {
  destruction, destructionMethods, type DestructionRequest, type DestructionMethodOption,
  DESTRUCTION_STATUS_LABELS,
} from '../../lib/destruction'

const OTHER = '__other__'
import { useToast } from '../../components/toast'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { FilePlus2, ClipboardList } from 'lucide-react'
import '../incoming/incoming.css'

const errOf = (e: unknown, f: string) => (e as AxiosError<{ error?: string }>).response?.data?.error ?? f

export default function DestructionPage() {
  const toast = useToast()
  const [requests, setRequests] = useState<DestructionRequest[]>([])
  const [busy, setBusy] = useState(false)
  const [methods, setMethods] = useState<DestructionMethodOption[]>([])
  const [form, setForm] = useState({ ids: '', reason: '', methodSel: '', customMethod: '' })
  const [execFor, setExecFor] = useState<number | null>(null)
  const [execPwd, setExecPwd] = useState('')

  const canRequest = auth.hasPermission('Destruction.Create')
  const canApprove = auth.hasPermission('Destruction.Approve')
  const canExecute = auth.hasPermission('Destruction.Delete')

  const load = useCallback(async () => {
    try {
      const r = await destruction.list()
      setRequests(r.items)
    } catch { toast.error('تعذّر تحميل البيانات') }
  }, [toast])
  useEffect(() => { load() }, [load])
  useEffect(() => {
    destructionMethods.list().then((m) => {
      const active = m.filter((x) => x.isActive)
      setMethods(active)
      setForm((f) => ({ ...f, methodSel: f.methodSel || active[0]?.label || OTHER }))
    }).catch(() => {})
  }, [])

  async function createRequest() {
    const ids = form.ids.split(/[,\s]+/).map((x) => Number(x.trim())).filter((x) => x > 0)
    if (ids.length === 0) { toast.error('أدخل أرقام الوثائق (المعرّفات)'); return }
    if (!form.reason.trim()) { toast.error('سبب الإتلاف مطلوب'); return }
    const isOther = form.methodSel === OTHER
    if (isOther && !form.customMethod.trim()) { toast.error('حدّد طريقة الإتلاف'); return }
    const label = isOther ? form.customMethod.trim() : form.methodSel
    if (!label) { toast.error('اختر طريقة الإتلاف'); return }
    setBusy(true)
    try {
      const req = await destruction.create({
        documentIds: ids, reason: form.reason.trim(),
        method: isOther ? 7 : 0, customMethod: label,
      })
      await destruction.submit(req.id)
      setForm((f) => ({ ...f, ids: '', reason: '', customMethod: '' })); await load()
      toast.success('تم إنشاء الطلب وتقديمه للاعتماد')
    } catch (e) { toast.error(errOf(e, 'تعذّر إنشاء الطلب')) }
    finally { setBusy(false) }
  }

  async function act(fn: () => Promise<unknown>, ok: string) {
    setBusy(true)
    try { await fn(); await load(); toast.success(ok) }
    catch (e) { toast.error(errOf(e, 'تعذّر تنفيذ الإجراء')) }
    finally { setBusy(false) }
  }

  async function doExecute() {
    if (execFor == null) return
    if (!execPwd) { toast.error('كلمة المرور مطلوبة للتحقق'); return }
    setBusy(true)
    try {
      await destruction.execute(execFor, { stepUpPassword: execPwd })
      setExecFor(null); setExecPwd(''); await load()
      toast.success('تم تنفيذ الإتلاف وإصدار الشهادة')
    } catch (e) { toast.error(errOf(e, 'تعذّر التنفيذ')) }
    finally { setBusy(false) }
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">الحوكمة والامتثال</span>
          <h1>الإتلاف الآمن للوثائق</h1>
        </div>
        <button className="btn btn-ghost" onClick={load}>↻ تحديث</button>
      </header>

      <Tabs defaultValue="requests" dir="rtl">
        <TabsList variant="line" size="lg">
          {canRequest && <TabsTrigger value="new"><FilePlus2 /> طلب جديد</TabsTrigger>}
          <TabsTrigger value="requests"><ClipboardList /> طلبات الإتلاف</TabsTrigger>
        </TabsList>

      {canRequest && (
        <TabsContent value="new">
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <h3 className="detail-h3">طلب إتلاف جديد</h3>
          <p className="muted">يُتلَف المحتوى نهائيًا مع الاحتفاظ بالبيانات الوصفية (سجل شاهد). تُفحص الأهلية تلقائيًا (انتهاء الحفظ، لا حجز قانوني، لا دورة عمل مفتوحة).</p>
          <div className="form-grid">
            <label className="field field--wide"><span>معرّفات الوثائق (مفصولة بفواصل)</span>
              <input value={form.ids} onChange={(e) => setForm((f) => ({ ...f, ids: e.target.value }))} dir="ltr" placeholder="مثال: 12, 15, 18" /></label>
            <label className="field"><span>طريقة الإتلاف</span>
              <select value={form.methodSel} onChange={(e) => setForm((f) => ({ ...f, methodSel: e.target.value }))}>
                {methods.map((m) => <option key={m.id} value={m.label}>{m.label}</option>)}
                <option value={OTHER}>أخرى (حدّد الطريقة)</option>
              </select></label>
            {form.methodSel === OTHER && (
              <label className="field"><span>حدّد الطريقة</span>
                <input value={form.customMethod} onChange={(e) => setForm((f) => ({ ...f, customMethod: e.target.value }))} placeholder="اكتب طريقة الإتلاف" /></label>
            )}
            <label className="field field--wide"><span>السبب</span>
              <input value={form.reason} onChange={(e) => setForm((f) => ({ ...f, reason: e.target.value }))} /></label>
          </div>
          <div className="form-actions"><button className="btn btn-seal" disabled={busy} onClick={createRequest}>تقديم طلب الإتلاف</button></div>
        </motion.section>
        </TabsContent>
      )}

      <TabsContent value="requests">
      <motion.section className="doc-card table-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.05 }}>
        <h3 className="detail-h3">طلبات الإتلاف</h3>
        <table className="reg-table">
          <thead><tr><th>#</th><th>السبب</th><th>الطالب</th><th>الوثائق</th><th>الحالة</th><th>الإجراءات</th></tr></thead>
          <tbody>
            {requests.length === 0 && <tr><td colSpan={6} className="reg-empty">لا توجد طلبات</td></tr>}
            {requests.map((r) => (
              <tr key={r.id}>
                <td className="mono">{r.id}</td>
                <td className="reg-subject">{r.reason}</td>
                <td>{r.requestedByName ?? '—'}</td>
                <td className="mono">{r.items.length}</td>
                <td><span className={`status-pill s-${r.status.toLowerCase()}`}>{DESTRUCTION_STATUS_LABELS[r.status] ?? r.status}</span></td>
                <td className="row-actions">
                  {r.status === 'PendingApproval' && canApprove && (
                    <>
                      <button className="btn btn-primary btn-sm" disabled={busy} onClick={() => act(() => destruction.approve(r.id), 'تم الاعتماد')}>اعتماد</button>{' '}
                      <button className="btn btn-ghost btn-sm" disabled={busy} onClick={() => act(() => destruction.reject(r.id), 'تم الرفض')}>رفض</button>
                    </>
                  )}
                  {(r.status === 'Approved' || r.status === 'PendingApproval') && canExecute && (
                    <button className="btn btn-seal btn-sm" disabled={busy} onClick={() => { setExecFor(r.id); setExecPwd('') }}>تنفيذ الإتلاف</button>
                  )}
                  {r.status === 'Completed' && (
                    <button className="btn btn-ghost btn-sm" onClick={() => destruction.downloadCertificate(r.id)}>⬇ الشهادة</button>
                  )}
                  {(r.status === 'Draft' || r.status === 'PendingApproval') && canRequest && (
                    <button className="btn btn-ghost btn-sm" disabled={busy} onClick={() => act(() => destruction.cancel(r.id), 'تم الإلغاء')}>إلغاء</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </motion.section>
      </TabsContent>
      </Tabs>

      {execFor != null && (
        <div className="shell__backdrop shell__backdrop--drawer" style={{ display: 'grid', placeItems: 'center', zIndex: 300 }} onClick={() => setExecFor(null)}>
          <div className="doc-card" style={{ maxWidth: 420, width: '90%' }} onClick={(e) => e.stopPropagation()}>
            <h3 className="detail-h3">تأكيد التنفيذ — تحقق أمني</h3>
            <p className="muted">هذا إجراء نهائي لا يمكن التراجع عنه. أعد إدخال كلمة المرور للمصادقة.</p>
            <label className="field"><span>كلمة المرور</span>
              <input type="password" value={execPwd} onChange={(e) => setExecPwd(e.target.value)} autoFocus /></label>
            <div className="form-actions">
              <button className="btn btn-seal" disabled={busy} onClick={doExecute}>تنفيذ الإتلاف نهائيًا</button>
              <button className="btn btn-ghost" onClick={() => setExecFor(null)}>إلغاء</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
