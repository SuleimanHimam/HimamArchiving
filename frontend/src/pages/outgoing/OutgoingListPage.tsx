import { useEffect, useState, useCallback } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import { auth } from '../../lib/auth'
import { outgoingMail, type OutgoingMailListItem, OUT_STATUS_LABELS } from '../../lib/outgoingMail'
import { type PagedResult, CONFIDENTIALITY_LABELS, PRIORITY_LABELS } from '../../lib/incomingMail'
import { useAutoRefresh } from '../../lib/useAutoRefresh'
import {
  ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuLabel,
  ContextMenuSeparator, ContextMenuShortcut, ContextMenuTrigger,
} from '../../components/ui/context-menu'
import { Eye, FolderOpen, BadgeCheck, Archive, Copy } from 'lucide-react'
import '../incoming/incoming.css'

export default function OutgoingListPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [data, setData] = useState<PagedResult<OutgoingMailListItem> | null>(null)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const canApprove = auth.hasPermission('OutgoingMail.Approve')
  const canArchive = auth.hasPermission('OutgoingMail.Archive')
  const copyNumber = (num: string) => navigator.clipboard.writeText(num).catch(() => {})

  const STATUSES = [
    { v: '', label: t('outgoing.statuses.all') },
    { v: '0', label: t('outgoing.statuses.draft') },
    { v: '1', label: t('outgoing.statuses.pendingApproval') },
    { v: '2', label: t('outgoing.statuses.approved') },
    { v: '3', label: t('outgoing.statuses.sent') },
    { v: '4', label: t('outgoing.statuses.archived') },
  ]

  const load = useCallback(async (silent = false) => {
    if (!silent) setLoading(true)
    setError('')
    try {
      setData(await outgoingMail.list({
        search: search || undefined,
        status: status === '' ? undefined : Number(status),
        page, pageSize: 15,
      }))
    } catch { if (!silent) setError(t('outgoing.loadError')) }
    finally { if (!silent) setLoading(false) }
  }, [search, status, page, t])

  useEffect(() => { load() }, [load])
  useAutoRefresh(() => load(true), 30000)

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">{t('outgoing.kicker')}</span>
          <h1>{t('outgoing.title')}</h1>
        </div>
        {auth.hasPermission('OutgoingMail.Create') && (
          <Link to="/app/outgoing/new" className="btn btn-seal">{t('outgoing.newButton')}</Link>
        )}
      </header>

      <div className="filters">
        <input
          className="filters__search"
          placeholder={t('outgoing.searchPlaceholder')}
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
        />
        <select className="filters__status" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
          {STATUSES.map((s) => <option key={s.v} value={s.v}>{s.label}</option>)}
        </select>
      </div>

      {error && <p className="login__error">{error}</p>}

      <motion.div className="doc-card table-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
        <table className="reg-table">
          <thead>
            <tr>
              <th>{t('outgoing.columns.number')}</th>
              <th>{t('outgoing.columns.recipient')}</th>
              <th>{t('outgoing.columns.subject')}</th>
              <th>{t('outgoing.columns.confidentiality')}</th>
              <th>{t('outgoing.columns.priority')}</th>
              <th>{t('outgoing.columns.status')}</th>
              <th>{t('outgoing.columns.date')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={7} className="reg-empty">{t('outgoing.loading')}</td></tr>}
            {!loading && data?.items.length === 0 && (
              <tr><td colSpan={7} className="reg-empty">{t('outgoing.empty')}</td></tr>
            )}
            {!loading && data?.items.map((m) => {
              const c = CONFIDENTIALITY_LABELS[m.confidentiality] ?? { ar: m.confidentiality, cls: 'internal' }
              return (
                <ContextMenu key={m.id}>
                  <ContextMenuTrigger asChild>
                    <tr
                      onClick={() => navigate(`/app/outgoing/${m.id}`)}
                      className="reg-row"
                      title="انقر للفتح · انقر بالزر الأيمن للإجراءات السريعة"
                    >
                      <td className="mono num">{m.letterNumber}</td>
                      <td>{m.recipientEntity}</td>
                      <td className="reg-subject">{m.subject}</td>
                      <td><span className={`badge ${c.cls}`}>{c.ar}</span></td>
                      <td>{PRIORITY_LABELS[m.priority] ?? m.priority}</td>
                      <td><span className={`status-pill s-${m.status.toLowerCase()}`}>{OUT_STATUS_LABELS[m.status] ?? m.status}</span></td>
                      <td className="mono">{m.sentDate ? m.sentDate.slice(0, 10) : '—'}</td>
                    </tr>
                  </ContextMenuTrigger>

                  <ContextMenuContent className="w-56">
                    <ContextMenuLabel className="text-xs text-muted-foreground truncate max-w-[200px]">
                      {m.letterNumber} — {m.recipientEntity}
                    </ContextMenuLabel>
                    <ContextMenuSeparator />

                    <ContextMenuItem onSelect={() => navigate(`/app/outgoing/${m.id}`)}>
                      <Eye className="ml-2 h-4 w-4 opacity-60" />
                      فتح الخطاب
                      <ContextMenuShortcut>↵</ContextMenuShortcut>
                    </ContextMenuItem>

                    <ContextMenuItem onSelect={() => window.open(`/app/outgoing/${m.id}`, '_blank')}>
                      <FolderOpen className="ml-2 h-4 w-4 opacity-60" />
                      فتح في نافذة جديدة
                    </ContextMenuItem>

                    {(canApprove || canArchive) && <ContextMenuSeparator />}

                    {canApprove && (
                      <ContextMenuItem onSelect={() => navigate(`/app/outgoing/${m.id}`)}>
                        <BadgeCheck className="ml-2 h-4 w-4 opacity-60" />
                        اعتماد
                      </ContextMenuItem>
                    )}

                    {canArchive && (
                      <ContextMenuItem onSelect={() => navigate(`/app/outgoing/${m.id}`)}>
                        <Archive className="ml-2 h-4 w-4 opacity-60" />
                        أرشفة
                      </ContextMenuItem>
                    )}

                    <ContextMenuSeparator />

                    <ContextMenuItem onSelect={() => copyNumber(m.letterNumber)}>
                      <Copy className="ml-2 h-4 w-4 opacity-60" />
                      نسخ رقم الخطاب
                      <ContextMenuShortcut>⌘C</ContextMenuShortcut>
                    </ContextMenuItem>
                  </ContextMenuContent>
                </ContextMenu>
              )
            })}
          </tbody>
        </table>
      </motion.div>

      {data && data.totalPages > 1 && (
        <div className="pager">
          <button className="btn btn-ghost" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            {t('common.pagination.previous')}
          </button>
          <span className="mono">
            {t('common.pagination.page', { page, total: data.totalPages })} · {t('common.pagination.count', { count: data.totalCount })}
          </span>
          <button className="btn btn-ghost" disabled={page >= data.totalPages} onClick={() => setPage((p) => p + 1)}>
            {t('common.pagination.next')}
          </button>
        </div>
      )}
    </div>
  )
}
