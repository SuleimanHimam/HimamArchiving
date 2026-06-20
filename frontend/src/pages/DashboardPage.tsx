import { useEffect, useState, useRef } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import {
  FileText, Inbox, Send, ListTodo, AlertTriangle, Clock, Trash2,
  Workflow as WorkflowIcon, Archive as ArchiveIcon, BarChart3, type LucideIcon,
  CheckCircle2, XCircle, DatabaseBackup,
} from 'lucide-react'
import { auth } from '../lib/auth'
import { useCurrentUser } from '../lib/useCurrentUser'
import { reports, type DashboardSummary, type OnlineUser, type AuditItem } from '../lib/reports'
import { admin, type AutoBackupSettings } from '../lib/admin'
import './dashboard.css'

// ── helpers ──────────────────────────────────────────────────────────

function greeting(h: number) {
  if (h >= 5  && h < 12) return 'صباح الخير'
  if (h >= 12 && h < 17) return 'ظهيرة الخير'
  if (h >= 17 && h < 21) return 'مساء الخير'
  return 'طاب ليلك'
}

function timeAgo(iso: string): string {
  const mins = Math.floor((Date.now() - new Date(iso).getTime()) / 60000)
  if (mins < 1) return 'الآن'
  if (mins < 60) return `منذ ${mins}د`
  const hrs = Math.floor(mins / 60)
  if (hrs < 24) return `منذ ${hrs}س`
  return `منذ ${Math.floor(hrs / 24)} يوم`
}

const ACTION_ICON: Record<string, string> = {
  Create: '✚', Created: '✚',
  Edit: '✏', PermissionsUpdated: '✏', RolesUpdated: '✏',
  Delete: '✕', Deleted: '✕',
  Approve: '✓', Activated: '◎', Deactivated: '◌',
  PermissionsReset: '↺', Login: '◉', PasswordReset: '⚿',
}
const ACTION_LABEL: Record<string, string> = {
  Create: 'أُنشئ', Created: 'أُنشئ',
  Edit: 'عُدِّل', PermissionsUpdated: 'صلاحيات مُحدَّثة', RolesUpdated: 'أدوار مُحدَّثة',
  Delete: 'حُذف', Deleted: 'حُذف',
  Approve: 'اعتُمد', Activated: 'فُعِّل', Deactivated: 'أُوقف',
  PermissionsReset: 'أُعيد ضبط الصلاحيات', Login: 'تسجيل دخول', PasswordReset: 'إعادة كلمة مرور',
}
const ENTITY_LABEL: Record<string, string> = {
  Document: 'وثيقة', IncomingMail: 'بريد وارد', OutgoingMail: 'بريد صادر',
  User: 'مستخدم', Role: 'دور', Workflow: 'سير عمل', Archive: 'أرشيف',
}

const ROLE_COLOR: Record<string, string> = {
  'System Administrator': '#9B2226',
  'Manager': '#1e3a8a',
  'Archive Officer': '#2F5D3A',
  'Employee': '#B0892D',
}

// animated counter
function useCountUp(target: number) {
  const [n, setN] = useState(0)
  const ref = useRef(false)
  useEffect(() => {
    if (ref.current || target === 0) { setN(target); return }
    ref.current = true
    let cur = 0
    const step = Math.max(1, Math.ceil(target / 30))
    const t = setInterval(() => {
      cur = Math.min(cur + step, target)
      setN(cur)
      if (cur >= target) clearInterval(t)
    }, 20)
    return () => clearInterval(t)
  }, [target])
  return n
}

// ── sub-components ───────────────────────────────────────────────────

function KpiCard({ label, value, icon: Icon, to, tone }:
  { label: string; value: number; icon: LucideIcon; to: string; tone?: 'alert' | 'warn' }) {
  const n = useCountUp(value)
  return (
    <motion.div initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }}>
      <Link to={to} className={`dash-kpi ${tone ? `dash-kpi--${tone}` : ''}`}>
        <span className="dash-kpi__icon"><Icon size={20} strokeWidth={2} /></span>
        <span className="dash-kpi__value">{n.toLocaleString('ar-SA')}</span>
        <span className="dash-kpi__label">{label}</span>
      </Link>
    </motion.div>
  )
}

