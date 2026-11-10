import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Inbox, Search, Send, Mail } from 'lucide-react'
import ArchiveTreePanel, { type ArchiveTreeSelection } from '../../components/ArchiveTreePanel'
import { archiveTree, type ArchiveTreeDocumentDto, type ArchiveTreeMode } from '../../lib/archiveTree'

const PAGE_SIZE = 20

/** الأرشيف حسب المصدر — an origin tree beside the documents it holds. Selecting any node
 *  (department, year, type, external party, direction) scopes the table to that subtree. */
export default function ArchiveByOriginView() {
  const { t } = useTranslation()
  const navigate = useNavigate()

  const [mode, setMode] = useState<ArchiveTreeMode>('Department')
  const [selection, setSelection] = useState<ArchiveTreeSelection>({ nodeId: null, path: '' })
  const [rows, setRows] = useState<ArchiveTreeDocumentDto[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  // Switching axis invalidates the node selector (a `dept:` id means nothing on the external tree).
  const changeMode = (m: ArchiveTreeMode) => {
    setMode(m)
    setSelection({ nodeId: null, path: '' })
    setPage(1)
  }

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await archiveTree.documents(selection.nodeId, search, page, PAGE_SIZE)
      setRows(res.items)
      setTotal(res.totalCount)
      setError('')
    } catch {
      setError(t('archiveTree.loadDocsError'))
    } finally {
      setLoading(false)
    }
  }, [selection.nodeId, search, page, t])

  useEffect(() => { load() }, [load])

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  return (
    // Below `lg` the tree stacks above the table. Side by side on a phone left the table in a
    // ~46px column — present, but unreadable.
    <div className="flex min-h-[70vh] flex-col items-stretch gap-4 lg:flex-row">
      <div className="w-full shrink-0 lg:w-[300px]">
        <ArchiveTreePanel
          mode={mode}
          onModeChange={changeMode}
          selection={selection}
          onSelect={(s) => { setSelection(s); setPage(1) }}
        />
      </div>

      <section className="flex min-w-0 flex-1 flex-col rounded-xl border border-slate-200 bg-white dark:border-slate-700/60 dark:bg-slate-900">
        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-200 px-4 py-3 dark:border-slate-700/60">
          <div className="min-w-0">
            <h2 className="truncate text-sm font-bold text-slate-900 dark:text-slate-100">
              {selection.path || t('archiveTree.allDocuments')}
            </h2>
            <p className="text-xs text-slate-500">{t('archiveTree.resultCount', { count: total })}</p>
          </div>
          <div className="relative">
            <Search className="pointer-events-none absolute inset-inline-start-0 top-1/2 ms-3 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
            <input
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1) }}
              placeholder={t('archiveTree.searchDocs')}
              aria-label={t('archiveTree.searchDocs')}
              className="h-9 w-56 rounded-lg border border-slate-200 bg-white ps-8 pe-2 text-sm outline-none focus:border-teal-500 dark:border-slate-700 dark:bg-slate-950 dark:text-slate-200"
            />
          </div>
        </header>

        {error && <p className="px-4 py-3 text-sm text-red-600">{error}</p>}

        <div className="min-h-0 flex-1 overflow-auto">
          <table className="reg-table">
            <thead><tr>
              <th>{t('archiveTree.cols.number')}</th>
              <th>{t('archiveTree.cols.date')}</th>
              <th>{t('archiveTree.cols.title')}</th>
              <th>{t('archiveTree.cols.type')}</th>
              <th>{t('archiveTree.cols.department')}</th>
              <th>{t('archiveTree.cols.origin')}</th>
              <th>{t('archiveTree.cols.direction')}</th>
            </tr></thead>
            <tbody>
              {loading && <tr><td colSpan={7} className="reg-empty">{t('common.loading')}</td></tr>}
              {!loading && rows.length === 0 && (
                <tr><td colSpan={7} className="reg-empty">{t('archiveTree.noDocs')}</td></tr>
              )}
              {!loading && rows.map((d) => (
                <tr
                  key={d.id}
                  className="reg-row"
                  onClick={() => navigate(`/app/documents/${d.id}`)}
                  title={t('archiveTree.openHint')}
                >
                  <td className="mono">{d.documentNumber}</td>
                  <td className="mono">{(d.documentDate ?? d.createdAt).slice(0, 10)}</td>
                  <td>{d.title}</td>
                  <td>{d.documentTypeName || '—'}</td>
                  <td>{d.departmentName || '—'}</td>
                  {/* Origin reads from the register the document was filed from. An internal
                      message has no external party, so its own label goes here instead of a dash
                      that would look like "came from nowhere". */}
                  <td>
                    {d.origin === 'Internal'
                      ? <span className="inline-flex items-center gap-1 text-violet-600"><Mail size={13} aria-hidden /> بريد داخلي</span>
                      : (d.externalOrgName || (d.origin === 'Direct' ? 'مباشر' : '—'))}
                  </td>
                  <td>
                    {d.direction === 'Incoming' && (
                      <span className="inline-flex items-center gap-1 text-blue-600">
                        <Inbox className="h-3.5 w-3.5" />{t('corr.dir.Incoming')}
                      </span>
                    )}
                    {d.direction === 'Outgoing' && (
                      <span className="inline-flex items-center gap-1 text-emerald-600">
                        <Send className="h-3.5 w-3.5" />{t('corr.dir.Outgoing')}
                      </span>
                    )}
                    {!d.direction && '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {totalPages > 1 && (
          <footer className="flex items-center justify-between gap-3 border-t border-slate-200 px-4 py-2.5 text-sm dark:border-slate-700/60">
            <span className="text-xs text-slate-500">{page} / {totalPages}</span>
            <div className="flex gap-2">
              <button type="button" className="btn btn-ghost btn-sm" disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}>
                {t('common.prev', { defaultValue: 'السابق' })}
              </button>
              <button type="button" className="btn btn-ghost btn-sm" disabled={page >= totalPages}
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}>
                {t('common.next', { defaultValue: 'التالي' })}
              </button>
            </div>
          </footer>
        )}
      </section>
    </div>
  )
}
