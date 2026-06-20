import { useEffect, useState, useCallback } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import {
  Eye, Forward, BadgeCheck, Archive, Printer, Copy, FolderOpen,
} from 'lucide-react'
import { auth } from '../../lib/auth'
import {
  incomingMail, type IncomingMailListItem, type PagedResult,
  CONFIDENTIALITY_LABELS, STATUS_LABELS, PRIORITY_LABELS,
} from '../../lib/incomingMail'
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuLabel,
  ContextMenuSeparator,
  ContextMenuShortcut,
  ContextMenuTrigger,
} from '../../components/ui/context-menu'
import './incoming.css'

export default function IncomingListPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [data, setData] = useState<PagedResult<IncomingMailListItem> | null>(null)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const canForward  = auth.hasPermission('IncomingMail.Forward')
  const canApprove  = auth.hasPermission('IncomingMail.Approve')
  const canArchive  = auth.hasPermission('IncomingMail.Archive')
  const canPrint    = auth.hasPermission('IncomingMail.Print')

  const STATUSES = [
    { v: '', label: t('incoming.statuses.all') },
    { v: '0', label: t('incoming.statuses.new') },
    { v: '1', label: t('incoming.statuses.assigned') },
    { v: '2', label: t('incoming.statuses.inProgress') },
    { v: '3', label: t('incoming.statuses.onHold') },
    { v: '4', label: t('incoming.statuses.closed') },
    { v: '5', label: t('incoming.statuses.archived') },
  ]

  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const res = await incomingMail.list({
        search: search || undefined,
        status: status === '' ? undefined : Number(status),
        page, pageSize: 15,
      })
      setData(res)
    } catch {
      setError(t('incoming.loadError'))
    } finally { setLoading(false) }
  }, [search, status, page, t])

  useEffect(() => { load() }, [load])

  function copyNumber(num: string) {
    navigator.clipboard.writeText(num).catch(() => {})
  }

  function openDetail(id: number) {
    navigate(`/app/incoming/${id}`)
  }

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">{t('incoming.kicker')}</span>
          <h1>{t('incoming.title')}</h1>
        </div>
        {auth.hasPermission('IncomingMail.Create') && (
          <Link to="/app/incoming/new" className="btn btn-seal">{t('incoming.newButton')}</Link>
        )}
      </header>

      <div className="filters">
        <input
          className="filters__search"
          placeholder={t('incoming.searchPlaceholder')}
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
              <th>{t('incoming.columns.number')}</th>
              <th>{t('incoming.columns.sender')}</th>
              <th>{t('incoming.columns.subject')}</th>
              <th>{t('incoming.columns.confidentiality')}</th>
              <th>{t('incoming.columns.priority')}</th>
              <th>{t('incoming.columns.status')}</th>
              <th>{t('incoming.columns.date')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && <tr><td colSpan={7} className="reg-empty">{t('incoming.loading')}</td></tr>}
            {!loading && data?.items.length === 0 && (
              <tr><td colSpan={7} className="reg-empty">{t('incoming.empty')}</td></tr>
            )}
            {!loading && data?.items.map((m) => {
              const c = CONFIDENTIALITY_LABELS[m.confidentiality] ?? { ar: m.confidentiality, cls: 'internal' }
              return (
                <ContextMenu key={m.id}>
                  <ContextMenuTrigger asChild>
                    <tr
                      onClick={() => openDetail(m.id)}
                      className="reg-row"
                      title="انقر للفتح · انقر بالزر الأيمن للإجراءات السريعة"
                    >
                      <td className="mono num">{m.transactionNumber}</td>
                      <td>{m.senderEntity}</td>
                      <td className="reg-subject">{m.subject}</td>
                      <td><span className={`badge ${c.cls}`}>{c.ar}</span></td>
                      <td>{PRIORITY_LABELS[m.priority] ?? m.priority}</td>
                      <td><span className={`status-pill s-${m.status.toLowerCase()}`}>{STATUS_LABELS[m.status] ?? m.status}</span></td>
                      <td className="mono">{m.receivedDate}</td>
                    </tr>
                  </ContextMenuTrigger>

                  <ContextMenuContent className="w-56">
                    <ContextMenuLabel className="text-xs text-muted-foreground truncate max-w-[200px]">
                      {m.transactionNumber} — {m.senderEntity}
                    </ContextMenuLabel>
                    <ContextMenuSeparator />

                    <ContextMenuItem onSelect={() => openDetail(m.id)}>
                      <Eye className="ml-2 h-4 w-4 opacity-60" />
                      فتح المعاملة
                      <ContextMenuShortcut>↵</ContextMenuShortcut>
                    </ContextMenuItem>

                    <ContextMenuItem
                      onSelect={() => window.open(`/app/incoming/${m.id}`, '_blank')}
                    >
                      <FolderOpen className="ml-2 h-4 w-4 opacity-60" />
                      فتح في نافذة جديدة
                    </ContextMenuItem>

                    <ContextMenuSeparator />

                    {canForward && (
                      <ContextMenuItem onSelect={() => openDetail(m.id)}>
                        <Forward className="ml-2 h-4 w-4 opacity-60" />
                        إحالة
                      </ContextMenuItem>
                    )}

                    {canApprove && (
                      <ContextMenuItem onSelect={() => openDetail(m.id)}>
                        <BadgeCheck className="ml-2 h-4 w-4 opacity-60" />
                        اعتماد
                      </ContextMenuItem>
                    )}

                    {(canForward || canApprove) && <ContextMenuSeparator />}

                    <ContextMenuItem onSelect={() => copyNumber(m.transactionNumber)}>
                      <Copy className="ml-2 h-4 w-4 opacity-60" />
                      نسخ رقم المعاملة
                      <ContextMenuShortcut>⌘C</ContextMenuShortcut>
                    </ContextMenuItem>

                    {(canArchive || canPrint) && <ContextMenuSeparator />}

                    {canPrint && (
                      <ContextMenuItem onSelect={() => openDetail(m.id)}>
                        <Printer className="ml-2 h-4 w-4 opacity-60" />
                        طباعة
                        <ContextMenuShortcut>⌘P</ContextMenuShortcut>
                      </ContextMenuItem>
                    )}

                    {canArchive && (
                      <ContextMenuItem onSelect={() => openDetail(m.id)}>
                        <Archive className="ml-2 h-4 w-4 opacity-60" />
                        أرشفة
                      </ContextMenuItem>
                    )}
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