function ActivityItem({ item, i }: { item: AuditItem; i: number }) {
  const icon  = ACTION_ICON[item.action]  ?? '●'
  const verb  = ACTION_LABEL[item.action] ?? item.action
  const etype = ENTITY_LABEL[item.entityType] ?? item.entityType
  return (
    <motion.li
      className="dash-activity__item"
      initial={{ opacity: 0, x: 12 }} animate={{ opacity: 1, x: 0 }}
      transition={{ delay: 0.04 * i, duration: 0.35 }}
    >
      <span className="dash-activity__icon">{icon}</span>
      <div className="dash-activity__body">
        <span className="dash-activity__who">{item.userName ?? 'النظام'}</span>
        <span className="dash-activity__verb">{verb}</span>
        {item.entityTitle && (
          <span className="dash-activity__entity">{etype}: {item.entityTitle}</span>
        )}
      </div>
      <span className="dash-activity__time mono">{timeAgo(item.createdAt)}</span>
    </motion.li>
  )
}

function BackupStatusPanel({ backup }: { backup: AutoBackupSettings }) {
  const isOff = !backup.enabled
  const isOk  = backup.lastRunStatus === 'Success'
  const isBad = backup.lastRunStatus === 'Failed'

  return (
    <div className="dash-panel dash-backup">
      <div className="dash-panel__head">
        <span className="kicker">النسخ الاحتياطي</span>
        <span className={`dash-backup__pill ${isOff ? 'is-off' : isOk ? 'is-ok' : isBad ? 'is-bad' : 'is-idle'}`}>
          {isOff ? 'غير مُفعّل' : isOk ? 'نجاح' : isBad ? 'فشل' : 'بانتظار أول تشغيل'}
        </span>
      </div>

      <div className="dash-backup__body">
        <div className="dash-backup__icon">
          {isOk ? <CheckCircle2 size={22} /> : isBad ? <XCircle size={22} /> : <DatabaseBackup size={22} />}
        </div>
        <div className="dash-backup__info">
          {backup.lastRunAt ? (
            <>
              <span className="dash-backup__time">{timeAgo(backup.lastRunAt)}</span>
              <span className="dash-backup__abs mono">{new Date(backup.lastRunAt).toLocaleString('ar-SA')}</span>
            </>
          ) : (
            <span className="dash-backup__time">لم يُشغَّل بعد</span>
          )}
          {isBad && backup.lastRunError && (
            <span className="dash-backup__error">{backup.lastRunError}</span>
          )}
        </div>
      </div>

      <Link to="/app/settings?tab=backup" className="dash-panel__hint dash-backup__link">
        إدارة إعدادات النسخ الاحتياطي ←
      </Link>
    </div>
  )
}

function OnlineChip({ user, i }: { user: OnlineUser; i: number }) {
  const mins = Math.floor((Date.now() - new Date(user.lastSeenAt).getTime()) / 60000)
  const isHot = mins < 2
  const color = ROLE_COLOR[user.role ?? ''] ?? '#6E6552'
  return (
    <motion.div
      className="dash-online__chip"
      initial={{ opacity: 0, scale: .8 }} animate={{ opacity: 1, scale: 1 }}
      transition={{ delay: 0.06 * i, duration: 0.3 }}
      title={`${user.role ?? ''} · ${timeAgo(user.lastSeenAt)}`}
    >
      <div className="dash-online__avatar" style={{ background: color }}>
        {user.fullName[0]}
        <span className={`dash-online__dot ${isHot ? 'is-hot' : ''}`} />
      </div>
      <div className="dash-online__info">
        <span className="dash-online__name">{user.fullName}</span>
        <span className="dash-online__role">{user.role ?? '—'}</span>
      </div>
    </motion.div>
  )
}

