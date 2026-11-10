import { useState, useEffect, useCallback, useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { motion, AnimatePresence } from 'motion/react'
import { ChevronDown, ChevronUp, Trash2, ShieldAlert, ShieldCheck, Download, Stamp, Search, X } from 'lucide-react'
import {
  auditApi,
  type AuditLogEntry,
  type AuditLogFilter,
  type AuditUser,
  type ChainVerifyResult,
  actionColor,
} from '../../lib/audit'
import { auth } from '../../lib/auth'
import { useDateFormat } from '../../lib/useCalendar'
import SearchSelect from '../../components/SearchSelect'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '../../components/ui/card'
import {
  Dialog, DialogContent, DialogHeader, DialogFooter, DialogTitle, DialogDescription,
} from '../../components/ui/dialog'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import '../../styles/forms.css'
import './monitoring.css'

// ── Helpers ─────────────────────────────────────────────────────────────────
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
function LogRow({ entry, canDelete, onDelete }:
  { entry: AuditLogEntry; canDelete: boolean; onDelete: (id: number) => void }) {
  const { t } = useTranslation()
  const { dateTime } = useDateFormat()
  const { confirm } = useConfirmDialog()
  const [open, setOpen] = useState(false)
  const hasDiff = entry.oldValues || entry.newValues

  // Fall back to the raw server code when a label is missing, rather than showing
  // a blank cell — an unknown action still has to be readable in an audit log.
  const actionLabel = t(`audit.action.${entry.action}`, { defaultValue: entry.action })
  const entityLabel = entry.entityType
    ? t(`audit.entity.${entry.entityType}`, { defaultValue: entry.entityType })
    : '—'
  return (
    <>
      <tr
        className={`mon-row ${open ? 'is-open' : ''} ${hasDiff ? 'is-expandable' : ''}`}
        onClick={() => hasDiff && setOpen((o) => !o)}
      >
        <td className="mon-cell mon-cell--time">{dateTime(entry.createdAt)}</td>
        {/* Names are free text — one account here carries a 190-character name, and unclamped it
            set the table's width, pushing action/entity/title off screen behind a scroll. */}
        <td className="mon-cell mon-cell--user">
          <div className="mon-user">
            <span className="mon-user__name" title={entry.userFullName ?? undefined}>{entry.userFullName ?? '—'}</span>
            <span className="mon-user__email" title={entry.userEmail ?? undefined}>{entry.userEmail ?? 'system'}</span>
          </div>
        </td>
        <td className="mon-cell">
          <span className="mon-action-badge" style={{ '--action-color': actionColor(entry.action) } as React.CSSProperties}>
            {actionLabel}
          </span>
        </td>
        <td className="mon-cell col-opt2">
          <span className="mon-entity">{entityLabel}</span>
          {entry.entityId ? <span className="mon-entity-id">#{entry.entityId}</span> : null}
        </td>
        <td className="mon-cell mon-cell--title" title={entry.entityTitle ?? undefined}>{entry.entityTitle ?? '—'}</td>
        <td className="mon-cell mon-cell--ip col-opt3">{entry.ipAddress ?? '—'}</td>
        <td className="mon-cell mon-cell--expand">
          {canDelete && (
            <button
              className="mon-del"
              title={t('audit.hide.title')}
              onClick={async (e) => {
                e.stopPropagation()
                if (await confirm({ message: t('audit.hide.confirm'), destructive: true })) onDelete(entry.id)
              }}
            >
              <Trash2 size={13} />
            </button>
          )}
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
              <JsonDiff label={t('audit.diff.before')} json={entry.oldValues} />
              <JsonDiff label={t('audit.diff.after')} json={entry.newValues} />
              {entry.machineName && (
                <span className="mon-machine">{t('audit.machine')}: {entry.machineName}</span>
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

const BLANK_FILTER: AuditLogFilter = { entityType: '', action: '', userId: '', from: '', to: '' }

/** `YYYY-MM-DDTHH:mm` in local time — the shape both `datetime-local` and the API expect. */
function localStamp(d: Date) {
  const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`
}

/** Quick date ranges. `days: 0` means "since midnight today". */
const PRESETS = [
  { key: 'today', days: 0 },
  { key: 'd7',    days: 7 },
  { key: 'd30',   days: 30 },
] as const
type PresetKey = (typeof PRESETS)[number]['key'] | ''

function presetFrom(days: number) {
  const d = new Date()
  if (days === 0) d.setHours(0, 0, 0, 0)
  else { d.setDate(d.getDate() - days); d.setHours(0, 0, 0, 0) }
  return localStamp(d)
}

export default function MonitoringPage() {
  const { t } = useTranslation()
  const canView   = auth.hasPermission('Audit.View')
  const canDelete = auth.hasPermission('Audit.Delete')
  const canReseal = auth.hasPermission('Audit.Edit')

  const [entries,     setEntries]     = useState<AuditLogEntry[]>([])
  const [total,       setTotal]       = useState(0)
  const [page,        setPage]        = useState(1)
  const [loading,     setLoading]     = useState(false)
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [entityTypes, setEntityTypes] = useState<string[]>([])
  const [actions,     setActions]     = useState<string[]>([])
  const [auditUsers,  setAuditUsers]  = useState<AuditUser[]>([])

  // Chain integrity (§8.2). null = not yet checked.
  const [chain,       setChain]       = useState<ChainVerifyResult | null>(null)
  const [chainBusy,   setChainBusy]   = useState(false)
  const [resealOpen,  setResealOpen]  = useState(false)
  const [exporting,   setExporting]   = useState(false)

  const [filter,      setFilter]      = useState<AuditLogFilter>(BLANK_FILTER)
  const [search,      setSearch]      = useState('')
  const [preset,      setPreset]      = useState<PresetKey>('')

  const verifyChain = useCallback(async () => {
    setChainBusy(true)
    try { setChain(await auditApi.verify()) }
    catch { setChain(null) }
    finally { setChainBusy(false) }
  }, [])

  async function doReseal() {
    setResealOpen(false)
    try {
      await auditApi.reseal()
      await verifyChain()
      await load(filter, page)   // the reseal is itself audited — surface it immediately
    } catch { /* ignore — badge keeps its prior state */ }
  }

  // §8.4 requires export. No server endpoint exists, so this is built client-side
  // from the same filter the table uses. It pulls the whole filtered set, not just
  // the visible page — an export that silently covered one page would be misleading.
  async function exportCsv() {
    setExporting(true)
    try {
      const all = await auditApi.logs({ ...filter, page: 1, pageSize: 10000 })
      const cols = ['id', 'createdAt', 'userFullName', 'userEmail', 'action', 'entityType', 'entityId', 'entityTitle', 'ipAddress'] as const
      const esc = (v: unknown) => {
        const s = v == null ? '' : String(v)
        return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s
      }
      const csv = [
        cols.join(','),
        ...all.items.map((r) => cols.map((c) => esc(r[c as keyof AuditLogEntry])).join(',')),
      ].join('\r\n')
      // BOM so Excel reads Arabic content as UTF-8 rather than mojibake.
      const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' })
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `audit-log-${new Date().toISOString().slice(0, 10)}.csv`
      a.click()
      URL.revokeObjectURL(url)
    } catch { /* ignore */ }
    finally { setExporting(false) }
  }

  async function removeEntry(id: number) {
    try {
      await auditApi.deleteLog(id)
      setEntries((rows) => rows.filter((r) => r.id !== id))
      setTotal((t) => Math.max(0, t - 1))
    } catch { /* ignore — row stays if the request fails */ }
  }

  async function confirmRemoveAll() {
    setConfirmOpen(false)
    try {
      await auditApi.deleteAllLogs()
      // Reload so the "who cleared the log" entry shows as the first/only motion.
      setFilter(BLANK_FILTER); setPage(1)
      await load(BLANK_FILTER, 1)
    } catch { /* ignore */ }
  }

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
    auditApi.actions().then(setActions).catch(() => {})
    auditApi.users().then(setAuditUsers).catch(() => {})
    verifyChain()
    load(BLANK_FILTER, 1)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  /* One chip per action *as the reader sees it*. The log stores several codes for the same
     action — AuditActionFilter writes Edit from the HTTP verb while services write Updated —
     and both translate to «تعديل», so an ungrouped list showed the same word twice with no way
     to tell the two apart. Codes that share a label are one chip whose value is every spelling,
     which the API accepts comma-separated. */
  const actionGroups = useMemo(() => {
    const byLabel = new Map<string, { label: string; codes: string[] }>()
    for (const a of actions) {
      const label = t(`audit.action.${a}`, { defaultValue: a })
      const g = byLabel.get(label)
      if (g) g.codes.push(a)
      else byLabel.set(label, { label, codes: [a] })
    }
    return [...byLabel.values()].map((g) => ({ ...g, value: g.codes.join(',') }))
  }, [actions, t])

  // Every control re-queries the server immediately — no "apply" button to forget to press.
  const applyFilter = useCallback((next: AuditLogFilter) => {
    setFilter(next); setPage(1); load(next, 1)
  }, [load])

  /** Quick range. Clicking the active one clears it, so the same button is on and off. */
  function applyPreset(key: PresetKey, days: number) {
    if (preset === key) { setPreset(''); applyFilter({ ...filter, from: '', to: '' }); return }
    setPreset(key)
    applyFilter({ ...filter, from: presetFrom(days), to: '' })
  }

  /** Typing an explicit date drops the preset — the two would otherwise both look selected. */
  function applyDate(part: 'from' | 'to', value: string) {
    setPreset('')
    // A bare date means the whole day: to-midnight would silently exclude everything that day.
    applyFilter({ ...filter, [part]: value ? `${value}T${part === 'from' ? '00:00' : '23:59'}` : '' })
  }

  function clearAll() {
    setPreset(''); setSearch(''); applyFilter(BLANK_FILTER)
  }

  const selectedUser = auditUsers.find((u) => String(u.id) === String(filter.userId))

  function changePage(p: number) { setPage(p); load(filter, p) }

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  const activeFilterCount =
    (filter.entityType ? 1 : 0) + (filter.action ? 1 : 0) +
    (filter.userId ? 1 : 0) + (filter.from ? 1 : 0) + (filter.to ? 1 : 0)

  const hasAnyFilter = activeFilterCount > 0 || search.trim().length > 0

  // Free-text search runs client-side over the loaded page — the server exposes no
  // text-search param (see AuditController.Logs), so this narrows what's on screen.
  const visibleEntries = useMemo(() => {
    const q = search.trim().toLowerCase()
    if (!q) return entries
    return entries.filter((e) => {
      const actionLabel = t(`audit.action.${e.action}`, { defaultValue: e.action })
      const entityLabel = e.entityType ? t(`audit.entity.${e.entityType}`, { defaultValue: e.entityType }) : ''
      return [e.entityTitle, e.userFullName, e.userEmail, e.ipAddress, actionLabel, entityLabel]
        .some((v) => v?.toLowerCase().includes(q))
    })
  }, [entries, search, t])

  if (!canView) {
    return (
      <div className="mon-noaccess">
        <ShieldAlert size={32} aria-hidden />
        <p>{t('audit.noaccess.title')}</p>
        <p className="mon-noaccess__hint">{t('audit.noaccess.hint')}</p>
      </div>
    )
  }

  return (
    <div>
      <Dialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        {/* No dir override — direction is inherited from the document root (§14.2). */}
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('audit.hideAll.title')}</DialogTitle>
            <DialogDescription>{t('audit.hideAll.desc')}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <button className="btn btn-ghost" onClick={() => setConfirmOpen(false)}>{t('common.cancel')}</button>
            <button className="btn btn-seal" onClick={confirmRemoveAll}>{t('audit.hideAll.confirm')}</button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Reseal — destructive tier. §8.3: a reseal does not repair or erase history;
          it anchors a new verified baseline. Saying so here is the whole point. */}
      <Dialog open={resealOpen} onOpenChange={setResealOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t('audit.reseal.title')}</DialogTitle>
            <DialogDescription>{t('audit.reseal.desc')}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <button className="btn btn-ghost" onClick={() => setResealOpen(false)}>{t('common.cancel')}</button>
            <button className="btn btn-seal" onClick={doReseal}>{t('audit.reseal.confirm')}</button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <header className="page__head">
        <div>
          <h1>{t('audit.title')}</h1>
        </div>
        <div className="mon-header-actions">
          {/* Chain-integrity status badge (§8.2, §29.5). Reflects the last verify run. */}
          <button
            className={`mon-chain ${chain ? (chain.intact ? 'is-valid' : 'is-broken') : 'is-unknown'}`}
            onClick={verifyChain}
            disabled={chainBusy}
            title={t('audit.chain.recheck')}
          >
            {chain?.intact ? <ShieldCheck size={14} /> : <ShieldAlert size={14} />}
            <span>
              {chainBusy
                ? t('audit.chain.checking')
                : !chain
                  ? t('audit.chain.unknown')
                  : chain.intact
                    ? t('audit.chain.valid', { count: chain.verified })
                    : t('audit.chain.broken', { id: chain.firstBrokenId ?? '—' })}
            </span>
          </button>

          <button className="btn btn-ghost btn-sm" onClick={exportCsv} disabled={exporting || total === 0}>
            <Download size={14} /> {exporting ? t('audit.exporting') : t('audit.export')}
          </button>

          {canReseal && (
            <button className="btn btn-ghost btn-sm" onClick={() => setResealOpen(true)}>
              <Stamp size={14} /> {t('audit.reseal.action')}
            </button>
          )}

          {canDelete && (
            <button className="btn btn-ghost btn-sm mon-delall" onClick={() => setConfirmOpen(true)} disabled={entries.length === 0}>
              <Trash2 size={14} aria-hidden /> {t('audit.hideAll.action')}
            </button>
          )}
        </div>
      </header>

      {/* ── Filter bar ────────────────────────────────────────────────────────
          Everything is on screen at once. It used to be an unlabelled icon button that
          opened a panel of chips — including one chip per user, which is 120 buttons in a
          scrolling column here — so the common questions ("what did X do today?", "who
          deleted things this week?") took several clicks and a lot of scrolling. */}
      <div className="mon-filterbar">
        <div className="mon-filterbar__top">
          <div className="mon-search">
            <Search size={15} className="mon-search__icon" aria-hidden />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t('audit.filter.searchPlaceholder')}
              aria-label={t('audit.filter.search')}
            />
          </div>
          {hasAnyFilter && (
            <button type="button" className="btn btn-ghost btn-sm mon-clearall" onClick={clearAll}>
              <X size={14} aria-hidden /> {t('audit.filter.clearAll')}
            </button>
          )}
        </div>
        {/* The search box only reaches the 50 rows already loaded — the API has no text-search
            parameter. Saying so beats letting people believe they searched 600 records. */}
        <p className="mon-scope">{t('audit.filter.searchScope')}</p>

        <div className="mon-fgroup">
          <span className="mon-fgroup__label">{t('audit.filter.action')}</span>
          <div className="mon-chips">
            <button
              type="button" aria-pressed={!filter.action}
              className={`mon-chip ${!filter.action ? 'is-on' : ''}`}
              onClick={() => applyFilter({ ...filter, action: '' })}
            >{t('audit.filter.all')}</button>
            {actionGroups.map((g) => (
              <button
                key={g.value} type="button" aria-pressed={filter.action === g.value}
                className={`mon-chip ${filter.action === g.value ? 'is-on' : ''}`}
                style={{ '--action-color': actionColor(g.codes[0]) } as React.CSSProperties}
                onClick={() => applyFilter({ ...filter, action: filter.action === g.value ? '' : g.value })}
              >
                <span className="mon-chip__dot" aria-hidden />
                {g.label}
              </button>
            ))}
          </div>
        </div>

        <div className="mon-fgroup">
          <span className="mon-fgroup__label">{t('audit.filter.period')}</span>
          <div className="mon-chips">
            <button
              type="button" aria-pressed={!preset && !filter.from && !filter.to}
              className={`mon-chip ${!preset && !filter.from && !filter.to ? 'is-on' : ''}`}
              onClick={() => { setPreset(''); applyFilter({ ...filter, from: '', to: '' }) }}
            >{t('audit.filter.allTime')}</button>
            {PRESETS.map((p) => (
              <button
                key={p.key} type="button" aria-pressed={preset === p.key}
                className={`mon-chip ${preset === p.key ? 'is-on' : ''}`}
                onClick={() => applyPreset(p.key, p.days)}
              >{t(`audit.filter.${p.key}`)}</button>
            ))}
            <span className="mon-dates">
              <label>
                {t('audit.filter.from')}
                <input type="date" value={(filter.from ?? '').slice(0, 10)} onChange={(e) => applyDate('from', e.target.value)} />
              </label>
              <label>
                {t('audit.filter.to')}
                <input type="date" value={(filter.to ?? '').slice(0, 10)} onChange={(e) => applyDate('to', e.target.value)} />
              </label>
            </span>
          </div>
        </div>

        <div className="mon-fgroup mon-fgroup--selects">
          <label className="mon-field">
            <span className="mon-fgroup__label">{t('audit.filter.entity')}</span>
            <select value={filter.entityType ?? ''} onChange={(e) => applyFilter({ ...filter, entityType: e.target.value })}>
              <option value="">{t('audit.filter.allEntities')}</option>
              {entityTypes.map((et) => (
                <option key={et} value={et}>{t(`audit.entity.${et}`, { defaultValue: et })}</option>
              ))}
            </select>
          </label>
          {/* Searchable rather than a list of chips: this installation has 120 audited users. */}
          <label className="mon-field mon-field--user">
            <span className="mon-fgroup__label">{t('audit.filter.user')}</span>
            <SearchSelect
              options={auditUsers.map((u) => ({ id: u.id, label: u.fullName, sub: u.email }))}
              value={selectedUser?.fullName ?? ''}
              linked={!!selectedUser}
              showAllOnOpen
              placeholder={t('audit.filter.allUsers')}
              ariaLabel={t('audit.filter.user')}
              onPick={(o) => applyFilter({ ...filter, userId: o.id })}
              onText={(text) => { if (!text.trim()) applyFilter({ ...filter, userId: '' }) }}
            />
          </label>
        </div>
      </div>

      <p className="mon-resultline">
        {loading
          ? t('common.loading')
          : search.trim()
            ? t('audit.matchCount', { count: visibleEntries.length })
            : t('audit.totalCount', { count: total })}
      </p>

      <div className="mt-4">
        <div className="min-w-0 flex-1">
          <Card>
            <CardHeader>
              <CardTitle>{t('audit.card.title')}</CardTitle>
              <CardDescription>{t('audit.card.desc')}</CardDescription>
            </CardHeader>
            <CardContent>
              <div className="mon-table-wrap">
                <table className="mon-table">
                  <thead>
                    <tr>
                      <th>{t('audit.col.time')}</th>
                      <th>{t('audit.col.user')}</th>
                      <th>{t('audit.col.action')}</th>
                      <th className="col-opt2">{t('audit.col.entity')}</th>
                      <th>{t('audit.col.title')}</th>
                      <th className="col-opt3">{t('audit.col.ip')}</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    <AnimatePresence>
                      {loading ? (
                        <tr><td colSpan={7} className="mon-loading">{t('common.loading')}</td></tr>
                      ) : visibleEntries.length === 0 ? (
                        <tr><td colSpan={7} className="mon-empty">{t('audit.empty')}</td></tr>
                      ) : (
                        visibleEntries.map((e) => <LogRow key={e.id} entry={e} canDelete={canDelete} onDelete={removeEntry} />)
                      )}
                    </AnimatePresence>
                  </tbody>
                </table>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="mon-pagination">
          <button
            className="btn btn-ghost btn-sm"
            disabled={page <= 1}
            onClick={() => changePage(page - 1)}
          >
            {t('common.prev')}
          </button>
          <span className="mon-page-info">
            {t('common.pageOf', { page, total: totalPages })}
          </span>
          <button
            className="btn btn-ghost btn-sm"
            disabled={page >= totalPages}
            onClick={() => changePage(page + 1)}
          >
            {t('common.next')}
          </button>
        </div>
      )}
    </div>
  )
}
