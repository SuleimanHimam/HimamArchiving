import { useEffect, useState, useMemo } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { auth } from '../../lib/auth'
import { useDateFormat } from '../../lib/useCalendar'
import { diwan, STATUS_NAMES, type DiwanListItem } from '../../lib/diwan'
import { CLASS_CLS } from '../../lib/internalMail'
import DatePicker from '../../components/DatePicker'

// Classification enum names — index is the enum value (Public=0 … HighlyConfidential=3).
const CLASS_NAMES = ['Public', 'Internal', 'Confidential', 'HighlyConfidential'] as const
import { useToast } from '../../components/toast'
import {
  ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuLabel,
  ContextMenuSeparator, ContextMenuShortcut, ContextMenuTrigger,
} from '../../components/ui/context-menu'
import { canPrintMail, warmMailPreview, failMailPreview, printDiwanMail, previewDiwanMail } from '../../lib/mailPrint'
import { closePreviewWindow } from '../../lib/reportTemplates'
import {
  Eye, FolderOpen, Copy, Printer, Archive as ArchiveIcon, Trash2, Share2, Tag, RotateCcw, Paperclip,
  Search, SlidersHorizontal, SquarePen, Inbox, Send, Mails, FileEdit, X, Users,
} from 'lucide-react'
import { useMailAttach } from '../../lib/useMailAttach'
import ConvertToInternalDialog from '../../components/ConvertToInternalDialog'
import ChangeMailStatusDialog from '../../components/ChangeMailStatusDialog'
import MailAccessDialog from '../../components/MailAccessDialog'
import { Tabs, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { useExcelTable, type ColumnDef } from '../../components/ui/use-excel-table'
import { ColumnHeaderMenu } from '../../components/ui/column-filter'
import '../../styles/mail.css'

// One icon per meaning, shared with the internal register: the same action must never
// render as two different glyphs across the two mailboxes.
const FOLDERS = [
  { key: 'all', Icon: Mails, labelKey: 'corr.folders.allDiwan' },
  { key: 'incoming', Icon: Inbox, labelKey: 'corr.folders.externalIn' },
  { key: 'outgoing', Icon: Send, labelKey: 'corr.folders.externalOut' },
  { key: 'drafts', Icon: FileEdit, labelKey: 'corr.folders.drafts' },
  { key: 'deleted', Icon: Trash2, labelKey: 'corr.folders.deleted' },
]

const DIR_CLS: Record<string, string> = { Incoming: 'corr-dir--in', Outgoing: 'corr-dir--out' }

const isOverdue = (it: DiwanListItem) =>
  !!it.responseDeadline && new Date(it.responseDeadline) < new Date() &&
  it.status !== 'Replied' && it.status !== 'Closed' && it.status !== 'Archived'

/** الديوان — the external register: letters exchanged with bodies outside the institution. */
export default function DiwanListPage() {
  const { t } = useTranslation()
  const { dateTime } = useDateFormat()
  const navigate = useNavigate()
  const toast = useToast()
  const [params] = useSearchParams()
  const canCreate = auth.hasPermission('Correspondence.Create')

  const folderParam = params.get('folder')
  const [folder, setFolder] = useState(folderParam && FOLDERS.some((f) => f.key === folderParam) ? folderParam : 'all')
  const [pageSize, setPageSize] = useState(20)
  // The row being converted into an internal message (null = dialog closed).
  const [convert, setConvert] = useState<DiwanListItem | null>(null)
  // The row whose حالة البريد is being changed.
  const [statusRow, setStatusRow] = useState<DiwanListItem | null>(null)
  // The row whose access list is open (من يطّلع على البريد).
  const [accessRow, setAccessRow] = useState<DiwanListItem | null>(null)
  const [query, setQuery] = useState('')
  // Server-side filters (the API supports status / classification / date range).
  const [fStatus, setFStatus] = useState<number | ''>('')
  const [fClass, setFClass] = useState<number | ''>('')
  const [fFrom, setFFrom] = useState('')
  const [fTo, setFTo] = useState('')
  const [showFilters, setShowFilters] = useState(false)
  const activeFilters = (fStatus !== '' ? 1 : 0) + (fClass !== '' ? 1 : 0) + (fFrom ? 1 : 0) + (fTo ? 1 : 0)
  const clearFilters = () => { setFStatus(''); setFClass(''); setFFrom(''); setFTo('') }

  useEffect(() => {
    if (folderParam && FOLDERS.some((f) => f.key === folderParam)) setFolder(folderParam)
  }, [folderParam])

  const { data, isLoading, refetch } = useQuery({
    queryKey: ['diwan', folder, pageSize, fStatus, fClass, fFrom, fTo],
    queryFn: () => diwan.list({
      folder, pageSize,
      status: fStatus === '' ? undefined : fStatus,
      classification: fClass === '' ? undefined : fClass,
      from: fFrom || undefined,
      // Include the whole "to" day — a bare date parses to 00:00 and would drop that day's records.
      to: fTo ? `${fTo}T23:59:59` : undefined,
    }),
  })

  /* The other end of the letter, whichever end that is: a وارد comes *from* an outside body, a
     صادر goes *to* one — and to a named person there, which is who the sender's copy is filed
     under. */
  const to = t('corr.col.to', { defaultValue: 'إلى' })
  const from = t('corr.col.from')
  const party = (it: DiwanListItem) =>
    (it.direction === 'Outgoing' ? it.addresseeName || it.externalOrgName : it.externalOrgName) || '—'

  const rows = (data?.items ?? []).filter((it) =>
    !query || it.subject.includes(query) || it.registerNumber.includes(query) || party(it).includes(query))

  // Labelled from the rows on screen, not from the open tab: الكل، المسودّات and المحذوفات all
  // hold both directions, and a header the rows contradict is worse than no header.
  const dirs = new Set(rows.map((it) => it.direction))
  const mixed = dirs.size > 1
  const partyLabel = mixed ? t('diwan.externalParty') : dirs.has('Outgoing') ? to : from
  const partyPrefix = (it: DiwanListItem) => (mixed ? (it.direction === 'Outgoing' ? to : from) : null)

  // Excel-style per-column sort + value checklist over the loaded page.
  const columns = useMemo<ColumnDef<DiwanListItem>[]>(() => [
    { key: 'status',         accessor: (it) => t(`corr.status.${it.status}`, { defaultValue: it.status }), sortValue: (it) => it.status },
    { key: 'number',         accessor: (it) => it.registerNumber },
    { key: 'date',           accessor: (it) => dateTime(it.mailDate), sortValue: (it) => it.mailDate ?? '' },
    { key: 'party',          accessor: (it) => party(it) },
    { key: 'direction',      accessor: (it) => t(`corr.dir.${it.direction}`, { defaultValue: it.direction }), sortValue: (it) => it.direction },
    { key: 'action',         accessor: (it) => (it.actionType ? t(`corr.action.${it.actionType}`, { defaultValue: it.actionType }) : '—') },
    { key: 'subject',        accessor: (it) => it.subject ?? '' },
    { key: 'deadline',       accessor: (it) => (it.responseDeadline ? dateTime(it.responseDeadline) : '—'), sortValue: (it) => it.responseDeadline ?? '' },
    { key: 'classification', accessor: (it) => t(`corr.class.${it.classification}`, { defaultValue: it.classification }) },
  ], [t, dateTime])
  const { viewRows, getColumnProps } = useExcelTable(rows, columns)

  async function act(fn: () => Promise<unknown>) {
    try { await fn(); await refetch(); toast.success(t('common.saved')) }
    catch { toast.error(t('corr.errors.saveFailed')) }
  }

  // Attach straight from a row's context menu, without opening the entry first.
  const attach = useMailAttach({
    upload: (mailId, file) => diwan.addAttachment(mailId, file),
    onDone: refetch,
    onError: (m) => toast.error(m),
    onSuccess: (m) => toast.success(m),
  })

  async function copyText(text: string) {
    try { await navigator.clipboard.writeText(text); toast.success(t('corr.ctx.copied')) }
    catch { toast.error(t('corr.ctx.copyFailed')) }
  }

  // Fetches the full letter, then prints it on the letterhead that letter resolves to.
  async function printRow(id: number) {
    try { await printDiwanMail(await diwan.get(id), t, dateTime) }
    catch { toast.error(t('corr.errors.printFailed')) }
  }
  async function previewRow(id: number) {
    // Claimed before the fetch: window.open only works while the click is still fresh.
    warmMailPreview()
    try { await previewDiwanMail(await diwan.get(id), t, dateTime) }
    catch {
      failMailPreview(t('corr.errors.previewFailed'))
      toast.error(t('corr.errors.previewFailed'))
    }
  }

  useEffect(() => closePreviewWindow, [])

  return (
    <div className="mbx mbx--stacked">
      {/* Folder switcher — segmented pill tabs */}
      <Tabs value={folder} onValueChange={setFolder} dir="rtl" className="mbx__tabs">
        <TabsList aria-label={t('corr.folders.label')}>
          {FOLDERS.map((f) => (
            <TabsTrigger key={f.key} value={f.key}>
              <f.Icon size={15} aria-hidden />
              <span>{t(f.labelKey)}</span>
              <FolderCount k={f.key} />
            </TabsTrigger>
          ))}
        </TabsList>
      </Tabs>

      <section className="mbx__main">
        <div className="mbx__toolbar">
          <div className="mbx__search">
            <Search size={15} aria-hidden />
            <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t('corr.searchPlaceholder')} aria-label={t('corr.searchPlaceholder')} />
          </div>
          <button
            type="button"
            className={`mbx__filterbtn${showFilters ? ' is-open' : ''}${activeFilters ? ' has-active' : ''}`}
            onClick={() => setShowFilters((v) => !v)}
            aria-expanded={showFilters}
          >
            <SlidersHorizontal size={15} aria-hidden /> {t('corr.filter.button', { defaultValue: 'تصفية' })}
            {activeFilters > 0 && <span className="mbx__filterbadge">{activeFilters}</span>}
          </button>
          <label className="mbx__rows-sel">
            {t('corr.rows')}
            <select value={pageSize} onChange={(e) => setPageSize(Number(e.target.value))} aria-label={t('corr.rows')}>
              {[20, 50, 100].map((n) => <option key={n} value={n}>{n}</option>)}
            </select>
          </label>
          <span className="mbx__count mono">{viewRows.length ? `1–${viewRows.length} / ${data?.totalCount ?? viewRows.length}` : '0'}</span>
          {canCreate && (
            <button className="mbx__new" style={{ marginInlineStart: 'auto' }} onClick={() => navigate('/app/diwan/compose?dir=1')}>
              <SquarePen size={16} aria-hidden /> {t('corr.newMail')}
            </button>
          )}
        </div>

        {showFilters && (
          <div className="mbx__filters">
            <label className="mbx__filter">
              <span>{t('corr.col.status')}</span>
              <select value={fStatus} onChange={(e) => setFStatus(e.target.value === '' ? '' : Number(e.target.value))}>
                <option value="">{t('corr.filter.all', { defaultValue: 'الكل' })}</option>
                {STATUS_NAMES.map((k, v) => <option key={k} value={v}>{t(`corr.status.${k}`, { defaultValue: k })}</option>)}
              </select>
            </label>
            <label className="mbx__filter">
              <span>{t('corr.col.classification')}</span>
              <select value={fClass} onChange={(e) => setFClass(e.target.value === '' ? '' : Number(e.target.value))}>
                <option value="">{t('corr.filter.all', { defaultValue: 'الكل' })}</option>
                {CLASS_NAMES.map((k, v) => <option key={k} value={v}>{t(`corr.class.${k}`, { defaultValue: k })}</option>)}
              </select>
            </label>
            <label className="mbx__filter">
              <span>{t('corr.filter.from', { defaultValue: 'من تاريخ' })}</span>
              <DatePicker value={fFrom} onChange={setFFrom} ariaLabel={t('corr.filter.from', { defaultValue: 'من تاريخ' })} />
            </label>
            <label className="mbx__filter">
              <span>{t('corr.filter.to', { defaultValue: 'إلى تاريخ' })}</span>
              <DatePicker value={fTo} onChange={setFTo} ariaLabel={t('corr.filter.to', { defaultValue: 'إلى تاريخ' })} />
            </label>
            {activeFilters > 0 && (
              <button type="button" className="mbx__filterclear" onClick={clearFilters}>
                <X size={14} aria-hidden /> {t('corr.filter.clear', { defaultValue: 'مسح' })}
              </button>
            )}
          </div>
        )}

        <div className="mbx__tablewrap">
          {isLoading ? <p className="corr__empty">{t('common.loading')}</p>
            : rows.length === 0 ? <p className="corr__empty">{t('corr.empty')}</p> : (
              <table className="mbx__table">
                <thead>
                  <tr>
                    <th><ColumnHeaderMenu label={t('corr.col.status')} {...getColumnProps('status')} /></th>
                    <th><ColumnHeaderMenu label={t('corr.col.number')} {...getColumnProps('number')} /></th>
                    <th><ColumnHeaderMenu label={t('corr.col.date')} {...getColumnProps('date')} /></th>
                    <th><ColumnHeaderMenu label={partyLabel} {...getColumnProps('party')} /></th>
                    <th><ColumnHeaderMenu label={t('corr.col.direction')} {...getColumnProps('direction')} /></th>
                    <th className="col-opt3"><ColumnHeaderMenu label={t('corr.col.action')} {...getColumnProps('action')} /></th>
                    <th className="mbx__c-subject"><ColumnHeaderMenu label={t('corr.col.subject')} {...getColumnProps('subject')} /></th>
                    <th className="col-opt2"><ColumnHeaderMenu label={t('corr.col.deadline')} {...getColumnProps('deadline')} /></th>
                    <th className="col-opt1"><ColumnHeaderMenu label={t('corr.col.classification')} {...getColumnProps('classification')} /></th>
                    <th className="mbx__c-actions">{t('corr.col.actions')}</th>
                  </tr>
                </thead>
                <tbody>
                  {viewRows.map((it) => (
                    <ContextMenu key={it.id}>
                    <ContextMenuTrigger asChild>
                    <tr className={`mbx__row ${isOverdue(it) ? 'is-overdue' : ''}`} onClick={() => navigate(`/app/diwan/${it.id}`)}>
                      {/* data-cell drives the phone card layout in mail.css — which cells
                          survive below 640px and where they land. */}
                      <td data-cell="status"><span className={`badge ${it.status === 'Draft' ? 'internal' : it.status === 'Overdue' ? 'secret' : 'public'}`}>{t(`corr.status.${it.status}`, { defaultValue: it.status })}</span></td>
                      <td data-cell="number" className="mono">{it.registerNumber}</td>
                      <td data-cell="date" className="mono mbx__nowrap">{dateTime(it.mailDate)}</td>
                      <td data-cell="party" className="mbx__ellip" title={`${partyPrefix(it) ? `${partyPrefix(it)}: ` : ''}${party(it)}`}>
                        {partyPrefix(it) && <span className="mbx__party-dir">{partyPrefix(it)}</span>}
                        {party(it)}
                      </td>
                      <td data-cell="dir"><span className={`corr-dir ${DIR_CLS[it.direction] ?? ''}`}>{t(`corr.dir.${it.direction}`, { defaultValue: it.direction })}</span></td>
                      <td data-cell="action" className="col-opt3">{it.actionType ? <span className="corr-type">{t(`corr.action.${it.actionType}`, { defaultValue: it.actionType })}</span> : <span className="muted">—</span>}</td>
                      <td data-cell="subject" className="mbx__c-subject mbx__ellip">
                        {it.subject}
                        {it.hasAttachments && <Paperclip size={13} aria-label={t('corr.hasAttachments')} className="mbx__clip" />}
                      </td>
                      <td data-cell="deadline" className="mono mbx__nowrap col-opt2">{it.responseDeadline ? <span className={isOverdue(it) ? 'corr__od' : ''}>{dateTime(it.responseDeadline)}</span> : '—'}</td>
                      <td data-cell="class" className="col-opt1"><span className={`badge ${CLASS_CLS[it.classification] ?? 'internal'}`}>{t(`corr.class.${it.classification}`, { defaultValue: it.classification })}</span></td>
                      <td data-cell="acts" className="mbx__c-actions" onClick={(e) => e.stopPropagation()}>
                        <div className="mbx__acts">
                          <button className="mbx__act" title={t('corr.open')} onClick={() => navigate(`/app/diwan/${it.id}`)}><Eye size={15} aria-hidden /></button>
                          {/* In المحذوفات the only sensible action is putting it back. */}
                          {folder === 'deleted' ? (
                            auth.hasPermission('Correspondence.Delete') &&
                              <button className="mbx__act" title={t('corr.restore')} onClick={() => act(() => diwan.restore(it.id))}><RotateCcw size={15} aria-hidden /></button>
                          ) : (
                            <>
                              {auth.hasPermission('Correspondence.Create') &&
                                <button className="mbx__act" title={t('diwan.toInternal', { defaultValue: 'تحويل إلى مراسلة داخلية' })} onClick={() => setConvert(it)}><Share2 size={15} aria-hidden /></button>}
                              {auth.hasPermission('Correspondence.Edit') &&
                                <button className="mbx__act" title={t('diwan.changeStatus', { defaultValue: 'حالة البريد' })} onClick={() => setStatusRow(it)}><Tag size={15} aria-hidden /></button>}
                              {/* Offered to anyone who can see the letter; the dialog itself is
                                  read-only unless they head a department it reached. */}
                              <button className="mbx__act" title={t('diwan.access', { defaultValue: 'من يطّلع على البريد' })} onClick={() => setAccessRow(it)}><Users size={15} aria-hidden /></button>
                              {auth.hasPermission('Correspondence.Archive') &&
                                <button className="mbx__act" title={t('corr.archive')} onClick={() => act(() => diwan.archive(it.id))}><ArchiveIcon size={15} aria-hidden /></button>}
                              {auth.hasPermission('Correspondence.Delete') &&
                                <button className="mbx__act" title={t('corr.delete')} onClick={() => act(() => diwan.remove(it.id))}><Trash2 size={15} aria-hidden /></button>}
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                    </ContextMenuTrigger>

                    <ContextMenuContent className="w-56">
                      <ContextMenuLabel className="text-xs text-muted-foreground truncate max-w-[220px]">
                        {it.registerNumber} — {it.subject}
                      </ContextMenuLabel>
                      <ContextMenuSeparator />

                      <ContextMenuItem onSelect={() => navigate(`/app/diwan/${it.id}`)}>
                        <Eye className="ml-2 h-4 w-4 opacity-60" />
                        {t('corr.open')}
                        <ContextMenuShortcut>↵</ContextMenuShortcut>
                      </ContextMenuItem>
                      <ContextMenuItem onSelect={() => window.open(`/app/diwan/${it.id}`, '_blank')}>
                        <FolderOpen className="ml-2 h-4 w-4 opacity-60" />
                        {t('corr.ctx.openNewTab')}
                      </ContextMenuItem>

                      {(auth.hasPermission('Correspondence.Create') || auth.hasPermission('Correspondence.Edit')) && <ContextMenuSeparator />}
                      {auth.hasPermission('Correspondence.Create') && (
                        <ContextMenuItem onSelect={() => setConvert(it)}>
                          <Share2 className="ml-2 h-4 w-4 opacity-60" />
                          {t('diwan.toInternal', { defaultValue: 'تحويل إلى مراسلة داخلية' })}
                        </ContextMenuItem>
                      )}
                      {auth.hasPermission('Correspondence.Edit') && (
                        <ContextMenuItem onSelect={() => setStatusRow(it)}>
                          <Tag className="ml-2 h-4 w-4 opacity-60" />
                          {t('diwan.changeStatus', { defaultValue: 'حالة البريد' })}
                        </ContextMenuItem>
                      )}
                      <ContextMenuItem onSelect={() => setAccessRow(it)}>
                        <Users className="ml-2 h-4 w-4 opacity-60" />
                        {t('diwan.access', { defaultValue: 'من يطّلع على البريد' })}
                      </ContextMenuItem>
                      {/* An archived entry is closed — its attachments no longer change. */}
                      {auth.hasPermission('Correspondence.Edit') && (
                        <ContextMenuItem disabled={attach.busy || it.isArchived}
                          onSelect={() => attach.pickFor(it.id)}>
                          <Paperclip className="ml-2 h-4 w-4 opacity-60" />
                          إرفاق ملف
                        </ContextMenuItem>
                      )}

                      <ContextMenuSeparator />
                      <ContextMenuItem onSelect={() => copyText(it.registerNumber)}>
                        <Copy className="ml-2 h-4 w-4 opacity-60" />
                        {t('corr.ctx.copyNumber')}
                        <ContextMenuShortcut>⌘C</ContextMenuShortcut>
                      </ContextMenuItem>
                      {/* One entry, not two: for anyone who may print, «طباعة» opens the platform's
                          print dialog, which shows the letter beside the printer and the paper —
                          a معاينة next to it would open the same document on a second screen. */}
                      {!canPrintMail() && (
                        <ContextMenuItem onSelect={() => previewRow(it.id)}>
                          <Eye className="ml-2 h-4 w-4 opacity-60" />
                          {t('corr.preview')}
                        </ContextMenuItem>
                      )}
                      {canPrintMail() && (
                        <ContextMenuItem onSelect={() => printRow(it.id)}>
                          <Printer className="ml-2 h-4 w-4 opacity-60" />
                          {t('corr.print')}
                          <ContextMenuShortcut>⌘P</ContextMenuShortcut>
                        </ContextMenuItem>
                      )}

                      {(auth.hasPermission('Correspondence.Archive') || auth.hasPermission('Correspondence.Delete')) && <ContextMenuSeparator />}
                      {folder === 'deleted' ? (
                        auth.hasPermission('Correspondence.Delete') && (
                          <ContextMenuItem onSelect={() => act(() => diwan.restore(it.id))}>
                            <RotateCcw className="ml-2 h-4 w-4 opacity-60" />
                            {t('corr.restore')}
                          </ContextMenuItem>
                        )
                      ) : (
                        <>
                          {auth.hasPermission('Correspondence.Archive') && (
                            <ContextMenuItem disabled={it.isArchived} onSelect={() => act(() => diwan.archive(it.id))}>
                              <ArchiveIcon className="ml-2 h-4 w-4 opacity-60" />
                              {t('corr.archive')}
                            </ContextMenuItem>
                          )}
                          {auth.hasPermission('Correspondence.Delete') && (
                            <ContextMenuItem className="text-destructive focus:text-destructive" onSelect={() => act(() => diwan.remove(it.id))}>
                              <Trash2 className="ml-2 h-4 w-4 opacity-60" />
                              {t('corr.delete')}
                            </ContextMenuItem>
                          )}
                        </>
                      )}
                    </ContextMenuContent>
                    </ContextMenu>
                  ))}
                </tbody>
              </table>
            )}
        </div>
      </section>

      <ConvertToInternalDialog
        item={convert}
        open={convert !== null}
        onOpenChange={(o) => { if (!o) setConvert(null) }}
      />

      <MailAccessDialog
        item={accessRow}
        open={accessRow !== null}
        onOpenChange={(o) => { if (!o) setAccessRow(null) }}
        onSaved={() => { void refetch() }}
      />

      <ChangeMailStatusDialog
        item={statusRow}
        open={statusRow !== null}
        onOpenChange={(o) => { if (!o) setStatusRow(null) }}
        onSaved={() => { void refetch() }}
      />
    </div>
  )
}

/** Live per-folder count badge shown inside its tab. */
function FolderCount({ k }: { k: string }) {
  const { data: count } = useQuery({
    queryKey: ['diwan', 'count', k],
    queryFn: () => diwan.list({ folder: k, pageSize: 1 }).then((r) => r.totalCount),
  })
  return <span className="mbx__tabcount">{count ?? 0}</span>
}