// ── main page ────────────────────────────────────────────────────────

export default function DashboardPage() {
  const { t, i18n } = useTranslation()
  const { data: user } = useCurrentUser()
  const [s, setS] = useState<DashboardSummary | null>(null)
  const [online, setOnline] = useState<OnlineUser[]>([])
  const [backup, setBackup] = useState<AutoBackupSettings | null>(null)
  const [now, setNow] = useState(new Date())
  const canViewBackup = auth.hasPermission('Backup.View')

  // live clock
  useEffect(() => {
    const t = setInterval(() => setNow(new Date()), 1000)
    return () => clearInterval(t)
  }, [])

  useEffect(() => {
    reports.dashboard().then(setS).catch(() => {})
  }, [])

  useEffect(() => {
    if (!canViewBackup) return
    admin.autoBackup.get().then(setBackup).catch(() => {})
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // fetch online users every 30 s
  useEffect(() => {
    const fetch = () => reports.onlineUsers().then(setOnline).catch(() => {})
    fetch()
    const id = setInterval(fetch, 30_000)
    return () => clearInterval(id)
  }, [])

  const MODULES = [
    { to: '/app/incoming',  label: t('dashboard.modules.incoming'),  en: 'Incoming',  icon: Inbox,       perm: 'IncomingMail.View' },
    { to: '/app/outgoing',  label: t('dashboard.modules.outgoing'),  en: 'Outgoing',  icon: Send,        perm: 'OutgoingMail.View' },
    { to: '/app/documents', label: t('dashboard.modules.documents'), en: 'Documents', icon: FileText,    perm: 'Documents.View' },
    { to: '/app/workflow',  label: t('dashboard.modules.workflow'),  en: 'Workflow',  icon: WorkflowIcon, perm: 'Workflow.View' },
    { to: '/app/archive',   label: t('dashboard.modules.archive'),   en: 'Archive',   icon: ArchiveIcon, perm: 'Archive.View' },
    { to: '/app/reports',   label: t('dashboard.modules.reports'),   en: 'Reports',   icon: BarChart3,   perm: 'Reports.View' },
  ].filter((m) => auth.hasPermission(m.perm))

  const hour = now.getHours()
  const dateStr = now.toLocaleDateString(i18n.language === 'ar' ? 'ar-SA' : 'en-GB', {
    weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
  })
  const timeStr = now.toLocaleTimeString(i18n.language === 'ar' ? 'ar-SA' : 'en-GB', {
    hour: '2-digit', minute: '2-digit',
  })

  const hasAlerts = (s?.overdueWorkflowTasks ?? 0) > 0 || (s?.expiringSoon ?? 0) > 0 || (s?.pendingDisposals ?? 0) > 0

  return (
    <div className="dash">
      {/* ── Hero ── */}
      <motion.div className="dash-hero" initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.5 }}>
        <div className="dash-hero__left">
          <p className="dash-hero__greeting">{greeting(hour)}</p>
          <h1 className="dash-hero__name">{user?.fullName ?? '...'}</h1>
          <p className="dash-hero__sub">
            {user?.jobTitle}
            {user?.clearance && <span className="badge secret" style={{ marginRight: '.5rem' }}>{user.clearance}</span>}
          </p>
        </div>
        <div className="dash-hero__right">
          <div className="dash-hero__time mono">{timeStr}</div>
          <div className="dash-hero__date">{dateStr}</div>
        </div>
      </motion.div>

      {/* ── KPI row ── */}
      {s && (
        <div className="dash-kpi-row">
          <KpiCard label="الوثائق"        value={s.totalDocuments}       icon={FileText}    to="/app/documents" />
          <KpiCard label="البريد الوارد"  value={s.totalIncoming}        icon={Inbox}       to="/app/incoming" />
          <KpiCard label="البريد الصادر"  value={s.totalOutgoing}        icon={Send}        to="/app/outgoing" />
          <KpiCard label="مهام مفتوحة"   value={s.openWorkflowTasks}    icon={ListTodo}    to="/app/workflow" />
          <KpiCard label="مهام متأخرة"   value={s.overdueWorkflowTasks} icon={AlertTriangle} to="/app/workflow" tone={s.overdueWorkflowTasks > 0 ? 'alert' : undefined} />
          <KpiCard label="تنتهي قريبًا"  value={s.expiringSoon}         icon={Clock}       to="/app/reports"  tone={s.expiringSoon > 0 ? 'warn' : undefined} />
          <KpiCard label="طلبات إتلاف"   value={s.pendingDisposals}     icon={Trash2}      to="/app/reports" />
        </div>
      )}

      {/* ── Alert bar ── */}
      {hasAlerts && s && (
        <motion.div className="dash-alerts" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
          {s.overdueWorkflowTasks > 0 && (
            <Link to="/app/workflow" className="dash-alert dash-alert--red">
              <AlertTriangle size={15} /> {s.overdueWorkflowTasks} مهام متأخرة عن موعدها — انتبه
            </Link>
          )}
          {s.expiringSoon > 0 && (
            <Link to="/app/reports" className="dash-alert dash-alert--amber">
              <Clock size={15} /> {s.expiringSoon} وثيقة تنتهي صلاحيتها خلال 30 يومًا
            </Link>
          )}
          {s.pendingDisposals > 0 && (
            <Link to="/app/reports" className="dash-alert dash-alert--neutral">
              <Trash2 size={15} /> {s.pendingDisposals} طلبات إتلاف في انتظار الاعتماد
            </Link>
          )}
        </motion.div>
      )}

      {/* ── Middle: activity + online ── */}
      <div className="dash-middle">
        {/* Activity feed */}
        <div className="dash-panel">
          <div className="dash-panel__head">
            <span className="kicker">النشاط الأخير</span>
          </div>
          {s && s.recentActivity.length > 0 ? (
            <ul className="dash-activity">
              {s.recentActivity.map((item, i) => (
                <ActivityItem key={item.id} item={item} i={i} />
              ))}
            </ul>
          ) : (
            <p className="dash-panel__empty">لا توجد نشاطات مسجّلة بعد</p>
          )}
        </div>

        {/* Right stack: online users + backup status */}
        <div className="dash-side-stack">
          <div className="dash-panel">
            <div className="dash-panel__head">
              <span className="kicker">المتصلون الآن</span>
              <span className="dash-online__count">{online.length}</span>
            </div>
            {online.length === 0 ? (
              <p className="dash-panel__empty">لا أحد متصل في الدقائق الأخيرة</p>
            ) : (
              <div className="dash-online">
                {online.map((u, i) => <OnlineChip key={u.id} user={u} i={i} />)}
              </div>
            )}
            <p className="dash-panel__hint mono">يتحدّث كل 30 ثانية</p>
          </div>

          {canViewBackup && backup && <BackupStatusPanel backup={backup} />}
        </div>
      </div>

      {/* ── Module grid ── */}
      <div className="dash-section-title">
        <span className="kicker">الوحدات المتاحة</span>
      </div>
      <div className="dash__grid">
        {MODULES.map((m, i) => {
          const Icon = m.icon
          return (
            <motion.div
              key={m.to}
              initial={{ opacity: 0, y: 18 }} animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.4, delay: 0.05 * i }}
            >
              <Link to={m.to} className="dash__cardlink">
                <div className="dash__card">
                  <span className="dash__cardicon"><Icon size={26} strokeWidth={1.8} /></span>
                  <span className="dash__cardlabel">{m.label}</span>
                  <span className="dash__carden mono">{m.en}</span>
                  <span className="dash__cardstate">{t('dashboard.enter')}</span>
                </div>
              </Link>
            </motion.div>
          )
        })}
      </div>

      <footer className="dash__foot mono">
        {t('dashboard.authSuccess')} · {t('dashboard.permissionsGranted', { count: user?.permissions.length ?? 0 })}
      </footer>
    </div>
  )
}
