import { useEffect, useState, useCallback, useRef } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import { auth } from '../../lib/auth'
import { documents, type DocumentListItem, DOC_STATUS_LABELS } from '../../lib/documents'
import { type PagedResult, CONFIDENTIALITY_LABELS } from '../../lib/incomingMail'
import { useToast } from '../../components/toast'
import { useAutoRefresh } from '../../lib/useAutoRefresh'
import {
  ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuLabel,
  ContextMenuSeparator, ContextMenuShortcut, ContextMenuTrigger,
} from '../../components/ui/context-menu'
import { Eye, FolderOpen, Pencil, Copy, Printer } from 'lucide-react'
import '../incoming/incoming.css'

export default function DocumentsListPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const [data, setData] = useState<PagedResult<DocumentListItem> | null>(null)
  const [search, setSearch] = useState(searchParams.get('search') ?? '')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [printRows, setPrintRows] = useState<DocumentListItem[] | null>(null)
  const [printing, setPrinting] = useState(false)
  const wantPrint = useRef(false)
  const toast = useToast()

  const canEdit = auth.hasPermission('Documents.Edit')
  const canPrint = auth.hasPermission('Documents.Print')
  const copyNumber = (num: string) => navigator.clipboard.writeText(num).catch(() => {})

  const STATUSES = [
    { v: '', label: t('documents.statuses.all') },
    { v: '0', label: t('documents.statuses.draft') },
    { v: '1', label: t('documents.statuses.active') },
    { v: '2', label: t('documents.statuses.archived') },
    { v: '3', label: t('documents.statuses.pendingDisposal') },
    { v: '4', label: t('documents.statuses.disposed') },
  ]

  const locale = i18n.language === 'ar' ? 'ar' : 'en'

  useEffect(() => {
    if (wantPrint.current && printRows) { wantPrint.current = false; window.print() }
  }, [printRows])

  async function printAll() {
    setPrinting(true)
    try {
      const all: DocumentListItem[] = []
      let p = 1
      for (;;) {
        const res = await documents.list({
          search: search || undefined,
          status: status === '' ? undefined : Number(status),
          page: p, pageSize: 100,
        })
        all.push(...res.items)
        if (res.items.length === 0 || all.length >= res.totalCount || all.length >= 5000) break
        p++
      }
      if (all.length === 0) { toast.error(t('documents.empty')); return }
      wantPrint.current = true
      setPrintRows(all)
    } catch {
      toast.error(t('documents.loadError'))
    } finally { setPrinting(false) }
  }

  const load = useCallback(async (silent = false) => {
    if (!silent) setLoading(true)
    setError('')
    try {
      const res = await documents.list({
        search: search || undefined,
        status: status === '' ? undefined : Number(status),
        page, pageSize: 15,
      })
      setData(res)
    } catch {
      if (!silent) setError(t('documents.loadError'))
    } finally { if (!silent) setLoading(false) }
  }, [search, status, page, t])

  useEffect(() => { load() }, [load])
  useAutoRefresh(() => load(true), 30000)

  // keep the URL in sync with the search box (and pick up deep-links like /app/documents?search=...)
  useEffect(() => {
    const urlSearch = searchParams.get('search') ?? ''
    if (urlSearch !== search) setSearch(urlSearch)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams])

  useEffect(() => {
    const next = new URLSearchParams(searchParams)
    if (search) next.set('search', search); else next.delete('search')
    setSearchParams(next, { replace: true })
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search])

  return (
    <div>
      <header className="page__head">
        <div>
          <span className="kicker">{t('documents.kicker')}</span>
          <h1>{t('documents.title')}</h1>
        </div>
        <div className="page__headactions">
          {auth.hasPermission('Documents.Print') && (
            <button className="btn btn-ghost" disabled={printing} onClick={printAll}>
              {printing ? t('common.loading') : `🖨 ${t('common.actions.print')}`}
            </button>
          )}
          {auth.hasPermission('Documents.Create') && (
            <>
              <Link to="/app/documents/scan" className="btn btn-seal">⎙ {t('documents.scanButton')}</Link>
              <Link to="/app/documents/new" className="btn btn-primary">{t('documents.newButton')}</Link>
            </>
          )}
        </div>
      </header>

      <div className="filters">
        <input
          className="filters__search"
          placeholder={t('documents.searchPlaceholder')}
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
        />
        <select className="filters__status" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
          {STATUSES.map((s) => <option key={s.v} value={s.v}>{s.label}</option>)}
        </select>
      </div>

      {error && <p className="login__error">{error}</p>}

      <motion.div className="doc-card table-card print-hide" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
        <table className="reg-table">
          <thead>
            <tr>
              <th>{t('documents.columns.title')}</th>
              <th>{t('documents.columns.type')}</th>
              <th>{t('documents.columns.confidentiality')}</th>
              <th>{t('documents.columns.status')}</th>
              <th>{t('documents.columns.location')}</th>
              <th>{t('documents.columns.date')}</th>
            </tr>
          </thead>
          <tbody>
            {loading && Array.from({ length: 6 }).map((_, i) => (
              <tr key={`sk${i}`} className="reg-row reg-skel">
                {Array.from({ length: 6 }).map((_, j) => <td key={j}><span className="skel-bar" /></td>)}
              </tr>
            ))}
            {!loading && data?.items.length === 0 && (
              <tr><td colSpan={6} className="reg-empty">
                <div className="empty-state">
                  <span className="empty-state__icon" aria-hidden>▤</span>
                  <span className="empty-state__text">{t('documents.empty')}</span>
                  {auth.hasPermission('Documents.Create') && (
                    <Link to="/app/documents/new" className="btn btn-ghost btn-sm">{t('documents.newButton')}</Link>
                  )}
                </div>
              </td></tr>
            )}
            {!loading && data?.items.map((d) => {
              const c = CONFIDENTIALITY_LABELS[d.confidentiality] ?? { ar: d.confidentiality, cls: 'internal' }
              return (
                <ContextMenu key={d.id}>
                  <ContextMenuTrigger asChild>
                    <tr
                      onClick={() => navigate(`/app/documents/${d.id}`)}
                      className="reg-row"
                      title="انقر للفتح · انقر بالزر الأيمن للإجراءات السريعة"
                    >
                      <td className="reg-subject">{d.title}</td>
                      <td>{d.documentTypeName}</td>
                      <td><span className={`badge ${c.cls}`}>{c.ar}</span></td>
                      <td><span className={`status-pill s-${d.status.toLowerCase()}`}>{DOC_STATUS_LABELS[d.status] ?? d.status}</span></td>
                      <td>{d.physicalLocationName ? `${d.physicalLocationName}${d.boxNumber ? ` · ${d.boxNumber}` : ''}` : '—'}</td>
                      <td className="mono">{d.expiryDate ?? '—'}</td>
                    </tr>
                  </ContextMenuTrigger>

                  <ContextMenuContent className="w-56">
                    <ContextMenuLabel className="text-xs text-muted-foreground truncate max-w-[200px]">
                      {d.documentNumber} — {d.title}
                    </ContextMenuLabel>
                    <ContextMenuSeparator />

                    <ContextMenuItem onSelect={() => navigate(`/app/documents/${d.id}`)}>
                      <Eye className="ml-2 h-4 w-4 opacity-60" />
                      فتح الوثيقة
                      <ContextMenuShortcut>↵</ContextMenuShortcut>
                    </ContextMenuItem>

                    <ContextMenuItem onSelect={() => window.open(`/app/documents/${d.id}`, '_blank')}>
                      <FolderOpen className="ml-2 h-4 w-4 opacity-60" />
                      فتح في نافذة جديدة
                    </ContextMenuItem>

                    {canEdit && (
                      <>
                        <ContextMenuSeparator />
                        <ContextMenuItem onSelect={() => navigate(`/app/documents/${d.id}/edit`)}>
                          <Pencil className="ml-2 h-4 w-4 opacity-60" />
                          تعديل
                        </ContextMenuItem>
                      </>
                    )}

                    <ContextMenuSeparator />

                    <ContextMenuItem onSelect={() => copyNumber(d.documentNumber)}>
                      <Copy className="ml-2 h-4 w-4 opacity-60" />
                      نسخ رقم الوثيقة
                      <ContextMenuShortcut>⌘C</ContextMenuShortcut>
                    </ContextMenuItem>

                    {canPrint && (
                      <ContextMenuItem onSelect={() => navigate(`/app/documents/${d.id}`)}>
                        <Printer className="ml-2 h-4 w-4 opacity-60" />
                        طباعة
                        <ContextMenuShortcut>⌘P</ContextMenuShortcut>
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

      {printRows && (
        <div className="print-register">
          <div className="print-header">
            <h2>{t('documents.title')}</h2>
            <div className="print-sub mono">{printRows.length} · {new Date().toLocaleString(locale)}</div>
            <hr />
          </div>
          <table className="reg-table print-table">
            <thead>
              <tr>
                <th>{t('documents.columns.title')}</th>
                <th>{t('documents.columns.type')}</th>
                <th>{t('documents.columns.confidentiality')}</th>
                <th>{t('documents.columns.status')}</th>
                <th>{t('documents.columns.date')}</th>
              </tr>
            </thead>
            <tbody>
              {printRows.map((d) => (
                <tr key={d.id}>
                  <td>{d.title}</td>
                  <td>{d.documentTypeName}</td>
                  <td>{CONFIDENTIALITY_LABELS[d.confidentiality]?.ar ?? d.confidentiality}</td>
                  <td>{DOC_STATUS_LABELS[d.status] ?? d.status}</td>
                  <td className="mono">{d.expiryDate ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
