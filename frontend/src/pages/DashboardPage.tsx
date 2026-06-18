import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { auth } from '../lib/auth'
import { reports, type DashboardSummary } from '../lib/reports'
import './dashboard.css'

const MODULES = [
  { to: '/app/incoming', label: 'الوارد', en: 'Incoming', icon: '↙', perm: 'IncomingMail.View' },
  { to: '/app/outgoing', label: 'الصادر', en: 'Outgoing', icon: '↗', perm: 'OutgoingMail.View' },
  { to: '/app/documents', label: 'الوثائق', en: 'Documents', icon: '▤', perm: 'Documents.View' },
  { to: '/app/workflow', label: 'سير العمل', en: 'Workflow', icon: '⇄', perm: 'Workflow.View' },
  { to: '/app/archive', label: 'الأرشيف', en: 'Archive', icon: '▦', perm: 'Archive.View' },
  { to: '/app/reports', label: 'التقارير', en: 'Reports', icon: '◷', perm: 'Reports.View' },
]

export default function DashboardPage() {
  const user = auth.getUser()
  const [s, setS] = useState<DashboardSummary | null>(null)

  useEffect(() => { reports.dashboard().then(setS).catch(() => {}) }, [])

  const stats = s ? [
    { label: 'الوثائق', value: s.totalDocuments, to: '/app/documents' },
    { label: 'الوارد', value: s.totalIncoming, to: '/app/incoming' },
    { label: 'الصادر', value: s.totalOutgoing, to: '/app/outgoing' },
    { label: 'مهامي المفتوحة', value: s.openWorkflowTasks, to: '/app/workflow' },
    { label: 'مهام متأخرة', value: s.overdueWorkflowTasks, to: '/app/workflow', alert: s.overdueWorkflowTasks > 0 },
    { label: 'قرب الانتهاء', value: s.expiringSoon, to: '/app/reports', alert: s.expiringSoon > 0 },
    { label: 'طلبات إتلاف معلّقة', value: s.pendingDisposals, to: '/app/reports', alert: s.pendingDisposals > 0 },
  ] : []

  return (
    <div className="dash">
      <motion.header
        className="page__head"
        initial={{ opacity: 0, y: -12 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.5 }}
      >
        <div>
          <span className="kicker">DASHBOARD · لوحة التحكم</span>
          <h1>أهلًا، {user?.fullName ?? 'مستخدم'}</h1>
          <p className="muted">
            {user?.jobTitle} · صلاحية الوصول: <span className="badge secret">{user?.clearance}</span>
          </p>
        </div>
        <div className="dash__roles">
          {user?.roles.map((r) => <span key={r} className="dash__role mono">{r}</span>)}
        </div>
      </motion.header>

      {stats.length > 0 && (
        <section className="dash__stats">
          {stats.map((st, i) => (
            <motion.div
              key={st.label}
              initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.4, delay: 0.04 * i }}
            >
              <Link to={st.to} className={`dash__stat ${st.alert ? 'is-alert' : ''}`}>
                <span className="dash__statvalue">{st.value}</span>
                <span className="dash__statlabel">{st.label}</span>
              </Link>
            </motion.div>
          ))}
        </section>
      )}

      <section className="dash__grid">
        {MODULES.map((m, i) => {
          const allowed = auth.hasPermission(m.perm)
          const Card = (
            <motion.div
              className={`dash__card ${allowed ? '' : 'is-locked'}`}
              initial={{ opacity: 0, y: 18 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.45, delay: 0.05 * i }}
            >
              <span className="dash__cardicon">{m.icon}</span>
              <span className="dash__cardlabel">{m.label}</span>
              <span className="dash__carden mono">{m.en}</span>
              <span className="dash__cardstate">{allowed ? 'دخول ←' : 'لا تملك صلاحية'}</span>
            </motion.div>
          )
          return allowed
            ? <Link key={m.to} to={m.to} className="dash__cardlink">{Card}</Link>
            : <div key={m.to} className="dash__cardlink">{Card}</div>
        })}
      </section>

      <footer className="dash__foot mono">
        مصادقة ناجحة · {user?.permissions.length ?? 0} صلاحية ممنوحة
      </footer>
    </div>
  )
}
