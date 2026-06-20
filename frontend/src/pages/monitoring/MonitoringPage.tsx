import { useState, useEffect, useCallback } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import { ShieldCheck, ShieldAlert, ChevronDown, ChevronUp, RefreshCw } from 'lucide-react'
import {
  auditApi,
  type AuditLogEntry,
  type AuditLogFilter,
  type AuditUser,
  type ChainVerifyResult,
  ACTION_AR,
  ENTITY_AR,
  ACTION_COLOR,
} from '../../lib/audit'
import { auth } from '../../lib/auth'
import './monitoring.css'

// ── Helpers ─────────────────────────────────────────────────────────────────
function fmtDate(iso: string) {
  const d = new Date(iso)
  return d.toLocaleString('ar-SA', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
    hour12: false,
  })
}

function JsonDiff({ label, json }: { label: string; json: string | null }) {
  if (!json) return null
  let parsed: unknown
  try { parsed = JSON.parse(json) } catch { parsed = json }
  return (
    <div className="mon-diff">
      <span className="mon-diff__label">{label}</span>
      <pre className="mon-diff__body">{JSON.stringify(parsed, null, 2)}</pre>
    </div>
  )
}

// ── Row component ────────────────────────────────────────────────────────────
function LogRow({ entry }: { entry: AuditLogEntry }) {
  const [open, setOpen] = useState(false)
  const hasDiff = entry.oldValues || entry.newValues

  const actionLabel = ACTION_AR[entry.action] ?? entry.action
  const entityLabel = entry.entityType ? (ENTITY_AR[entry.entityType] ?? entry.entityType) : '—'
  const actionColor = ACTION_COLOR[entry.action] ?? 'var(--ink-soft)'

  return (
    <>
      <tr
        className={`mon-row ${open ? 'is-open' : ''} ${hasDiff ? 'is-expandable' : ''}`}
        onClick={() => hasDiff && setOpen((o) => !o)}
      >
        <td className="mon-cell mon-cell--time">{fmtDate(entry.createdAt)}</td>
        <td className="mon-cell">
          <div className="mon-user">
            <span className="mon-user__name">{entry.userFullName ?? '—'}</span>
            <span className="mon-user__email">{entry.userEmail ?? 'system'}</span>
          </div>
        </td>
        <td className="mon-cell">
          <span className="mon-action-badge" style={{ '--action-color': actionColor } as React.CSSProperties}>
            {actionLabel}
          </span>
        </td>
        <td className="mon-cell">
          <span className="mon-entity">{entityLabel}</span>
          {entry.entityId ? <span className="mon-entity-id">#{entry.entityId}</span> : null}
        </td>
        <td className="mon-cell mon-cell--title">{entry.entityTitle ?? '—'}</td>
        <td className="mon-cell mon-cell--ip">{entry.ipAddress ?? '—'}</td>
        <td className="mon-cell mon-cell--expand">
          {hasDiff && (open ? <ChevronUp size={14} /> : <ChevronDown size={14} />)}
        </td>
      </tr>
      {hasDiff && open && (
        <tr className="mon-detail-row">
          <td colSpan={7}>
            <motion.div
              className="mon-detail"
              initial={{ height: 0, opacity: 0 }}
              animate={{ height: 'auto', opacity: 1 }}
              exit={{ height: 0, opacity: 0 }}
            >
              <JsonDiff label="قبل التغيير" json={entry.oldValues} />
              <JsonDiff label="بعد التغيير" json={entry.newValues} />
              {entry.machineName && (
                <span className="mon-machine">الجهاز: {entry.machineName}</span>
              )}
            </motion.div>
          </td>
        </tr>
      )}
    </>
  )
}

// ── Main page ────────────────────────────────────────────────────────────────
const PAGE_SIZE = 50

const KNOWN_ACTIONS = [
  'Create', 'Edit', 'Delete', 'Forward', 'Approve', 'Archive', 'Print', 'View',
  'Login', 'Logout', 'Activated', 'Deactivated', 'PermissionsUpdated', 'PermissionsReset',
]

const BLANK_FILTER: AuditLogFilter = { entityType: '', action: '', userId: '', from: '', to: '' }

export default function MonitoringPage() {
  const canView   = auth.hasPermission('Audit.View')
  const canReseal = auth.hasPermission('Audit.Edit')

  const [entries,     setEntries]     = useState<AuditLogEntry[]>([])
  const [total,       setTotal]       = useState(0)
  const [page,        setPage]        = useState(1)
  const [loading,     setLoading]     = useState(false)
  const [chainOk,     setChainOk]     = useState<ChainVerifyResult | null>(null)
  const [verifying,   setVerifying]   = useState(false)
  const [entityTypes, setEntityTypes] = useState<string[]>([])
  const [auditUsers,  setAuditUsers]  = useState<AuditUser[]>([])

  const [filter, setFilter] = useState<AuditLogFilter>(BLANK_FILTER)

  const load = useCallback(async (f: AuditLogFilter, pg: number) => {
    setLoading(true)
    try {
      const data = await auditApi.logs({ ...f, page: pg, pageSize: PAGE_SIZE })
      setEntries(data.items)
      setTotal(data.total)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (!canView) return
    auditApi.entityTypes().then(setEntityTypes).catch(() => {})
    auditApi.users().then(setAuditUsers).catch(() => {})
    load(BLANK_FILTER, 1)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  function applyFilter() { setPage(1); load(filter, 1) }

  function resetFilter() {
    setFilter(BLANK_FILTER); setPage(1); load(BLANK_FILTER, 1)
  }

  async function verify() {
    setVerifying(true)
    try { setChainOk(await auditApi.verify()) }
    finally { setVerifying(false) }
  }

  function changePage(p: number) { setPage(p); load(filter, p) }

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  // selected user label for active-filter badge
  const selectedUser = filter.userId
    ? auditUsers.find((u) => u.id === Number(filter.userId))
    : null

  if (!canView) {
    return (
      <div className="mon-noaccess">
        <ShieldAlert size={32} />
        <p>ليس لديك صلاحية الوصول إلى سجل المراقبة</p>
        <p className="mon-noaccess__hint">تواصل مع مدير النظام لمنحك صلاحية Audit.View</p>
      </div>
    )
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">سجل المراقبة · MONITORING LOG</span>
          <h1>سجل المراقبة والنشاط</h1>
        </div>
        <div className="mon-header-actions">
          {chainOk && (
            <span className={`mon-chain-badge ${chainOk.valid ? 'is-ok' : 'is-broken'}`}>
              {chainOk.valid
                ? <><ShieldCheck size={14} /> السلسلة سليمة ({chainOk.checkedCount} سجل)</>
                : <><ShieldAlert size={14} /> سلسلة مكسورة! (سجل #{chainOk.firstBrokenId})</>}
            </span>
          )}
          <button className="btn btn-ghost btn-sm" onClick={verify} disabled={verifying}>
            <ShieldCheck size={14} />
            {verifying ? 'جارٍ التحقق…' : 'تحقق من السلامة'}
          </button>
          {canReseal && (
            <button className="btn btn-ghost btn-sm" onClick={async () => {
              if (confirm('إعادة ختم سلسلة التجزئة؟ هذه العملية لا يمكن التراجع عنها.')) {
                await auditApi.verify()
              }
            }}>إعادة الختم</button>
          )}
        </div>
      </header>

      {/* Active filter badge */}
      {selectedUser && (
        <div className="mon-active-filter">
          <span>المستخدم:</span>
          <strong>{selectedUser.fullName}</strong>
          <span className="mon-active-filter__email">{selectedUser.email}</span>
          <button
            className="mon-active-filter__clear"
            onClick={() => {
              const next = { ...filter, userId: '' as const }
              setFilter(next); setPage(1); load(next, 1)
            }}
          >✕</button>
        </div>
      )}

      {/* Filter toolbar */}
      <div className="doc-card mon-filters">
        <div className="mon-filters__grid">
          <label className="mon-filter-field">
            <span>نوع الجهة</span>
            <select
              value={filter.entityType}
              onChange={(e) => setFilter((f) => ({ ...f, entityType: e.target.value }))}
            >
              <option value="">الكل</option>
              {entityTypes.map((et) => (
                <option key={et} value={et}>{ENTITY_AR[et] ?? et}</option>
              ))}
            </select>
          </label>

          <label className="mon-filter-field">
            <span>الإجراء</span>
            <select
              value={filter.action}
              onChange={(e) => setFilter((f) => ({ ...f, action: e.target.value }))}
            >
              <option value="">الكل</option>
              {KNOWN_ACTIONS.map((a) => (
                <option key={a} value={a}>{ACTION_AR[a] ?? a}</option>
              ))}
            </select>
          </label>

          <label className="mon-filter-field">
            <span>المستخدم</span>
            <select
              value={filter.userId ?? ''}
              onChange={(e) => setFilter((f) => ({ ...f, userId: e.target.value ? Number(e.target.value) : '' }))}
            >
              <option value="">جميع المستخدمين</option>
              {auditUsers.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.fullName}
                </option>
              ))}
            </select>
          </label>

          <label className="mon-filter-field">
            <span>من تاريخ</span>
            <input
              type="datetime-local"
              value={filter.from}
              onChange={(e) => setFilter((f) => ({ ...f, from: e.target.value }))}
            />
          </label>

          <label className="mon-filter-field">
            <span>إلى تاريخ</span>
            <input
              type="datetime-local"
              value={filter.to}
              onChange={(e) => setFilter((f) => ({ ...f, to: e.target.value }))}
            />
          </label>
        </div>

        <div className="mon-filters__actions">
          <button className="btn btn-seal btn-sm" onClick={applyFilter} disabled={loading}>
            {loading ? <RefreshCw size={14} className="spin" /> : null}
            بحث
          </button>
          <button className="btn btn-ghost btn-sm" onClick={resetFilter}>مسح</button>
          <span className="mon-total">{total.toLocaleString('ar-SA')} سجل</span>
        </div>
      </div>

      {/* Table */}
      <div className="doc-card mon-table-wrap">
        <table className="mon-table">
          <thead>
            <tr>
              <th>الوقت</th>
              <th>المستخدم</th>
              <th>الإجراء</th>
              <th>الجهة</th>
              <th>العنوان</th>
              <th>عنوان IP</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <AnimatePresence>
              {loading ? (
                <tr><td colSpan={7} className="mon-loading">جارٍ التحميل…</td></tr>
              ) : entries.length === 0 ? (
                <tr><td colSpan={7} className="mon-empty">لا توجد سجلات مطابقة</td></tr>
              ) : (
                entries.map((e) => <LogRow key={e.id} entry={e} />)
              )}
            </AnimatePresence>
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="mon-pagination">
          <button
            className="btn btn-ghost btn-sm"
            disabled={page <= 1}
            onClick={() => changePage(page - 1)}
          >
            السابق
          </button>
          <span className="mon-page-info">
            صفحة {page} من {totalPages}
          </span>
          <button
            className="btn btn-ghost btn-sm"
            disabled={page >= totalPages}
            onClick={() => changePage(page + 1)}
          >
            التالي
          </button>
        </div>
      )}
    </div>
  )
}
