import { useEffect, useState, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import { FileText, Inbox, Send, ClipboardList, AlertTriangle, CalendarClock, Trash2 } from 'lucide-react'
import { auth } from '../../lib/auth'
import { reports, type DashboardSummary } from '../../lib/reports'
import {
  lifecycle, type ExpiringDocumentDto, type DisposalRequestDto,
  DISPOSAL_STATUS_LABELS, DISPOSAL_ACTION_LABELS,
} from '../../lib/lifecycle'
import { useToast } from '../../components/toast'
import '../incoming/incoming.css'
import '../dashboard.css'
import './reports.css'

export default function ReportsPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const [sum, setSum] = useState<DashboardSummary | null>(null)
  const [expiring, setExpiring] = useState<ExpiringDocumentDto[]>([])
  const [disposals, setDisposals] = useState<DisposalRequestDto[]>([])
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const toast = useToast()
  const locale = i18n.language === 'ar' ? 'ar' : 'en'

  const canApprove = auth.hasPermission('Documents.Approve')
  const canExecute = auth.hasPermission('Documents.Delete')

  const load = useCallback(async () => {
    setError('')
    try {
      const [s, e, d] = await Promise.all([
        reports.dashboard(), lifecycle.expiring(365), lifecycle.disposalRequests(),
      ])
      setSum(s); setExpiring(e); setDisposals(d)
    } catch { setError(t('reports.loadError')) }
  }, [t])

  useEffect(() => { load() }, [load])

  async function decide(id: number, approve: boolean) {
    setBusy(true); setError('')
    try {
      await lifecycle.decide(id, approve); await load()
      toast.success(approve ? t('workflow.actions.approve') : t('workflow.actions.reject'))
    } catch { toast.error(t('reports.loadError')) }
    finally { setBusy(false) }
  }

  async function execute(id: number) {
    setBusy(true); setError('')
    try { await lifecycle.execute(id); await load(); toast.success(t('workflow.actions.close')) }
    catch { toast.error(t('reports.loadError')) }
    finally { setBusy(false) }
  }

  const kpis = sum ? [
    { icon: FileText, value: sum.totalDocuments, label: t('dashboard.stats.documents') },
    { icon: Inbox, value: sum.totalIncoming, label: t('dashboard.stats.incoming') },
    { icon: Send, value: sum.totalOutgoing, label: t('dashboard.stats.outgoing') },
    { icon: ClipboardList, value: sum.openWorkflowTasks, label: 'مهام مفتوحة' },
    { icon: AlertTriangle, value: sum.overdueWorkflowTasks, label: 'مهام متأخرة', tone: 'alert' as const },
    { icon: CalendarClock, value: sum.expiringSoon, label: 'قرب انتهاء الحفظ', tone: 'warn' as const },
    { icon: Trash2, value: sum.pendingDisposals, label: 'بانتظار الإتلاف', tone: 'warn' as const },
  ] : []

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">{t('reports.kicker')}</span>
          <h1>{t('reports.title')}</h1>
        </div>
        <button className="btn btn-ghost" onClick={load}>↻ {t('common.actions.refresh')}</button>
      </header>

      {error && <p className="login__error">{error}</p>}

      {/* KPI overview — matches the dashboard cards */}
      {sum && (
        <motion.div className="dash-kpi-row" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} style={{ marginBottom: '1.2rem' }}>
          {kpis.map((k) => {
            const Icon = k.icon
            return (
              <div key={k.label} className={`dash-kpi ${k.tone === 'alert' ? 'dash-kpi--alert' : k.tone === 'warn' ? 'dash-kpi--warn' : ''}`}>
                <span className="dash-kpi__icon"><Icon size={18} strokeWidth={1.8} /></span>
                <span className="dash-kpi__value">{k.value}</span>
                <span className="dash-kpi__label">{k.label}</span>
              </div>
            )
          })}
        </motion.div>
      )}

      {sum && (
        <motion.section className="doc-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
          <div className="detail-grid">
            <div>
              <strong>{t('dashboard.stats.documents')}</strong>
              <ul className="dist-list">{sum.documentsByStatus.map((x) => <li key={x.status}><span>{x.status}</span><span className="mono">{x.count}</span></li>)}</ul>
            </div>
            <div>
              <strong>{t('dashboard.stats.incoming')}</strong>
              <ul className="dist-list">{sum.incomingByStatus.map((x) => <li key={x.status}><span>{x.status}</span><span className="mono">{x.count}</span></li>)}</ul>
            </div>
            <div>
              <strong>{t('dashboard.stats.outgoing')}</strong>
              <ul className="dist-list">{sum.outgoingByStatus.map((x) => <li key={x.status}><span>{x.status}</span><span className="mono">{x.count}</span></li>)}</ul>
            </div>
          </div>
        </motion.section>
      )}

      <motion.section className="doc-card table-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.06 }}>
        <h3 className="detail-h3">{t('reports.sections.expiring')}</h3>
        <table className="reg-table">
          <thead><tr>
            <th>{t('documents.columns.title')}</th>
            <th>{t('documents.columns.date')}</th>
            <th>المتبقّي</th>
          </tr></thead>
          <tbody>
            {expiring.length === 0 && <tr><td colSpan={3} className="reg-empty">{t('reports.empty')}</td></tr>}
            {expiring.map((d) => (
              <tr key={d.documentId} className="reg-row" onClick={() => navigate(`/app/documents/${d.documentId}`)} title="فتح الوثيقة">
                <td className="reg-subject">{d.title}</td>
                <td className="mono">{d.expiryDate}</td>
                <td className="mono">{d.daysRemaining < 0 ? `متأخّر ${-d.daysRemaining} يوم` : `${d.daysRemaining} يوم`}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </motion.section>

      <motion.section className="doc-card table-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.12 }}>
        <h3 className="detail-h3">{t('reports.sections.disposal')}</h3>
        <table className="reg-table">
          <thead><tr>
            <th>{t('documents.title')}</th>
            <th>الإجراء</th>
            <th>{t('incoming.columns.status')}</th>
            <th>{t('common.actions.edit')}</th>
          </tr></thead>
          <tbody>
            {disposals.length === 0 && <tr><td colSpan={4} className="reg-empty">{t('reports.empty')}</td></tr>}
            {disposals.map((r) => (
              <tr key={r.id}>
                <td className="mono">
                  <a onClick={(e) => { e.preventDefault(); navigate(`/app/documents/${r.documentId}`) }}
                    href={`/app/documents/${r.documentId}`} title={r.documentTitle ?? ''}>{r.documentNumber}</a>
                </td>
                <td>{DISPOSAL_ACTION_LABELS[r.action] ?? r.action}</td>
                <td><span className={`status-pill s-${r.status.toLowerCase()}`}>{DISPOSAL_STATUS_LABELS[r.status] ?? r.status}</span></td>
                <td>
                  {r.status === 'Pending' && canApprove && (
                    <>
                      <button className="btn btn-primary btn-sm" disabled={busy} onClick={() => decide(r.id, true)}>{t('workflow.actions.approve')}</button>{' '}
                      <button className="btn btn-ghost btn-sm" disabled={busy} onClick={() => decide(r.id, false)}>{t('workflow.actions.reject')}</button>
                    </>
                  )}
                  {r.status === 'Approved' && canExecute && (
                    <button className="btn btn-seal btn-sm" disabled={busy} onClick={() => execute(r.id)}>{t('workflow.actions.close')}</button>
                  )}
                  {(r.status === 'Executed' || r.status === 'Rejected') && <span className="muted">—</span>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </motion.section>

      {/* Recent system activity — connects the report to the live audit trail */}
      {sum && sum.recentActivity.length > 0 && (
        <motion.section className="doc-card table-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.18 }}>
          <h3 className="detail-h3">آخر النشاطات</h3>
          <table className="reg-table">
            <thead><tr><th>الإجراء</th><th>السجل</th><th>المستخدم</th><th>التاريخ</th></tr></thead>
            <tbody>
              {sum.recentActivity.slice(0, 12).map((a) => (
                <tr key={a.id}>
                  <td>{a.action}</td>
                  <td className="reg-subject">{a.entityTitle || a.entityType}</td>
                  <td>{a.userName ?? '—'}</td>
                  <td className="mono">{new Date(a.createdAt).toLocaleString(locale)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </motion.section>
      )}
    </div>
  )
}
