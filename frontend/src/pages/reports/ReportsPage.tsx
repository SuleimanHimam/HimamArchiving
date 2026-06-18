import { useEffect, useState, useCallback } from 'react'
import { motion } from 'motion/react'
import { auth } from '../../lib/auth'
import { reports, type DashboardSummary } from '../../lib/reports'
import {
  lifecycle, type ExpiringDocumentDto, type DisposalRequestDto,
  DISPOSAL_STATUS_LABELS, DISPOSAL_ACTION_LABELS,
} from '../../lib/lifecycle'
import { useToast } from '../../components/toast'
import '../incoming/incoming.css'
import './reports.css'

export default function ReportsPage() {
  const [sum, setSum] = useState<DashboardSummary | null>(null)
  const [expiring, setExpiring] = useState<ExpiringDocumentDto[]>([])
  const [disposals, setDisposals] = useState<DisposalRequestDto[]>([])
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const toast = useToast()

  const canApprove = auth.hasPermission('Documents.Approve')
  const canExecute = auth.hasPermission('Documents.Delete')

  const load = useCallback(async () => {
    setError('')
    try {
      const [s, e, d] = await Promise.all([
        reports.dashboard(), lifecycle.expiring(365), lifecycle.disposalRequests(),
      ])
      setSum(s); setExpiring(e); setDisposals(d)
    } catch { setError('تعذّر تحميل التقارير') }
  }, [])

  useEffect(() => { load() }, [load])

  async function decide(id: number, approve: boolean) {
    setBusy(true); setError('')
    try { await lifecycle.decide(id, approve); await load(); toast.success(approve ? 'تم اعتماد الطلب' : 'تم رفض الطلب') }
    catch { toast.error('تعذّر تنفيذ القرار') } finally { setBusy(false) }
  }
  async function execute(id: number) {
    setBusy(true); setError('')
    try { await lifecycle.execute(id); await load(); toast.success('تم تنفيذ الإتلاف') }
    catch { toast.error('تعذّر تنفيذ الإتلاف') } finally { setBusy(false) }
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">REPORTS · التقارير والدورة المستندية</span>
          <h1>التقارير والإتلاف</h1>
        </div>
        <button className="btn btn-ghost" onClick={load}>↻ تحديث</button>
      </header>

      {error && <p className="login__error">{error}</p>}

      {sum && (
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <h3 className="detail-h3">حسب الحالة</h3>
          <div className="detail-grid">
            <div>
              <strong>الوثائق</strong>
              <ul className="dist-list">{sum.documentsByStatus.map((x) => <li key={x.status}><span>{x.status}</span><span className="mono">{x.count}</span></li>)}</ul>
            </div>
            <div>
              <strong>الوارد</strong>
              <ul className="dist-list">{sum.incomingByStatus.map((x) => <li key={x.status}><span>{x.status}</span><span className="mono">{x.count}</span></li>)}</ul>
            </div>
            <div>
              <strong>الصادر</strong>
              <ul className="dist-list">{sum.outgoingByStatus.map((x) => <li key={x.status}><span>{x.status}</span><span className="mono">{x.count}</span></li>)}</ul>
            </div>
          </div>
        </motion.section>
      )}

      <motion.section className="doc-card table-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.06 }}>
        <h3 className="detail-h3">وثائق قاربت الانتهاء (خلال سنة)</h3>
        <table className="reg-table">
          <thead><tr><th>رقم الوثيقة</th><th>العنوان</th><th>تاريخ الانتهاء</th><th>الأيام المتبقية</th></tr></thead>
          <tbody>
            {expiring.length === 0 && <tr><td colSpan={4} className="reg-empty">لا توجد وثائق قاربت الانتهاء</td></tr>}
            {expiring.map((d) => (
              <tr key={d.documentId}>
                <td className="mono num">{d.documentNumber}</td><td className="reg-subject">{d.title}</td>
                <td className="mono">{d.expiryDate}</td>
                <td className="mono">{d.daysRemaining < 0 ? `منتهية منذ ${-d.daysRemaining}ي` : `${d.daysRemaining}ي`}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </motion.section>

      <motion.section className="doc-card table-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.12 }}>
        <h3 className="detail-h3">طلبات الإتلاف</h3>
        <table className="reg-table">
          <thead><tr><th>الوثيقة</th><th>الإجراء</th><th>الحالة</th><th>إجراءات</th></tr></thead>
          <tbody>
            {disposals.length === 0 && <tr><td colSpan={4} className="reg-empty">لا توجد طلبات إتلاف</td></tr>}
            {disposals.map((r) => (
              <tr key={r.id}>
                <td className="mono">{r.documentNumber}</td>
                <td>{DISPOSAL_ACTION_LABELS[r.action] ?? r.action}</td>
                <td><span className={`status-pill s-${r.status.toLowerCase()}`}>{DISPOSAL_STATUS_LABELS[r.status] ?? r.status}</span></td>
                <td>
                  {r.status === 'Pending' && canApprove && (
                    <>
                      <button className="btn btn-primary btn-sm" disabled={busy} onClick={() => decide(r.id, true)}>اعتماد</button>{' '}
                      <button className="btn btn-ghost btn-sm" disabled={busy} onClick={() => decide(r.id, false)}>رفض</button>
                    </>
                  )}
                  {r.status === 'Approved' && canExecute && (
                    <button className="btn btn-seal btn-sm" disabled={busy} onClick={() => execute(r.id)}>تنفيذ</button>
                  )}
                  {(r.status === 'Executed' || r.status === 'Rejected') && <span className="muted">—</span>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </motion.section>
    </div>
  )
}
