import { useEffect, useState, useCallback, useRef } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { motion } from 'motion/react'
import { useTranslation } from 'react-i18next'
import { auth } from '../../lib/auth'
import { documents, type DocumentListItem, DOC_STATUS_LABELS } from '../../lib/documents'
import { type PagedResult, CONFIDENTIALITY_LABELS } from '../../lib/incomingMail'
import { useToast } from '../../components/toast'
import { useAutoRefresh } from '../../lib/useAutoRefresh'
import { foldersApi, exportApi, type Folder } from '../../lib/userFeatures'
import { useTableColumns } from '../../hooks/useTableColumns'
import { customFields, optionList, type CustomFieldDef } from '../../lib/customFields'
import {
  ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuLabel,
  ContextMenuSeparator, ContextMenuShortcut, ContextMenuTrigger,
} from '../../components/ui/context-menu'
import { Eye, FolderOpen, Pencil, Copy, Printer, Trash2 } from 'lucide-react'
import RequestDispositionModal from '../../components/RequestDispositionModal'
import '../incoming/incoming.css'

export default function DocumentsListPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { columns } = useTableColumns('documents')
  const [searchParams, setSearchParams] = useSearchParams()
  const [data, setData] = useState<PagedResult<DocumentListItem> | null>(null)
  const [search, setSearch] = useState(searchParams.get('search') ?? '')
  const [status, setStatus] = useState('')
  const [favOnly, setFavOnly] = useState(false)
  const [sharedOnly, setSharedOnly] = useState(false)
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')
  const [folderId, setFolderId] = useState('')
  const [folders, setFolders] = useState<Folder[]>([])
  const [cfDefs, setCfDefs] = useState<CustomFieldDef[]>([])
  const [cfFieldId, setCfFieldId] = useState('')
  const [cfValue, setCfValue] = useState('')
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [printRows, setPrintRows] = useState<DocumentListItem[] | null>(null)
  const [printing, setPrinting] = useState(false)
  const [exporting, setExporting] = useState(false)
  const wantPrint = useRef(false)
  const toast = useToast()
  const canDestroy = auth.hasPermission('Disposition.Create')
  const [reqFor, setReqFor] = useState<{ id: number; number: string } | null>(null)

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
        dateFrom: dateFrom || undefined,
        dateTo: dateTo || undefined,
        favoritesOnly: favOnly || undefined,
        sharedWithMe: sharedOnly || undefined,
        folderId: folderId ? Number(folderId) : undefined,
        customFieldId: cfFieldId ? Number(cfFieldId) : undefined,
        customFieldValue: cfFieldId && cfValue ? cfValue : undefined,
        page, pageSize: 15,
      })
      setData(res)
    } catch {
      if (!silent) setError(t('documents.loadError'))
    } finally { if (!silent) setLoading(false) }
  }, [search, status, dateFrom, dateTo, favOnly, sharedOnly, folderId, cfFieldId, cfValue, page, t])

  useEffect(() => { load() }, [load])
  useAutoRefresh(() => load(true), 30000)
  useEffect(() => { foldersApi.list().then(setFolders).catch(() => {}) }, [])
  useEffect(() => { customFields.list('Document').then((d) => setCfDefs(d.filter((x) => x.isActive && x.searchable))).catch(() => {}) }, [])

  async function exportAll() {
    setExporting(true)
    try { await exportApi.exportAll({ favoritesOnly: favOnly || undefined, folderId: folderId ? Number(folderId) : undefined }) }
    catch { toast.error(t('documents.loadError')) }
    finally { setExporting(false) }
  }

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
          {auth.hasPermission('Export.View') && (
            <button className="btn btn-ghost" disabled={exporting} onClick={exportAll} title="تصدير الوثائق كملف مضغوط">
              {exporting ? '…' : '⬇ تصدير ZIP'}
            </button>
          )}
          {auth.hasPermission('Documents.Print') && (
            <button className="btn btn-ghost" disabled={printing} onClick={printAll}>
              {printing ? t('common.loading') : `🖨 ${t('common.actions.print')}`}
            </button>
          )}
          {auth.hasPermission('Documents.Create') && (
            <Link to="/app/documents/new" className="btn btn-primary">{t('documents.newButton')}</Link>
          )}
        </div>
      </header>

      <div className="filters" style={{ flexWrap: 'wrap', gap: '.5rem' }}>
        <input
          className="filters__search"
          placeholder={t('documents.searchPlaceholder')}
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(1) }}
        />
        <select className="filters__status" value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }}>
          {STATUSES.map((s) => <option key={s.v} value={s.v}>{s.label}</option>)}
        </select>
        <select className="filters__status" value={folderId} onChange={(e) => { setFolderId(e.target.value); setPage(1) }} title="المجلد">
          <option value="">كل المجلدات</option>
          {folders.map((f) => <option key={f.id} value={f.id}>{f.name} ({f.documentCount})</option>)}
        </select>
        <button className="btn btn-ghost btn-sm" title="إنشاء مجلد" onClick={async () => {
          const name = window.prompt('اسم المجلد الجديد')?.trim()
          if (!name) return
          try { await foldersApi.create({ name }); setFolders(await foldersApi.list()) } catch { toast.error('تعذّر إنشاء المجلد') }
        }}>+ مجلد</button>
        <input type="date" className="filters__status" dir="ltr" value={dateFrom} title="من تاريخ"
          onChange={(e) => { setDateFrom(e.target.value); setPage(1) }} />
        <input type="date" className="filters__status" dir="ltr" value={dateTo} title="إلى تاريخ"
          onChange={(e) => { setDateTo(e.target.value); setPage(1) }} />
        <button className={`btn btn-sm ${favOnly ? 'btn-primary' : 'btn-ghost'}`}
          onClick={() => { setFavOnly((v) => !v); setPage(1) }}>★ المفضلة</button>
        <button className={`btn btn-sm ${sharedOnly ? 'btn-primary' : 'btn-ghost'}`}
          onClick={() => { setSharedOnly((v) => !v); setPage(1) }}>👥 مشاركة معي</button>

        {cfDefs.length > 0 && (
          <>
            <select className="filters__status" value={cfFieldId} title="بحث بحقل مخصص"
              onChange={(e) => { setCfFieldId(e.target.value); setCfValue(''); setPage(1) }}>
              <option value="">حقل مخصص…</option>
              {cfDefs.map((f) => <option key={f.id} value={f.id}>{f.label}</option>)}
            </select>
            {cfFieldId && (() => {
              const def = cfDefs.find((d) => String(d.id) === cfFieldId)
              if (def?.fieldType === 3) return (
                <select className="filters__status" value={cfValue} onChange={(e) => { setCfValue(e.target.value); setPage(1) }}>
                  <option value="">—</option>
                  {optionList(def).map((o) => <option key={o} value={o}>{o}</option>)}
                </select>
              )
              return (
                <input className="filters__status" placeholder="القيمة" value={cfValue}
                  dir={def?.fieldType === 1 ? 'ltr' : undefined}
                  type={def?.fieldType === 2 ? 'date' : def?.fieldType === 1 ? 'number' : 'text'}
                  onChange={(e) => { setCfValue(e.target.value); setPage(1) }} />
              )
            })()}
          </>
        )}
      </div>

      {error && <p className="login__error">{error}</p>}

      <motion.div className="doc-card table-card print-hide" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}>
        <table className="reg-table">
          <thead>
            <tr>
              {columns.map((col) => <th key={col.key}>{col.label}</th>)}
            </tr>
          </thead>
          <tbody>
            {loading && Array.from({ length: 6 }).map((_, i) => (
              <tr key={`sk${i}`} className="reg-row reg-skel">
                {columns.map((col) => <td key={col.key}><span className="skel-bar" /></td>)}
              </tr>
            ))}
            {!loading && data?.items.length === 0 && (
              <tr><td colSpan={columns.length} className="reg-empty">
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
                      {columns.map((col) => {
                        switch (col.key) {
                          case 'title': return <td key={col.key} className="reg-subject">{d.title}</td>
                          case 'type': return <td key={col.key}>{d.documentTypeName}</td>
                          case 'confidentiality': return <td key={col.key}><span className={`badge ${c.cls}`}>{c.ar}</span></td>
                          case 'status': return <td key={col.key}><span className={`status-pill s-${d.status.toLowerCase()}`}>{DOC_STATUS_LABELS[d.status] ?? d.status}</span></td>
                          case 'location': return <td key={col.key}>{d.boxCode ? `📦 ${d.boxCode}` : d.physicalLocationName ? `${d.physicalLocationName}${d.boxNumber ? ` · ${d.boxNumber}` : ''}` : '—'}</td>
                          case 'date': return <td key={col.key} className="mono">{d.expiryDate ?? '—'}</td>
                          case 'documentNumber': return <td key={col.key} className="mono num">{d.documentNumber}</td>
                          case 'documentDate': return <td key={col.key} className="mono">{d.documentDate ?? '—'}</td>
                          case 'version': return <td key={col.key} className="mono">{d.version}</td>
                          case 'box': return <td key={col.key} className="mono">{d.boxNumber ?? '—'}</td>
                          case 'file': return <td key={col.key} className="mono">{d.fileNumber ?? '—'}</td>
                          case 'createdAt': return <td key={col.key} className="mono">{d.createdAt?.slice(0, 10) ?? '—'}</td>
                          default:
                            if (col.key.startsWith('cf_')) {
                              return <td key={col.key}>{d.customValues?.[col.key.slice(3)] ?? '—'}</td>
                            }
                            return null
                        }
                      })}
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

                    {canDestroy && (
                      <>
                        <ContextMenuSeparator />
                        <ContextMenuItem className="text-destructive focus:text-destructive" onSelect={() => setReqFor({ id: d.id, number: d.documentNumber })}>
                          <Trash2 className="ml-2 h-4 w-4 opacity-60" />
                          طلب إتلاف
                        </ContextMenuItem>
                      </>
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

      {reqFor && (
        <RequestDispositionModal documentId={reqFor.id} documentNumber={reqFor.number}
          onClose={() => setReqFor(null)} />
      )}
    </div>
  )
}
