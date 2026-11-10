import { useEffect, useState } from 'react'
import { ArrowRight } from 'lucide-react'
import { Link } from 'react-router-dom'
import { motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import {
  FileText, Clock, Trash2,
  CheckCircle2, XCircle, DatabaseBackup,
  FolderPlus, Users, Settings, Inbox, Send, ArrowDownLeft, ArrowUpRight, type LucideIcon,
  Plus, Pencil, Check, CircleCheck, CircleSlash, RotateCcw, LogIn, KeyRound, Circle,
} from 'lucide-react'
import { auth } from '../lib/auth'
import { clearanceLabel, clearanceClass } from '../lib/shared'
import { roleLabel } from '../lib/users'
import { diwan } from '../lib/diwan'
import { internalMail } from '../lib/internalMail'
import DashboardMailbox from '../components/DashboardMailbox'
import DashboardDiwanCharts from '../components/DashboardDiwanCharts'
import { useCurrentUser } from '../lib/useCurrentUser'
import { useDateFormat } from '../lib/useCalendar'
import { reports, type DashboardSummary, type OnlineUser, type AuditItem, type DiwanDashboard as DiwanDashboardData } from '../lib/reports'
import { admin, type AutoBackupSettings } from '../lib/admin'
import { EventManager, type Event as CalEvent } from '@/components/ui/event-manager'
import { lifecycle } from '../lib/lifecycle'
import './dashboard.css'

/* ── helpers ─────────────────────────────────────────────────────────── */

type TFn = (key: string, opts?: Record<string, unknown>) => string

function greeting(h: number, t: TFn) {
  if (h >= 5 && h < 12) return t('dash.greeting.morning')
  if (h >= 12 && h < 17) return t('dash.greeting.noon')
  if (h >= 17 && h < 21) return t('dash.greeting.evening')
  return t('dash.greeting.night')
}

function timeAgo(iso: string, t: TFn): string {
  const mins = Math.floor((Date.now() - new Date(iso).getTime()) / 60000)
  if (mins < 1) return t('dash.ago.now')
  if (mins < 60) return t('dash.ago.min', { n: mins })
  const hrs = Math.floor(mins / 60)
  if (hrs < 24) return t('dash.ago.hour', { n: hrs })
  return t('dash.ago.day', { n: Math.floor(hrs / 24) })
}

const ACTION_ICON: Record<string, LucideIcon> = {
  Create: Plus, Created: Plus,
  Edit: Pencil, PermissionsUpdated: Pencil, RolesUpdated: Pencil,
  Delete: Trash2, Deleted: Trash2,
  Approve: Check, Activated: CircleCheck, Deactivated: CircleSlash,
  PermissionsReset: RotateCcw, Login: LogIn, PasswordReset: KeyRound,
}

const ROLE_COLOR: Record<string, string> = {
  'System Administrator': 'var(--brand-danger)',
  'Manager': 'var(--brand-primary)',
  'Archive Officer': 'var(--brand-success)',
  'Employee': 'var(--brand-warning)',
}

/** Animated count-up; respects prefers-reduced-motion via a fast settle. */
function useCountUp(target: number) {
  const [n, setN] = useState(0)
  useEffect(() => {
    if (target === 0) { setN(0); return }
    let cur = 0
    const step = Math.max(1, Math.ceil(target / 30))
    const id = setInterval(() => {
      cur = Math.min(cur + step, target)
      setN(cur)
      if (cur >= target) clearInterval(id)
    }, 20)
    return () => clearInterval(id)
  }, [target])
  return n
}

/* ── sub-components ──────────────────────────────────────────────────── */

/** The single dashboard stat card. Every stat row uses this so the whole page reads as one family.
 * Icon and figure sit on one line: a stat card holds two short strings, and stacking them over a
 * 44px icon block left most of the card empty. */
function KpiCard({
  label, value, icon: Icon, to, tone, i = 0,
}: {
  label: string; value: number; icon: LucideIcon; to: string;
  tone: string; i?: number;
}) {
  const { i18n } = useTranslation()
  const n = useCountUp(value)
  const numLocale = i18n.language === 'ar' ? 'ar-SA-u-nu-latn' : 'en-US'
  return (
    <motion.div
      initial={{ opacity: 0, y: 14 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: 0.05 * i, duration: 0.4, ease: [0.22, 1, 0.36, 1] }}
    >
      {/* Figure first, icon last: on a wide row the card stretches, and keeping both
          on the leading edge left two thirds of it empty. */}
      <Link to={to} className={`dash-kpi dash-kpi--${tone}`}>
        <span className="dash-kpi__body">
          <span className="dash-kpi__value">{n.toLocaleString(numLocale)}</span>
          <span className="dash-kpi__label">{label}</span>
        </span>
        <span className="dash-kpi__icon"><Icon size={20} strokeWidth={2} /></span>
      </Link>
    </motion.div>
  )
}

function QuickAction({ to, icon: Icon, label, i }: {
  to: string; icon: LucideIcon; label: string; i: number
}) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: 0.05 * i, duration: 0.35 }}
    >
      <Link to={to} className="dash-quick__btn">
        <span className="dash-quick__icon"><Icon size={20} strokeWidth={2} /></span>
        <span className="dash-quick__label">{label}</span>
      </Link>
    </motion.div>
  )
}

function ActivityItem({ item, i, isLast }: { item: AuditItem; i: number; isLast: boolean }) {
  const { t } = useTranslation()
  const Icon = ACTION_ICON[item.action] ?? Circle
  const verb = t(`dash.action.${item.action}`, { defaultValue: item.action })
  const etype = t(`dash.entity.${item.entityType}`, { defaultValue: item.entityType })
  return (
    <motion.li
      className={`dash-activity__item ${isLast ? 'is-last' : ''}`}
      initial={{ opacity: 0, x: 12 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ delay: 0.04 * i, duration: 0.35 }}
    >
      <span className="dash-activity__icon" aria-hidden><Icon size={14} /></span>
      <div className="dash-activity__body">
        <span className="dash-activity__who">{item.userName ?? t('dash.system')}</span>
        <span className="dash-activity__verb">{verb}</span>
        {item.entityTitle && (
          <span className="dash-activity__entity">{etype}: {item.entityTitle}</span>
        )}
      </div>
      <span className="dash-activity__time">{timeAgo(item.createdAt, t)}</span>
    </motion.li>
  )
}

function BackupStatusPanel({ backup }: { backup: AutoBackupSettings }) {
  const { t } = useTranslation()
  const { dateTime } = useDateFormat()
  const isOff = !backup.enabled
  const isOk = backup.lastRunStatus === 'Success'
  const isBad = backup.lastRunStatus === 'Failed'

  return (
    <div className="dash-panel dash-backup">
      <div className="dash-panel__head">
        <span className="dash-panel__title">{t('dash.backup.title')}</span>
        <span className={`dash-backup__pill ${isOff ? 'is-off' : isOk ? 'is-ok' : isBad ? 'is-bad' : 'is-idle'}`}>
          {isOff ? t('dash.backup.off') : isOk ? t('dash.backup.ok') : isBad ? t('dash.backup.bad') : t('dash.backup.pending')}
        </span>
      </div>

      <div className="dash-backup__body">
        <div className="dash-backup__icon">
          {isOk ? <CheckCircle2 size={22} /> : isBad ? <XCircle size={22} /> : <DatabaseBackup size={22} />}
        </div>
        <div className="dash-backup__info">
          {backup.lastRunAt ? (
            <>
              <span className="dash-backup__time">{timeAgo(backup.lastRunAt, t)}</span>
              <span className="dash-backup__abs">{dateTime(backup.lastRunAt)}</span>
            </>
          ) : (
            <span className="dash-backup__time">{t('dash.backup.neverRun')}</span>
          )}
          {isBad && backup.lastRunError && (
            <span className="dash-backup__error">{backup.lastRunError}</span>
          )}
        </div>
      </div>

      <Link to="/app/backup" className="dash-backup__link">
        {t('dash.backup.manage')} <ArrowRight size={14} className="icon-dir" aria-hidden />
      </Link>
    </div>
  )
}

function OnlineChip({ user, i }: { user: OnlineUser; i: number }) {
  const { t } = useTranslation()
  const mins = Math.floor((Date.now() - new Date(user.lastSeenAt).getTime()) / 60000)
  const isHot = mins < 2
  const color = ROLE_COLOR[user.role ?? ''] ?? 'var(--brand-primary)'
  return (
    <motion.div
      className="dash-online__chip"
      initial={{ opacity: 0, scale: 0.85 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ delay: 0.06 * i, duration: 0.3 }}
      title={`${roleLabel(user.role)} · ${timeAgo(user.lastSeenAt, t)}`}
    >
      <div className="dash-online__avatar" style={{ background: color }}>
        {user.fullName[0]}
        <span className={`dash-online__dot ${isHot ? 'is-hot' : ''}`} />
      </div>
      <div className="dash-online__info">
        <span className="dash-online__name">{user.fullName}</span>
        <span className="dash-online__role">{roleLabel(user.role)}</span>
      </div>
    </motion.div>
  )
}

/* ── main page ───────────────────────────────────────────────────────── */

export default function DashboardPage() {
  const { t, i18n } = useTranslation()
  const { data: user } = useCurrentUser()
  const [s, setS] = useState<DashboardSummary | null>(null)
  const [online, setOnline] = useState<OnlineUser[]>([])
  const [backup, setBackup] = useState<AutoBackupSettings | null>(null)
  const [now, setNow] = useState(new Date())
  const canViewBackup = auth.hasPermission('Backup.View')
  // Presence is data about other people; the panel follows the staff-list permission.
  const canSeeOnline = auth.hasPermission('Users.View')
  const canSeeAllActivity = auth.hasPermission('Audit.View')

  // live clock
  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 1000)
    return () => clearInterval(id)
  }, [])

  useEffect(() => {
    reports.dashboard().then(setS).catch(() => { })
  }, [])

  // Live mail counts: المراسلات (inbox/sent) + الديوان (incoming/outgoing) for the metric cards + charts.
  const [corr, setCorr] = useState<Record<string, number>>({})
  useEffect(() => {
    const count = (p: Promise<{ totalCount: number }>) => p.then((r) => r.totalCount).catch(() => 0)
    Promise.all([
      count(internalMail.list({ folder: 'inbox', pageSize: 1 })),
      count(internalMail.list({ folder: 'sent', pageSize: 1 })),
      count(diwan.list({ folder: 'incoming', pageSize: 1 })),
      count(diwan.list({ folder: 'outgoing', pageSize: 1 })),
    ]).then(([inboxN, sentN, dInN, dOutN]) => setCorr({
      'internal-in': inboxN, 'internal-out': sentN, 'diwan-in': dInN, 'diwan-out': dOutN,
    })).catch(() => { })
  }, [])

  // Diwan bureau volumes + real month-bucketed mail/attachment series for the charts.
  const [dd, setDd] = useState<DiwanDashboardData | null>(null)
  const [diwanYear, setDiwanYear] = useState<number | undefined>(undefined)
  const [diwanUnit, setDiwanUnit] = useState<number | null>(null)
  useEffect(() => {
    reports.diwanDashboard(diwanYear, diwanUnit).then(setDd).catch(() => { })
  }, [diwanYear, diwanUnit])

  // Retention-expiry dates become calendar events for the big EventManager calendar.
  const [calEvents, setCalEvents] = useState<CalEvent[]>([])
  useEffect(() => {
    lifecycle.expiring(365).then((list) => setCalEvents(list.map((e) => {
      const d = new Date(`${e.expiryDate}T09:00:00`)
      return {
        id: String(e.documentId),
        title: `${e.documentNumber} — ${e.title}`,
        description: t('dash.expiringDesc'),
        startTime: d,
        endTime: new Date(d.getTime() + 60 * 60 * 1000),
        color: e.daysRemaining < 0 ? 'red' : e.daysRemaining <= 30 ? 'orange' : 'blue',
        category: 'Retention',
        tags: e.daysRemaining < 0 ? ['Urgent'] : [],
      }
    }))).catch(() => { })
  }, [])

  useEffect(() => {
    if (!canViewBackup) return
    admin.autoBackup.get().then(setBackup).catch(() => { })
  }, [canViewBackup])

  // fetch online users every 30 s — only for callers allowed to see the staff list
  useEffect(() => {
    if (!canSeeOnline) return
    const run = () => reports.onlineUsers().then(setOnline).catch(() => { })
    run()
    const id = setInterval(run, 30_000)
    return () => clearInterval(id)
  }, [canSeeOnline])

  const hour = now.getHours()
  // `ar-SA` would render Hijri — pin the Gregorian calendar explicitly.
  const locale = i18n.language === 'ar' ? 'ar-u-ca-gregory-nu-latn' : 'en-GB'
  const dateStr = now.toLocaleDateString(locale, {
    weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
  })
  const timeStr = now.toLocaleTimeString(locale, {
    hour: '2-digit', minute: '2-digit',
  })

  return (
    <div className="dash">
      {/* ── Hero ── */}
      <motion.header
        className="dash-hero"
        initial={{ opacity: 0, y: -10 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5 }}
      >
        <div className="dash-hero__main">
          <h1 className="dash-hero__name">
            {t('dash.hero', { greeting: greeting(hour, t), name: user?.fullName ?? '…' })}
          </h1>
          <p className="dash-hero__sub">
            <span>{user?.jobTitle}</span>
            {user?.clearance && (
              <span className={`badge ${clearanceClass(user.clearance)}`}>{clearanceLabel(user.clearance)}</span>
            )}
          </p>
        </div>
        <div className="dash-hero__clock">
          <div>
            <div className="dash-hero__time">{timeStr}</div>
            <div className="dash-hero__date">{dateStr}</div>
          </div>
        </div>
      </motion.header>

      {/* ── KPI row — records and correspondence on one line ──
           Previously two rows under a "المراسلات والديوان" heading. Merged on
           request; the heading went with it, since it can no longer label a
           subset of a single row. Each card's own label still says what it is. */}
      {s && (
        <div className="dash-kpi-row dash-kpi-row--wide">
          <KpiCard i={0} label={t('dash.kpi.documents')} value={s.totalDocuments} icon={FileText} to="/app/documents" tone="doc" />
          <KpiCard i={1} label={t('dash.kpi.expiringSoon')} value={s.expiringSoon} icon={Clock} to="/app/disposition" tone="warn" />
          <KpiCard i={2} label={t('dash.kpi.disposals')} value={s.pendingDisposals} icon={Trash2} to="/app/disposition" tone="dispose" />
          {auth.hasPermission('Correspondence.View') && (
            <>
              <KpiCard i={3} tone="mail-in" icon={ArrowDownLeft} label={t('corr.folders.internalIn')} value={corr['internal-in'] ?? 0} to="/app/internal?folder=inbox" />
              <KpiCard i={4} tone="mail-out" icon={ArrowUpRight} label={t('corr.folders.internalOut')} value={corr['internal-out'] ?? 0} to="/app/internal?folder=sent" />
              <KpiCard i={5} tone="dv-in" icon={Inbox} label={t('corr.folders.externalIn')} value={corr['diwan-in'] ?? 0} to="/app/diwan?folder=incoming" />
              <KpiCard i={6} tone="dv-out" icon={Send} label={t('corr.folders.externalOut')} value={corr['diwan-out'] ?? 0} to="/app/diwan?folder=outgoing" />
            </>
          )}
        </div>
      )}

      {/* ── Diwan: per-bureau tiles, inline mailbox, real monthly series ── */}
      {auth.hasPermission('Correspondence.View') && dd && (
        <>
          <DashboardDiwanCharts data={dd} onYearChange={setDiwanYear} onUnitChange={setDiwanUnit} />
          <DashboardMailbox />
        </>
      )}

      {/* ── Quick actions ── */}
      <div className="dash-panel">
        <div className="dash-panel__head">
          <span className="dash-panel__title">{t('dash.activity.quickActions', { defaultValue: i18n.language === 'ar' ? 'إجراءات سريعة' : 'Quick actions' })}</span>
        </div>
        <div className="dash-panel__head" style={{ background: 'var(--brand-surface)', borderBottom: 'none', padding: '14px 18px' }}>
          <div className="dash-quick" style={{ width: '100%' }}>
            {/* Each tile is gated on the permission its destination needs — a shortcut to a screen
                that answers 403 is worse than no shortcut. The middle tile read «الصلاحيات» but
                opened الاجتماعات والقرارات; it goes to user administration, where roles and
                permissions actually live, and is labelled for that. */}
            {auth.hasPermission('Documents.Create') && (
              <QuickAction to="/app/documents/new" icon={FolderPlus} label={i18n.language === 'ar' ? 'وثيقة جديدة' : 'New document'} i={0} />
            )}
            {auth.hasPermission('Users.View') && (
              <QuickAction to="/app/users" icon={Users} label={i18n.language === 'ar' ? 'المستخدمون والصلاحيات' : 'Users & permissions'} i={4} />
            )}
            {auth.hasPermission('Settings.View') && (
              <QuickAction to="/app/settings" icon={Settings} label={i18n.language === 'ar' ? 'الإعدادات' : 'Settings'} i={5} />
            )}
          </div>
        </div>
      </div>

      {/* ── Section: calendar + side panels ── */}
      <div className="dash-section">
        {/* Big interactive calendar (retention-expiry events) */}
        <div className="dash-cal-main" dir="rtl">
          <EventManager
            key={calEvents.length > 0 ? 'loaded' : 'empty'}
            events={calEvents}
            defaultView="month"
            categories={['Retention']}
            availableTags={['Urgent', 'Important']}
          />
        </div>

        {/* Side stack: online + backup + activity */}
        <div className="dash-col">
          {canSeeOnline && (
          <div className="dash-panel">
            <div className="dash-panel__head">
              <span className="dash-panel__title">{t('dash.online.title')}</span>
              <span className="dash-online__count">{online.length}</span>
            </div>
            {online.length === 0 ? (
              <p className="dash-panel__empty">{t('dash.online.empty')}</p>
            ) : (
              <div className="dash-online">
                {online.map((u, i) => <OnlineChip key={u.id} user={u} i={i} />)}
              </div>
            )}
            <p className="dash-panel__hint">{t('dash.online.refresh')}</p>
          </div>
          )}

          {canViewBackup && backup && <BackupStatusPanel backup={backup} />}

          <div className="dash-panel">
            <div className="dash-panel__head">
              <span className="dash-panel__title">
                {canSeeAllActivity ? t('dash.activity.title') : t('dash.activity.mine')}
              </span>
            </div>
            {s && s.recentActivity.length > 0 ? (
              <ul className="dash-activity">
                {s.recentActivity.map((item, i) => (
                  <ActivityItem key={item.id} item={item} i={i} isLast={i === s.recentActivity.length - 1} />
                ))}
              </ul>
            ) : (
              <p className="dash-panel__empty">{t('dash.activity.empty')}</p>
            )}
          </div>
        </div>
      </div>

      <footer className="dash__foot">
        {t('dashboard.authSuccess')} · {t('dashboard.permissionsGranted', { count: user?.permissions.length ?? 0 })}
      </footer>
    </div>
  )
}