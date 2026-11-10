import { useEffect, useState, useMemo } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { auth } from '../../lib/auth'
import { useDateFormat } from '../../lib/useCalendar'
import { internalMail, CLASS_CLS, type InternalListItem } from '../../lib/internalMail'
import { STATUS_NAMES } from '../../lib/diwan'
import DatePicker from '../../components/DatePicker'

// Classification enum names — index is the enum value (Public=0 … HighlyConfidential=3).
const CLASS_NAMES = ['Public', 'Internal', 'Confidential', 'HighlyConfidential'] as const
import { useToast } from '../../components/toast'
import {
  ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuLabel,
  ContextMenuSeparator, ContextMenuShortcut, ContextMenuTrigger,
} from '../../components/ui/context-menu'
import { canPrintMail, printInternalMail, askPrintMode, mailHasAttachments } from '../../lib/mailPrint'
import { closePreviewWindow } from '../../lib/reportTemplates'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import {
  Eye, FolderOpen, Copy, Printer, Reply, ReplyAll, Forward, Archive as ArchiveIcon, Trash2, RotateCcw,
  Paperclip, Search, SlidersHorizontal, SquarePen, Inbox, Send, Mails, FileEdit, X, Check,
} from 'lucide-react'
import { useMailAttach } from '../../lib/useMailAttach'
import { Tabs, TabsList, TabsTrigger } from '../../components/ui/tabs'
import { useExcelTable, type ColumnDef } from '../../components/ui/use-excel-table'
import { ColumnHeaderMenu } from '../../components/ui/column-filter'
import '../../styles/mail.css'
import '../../styles/workflow.css'
import type { WorkflowMailBadge } from '../../lib/workflow'

// Same icon per meaning as the Diwan register — the two mailboxes read as one system.
const FOLDERS = [
  { key: 'all', Icon: Mails, labelKey: 'corr.folders.allMurasalat' },
  { key: 'inbox', Icon: Inbox, labelKey: 'corr.folders.internalIn' },
  { key: 'sent', Icon: Send, labelKey: 'corr.folders.internalOut' },
  { key: 'drafts', Icon: FileEdit, labelKey: 'corr.folders.drafts' },
  { key: 'deleted', Icon: Trash2, labelKey: 'corr.folders.deleted' },
]

/** Workflow columns for one row: the route it is on, the step, and — the only part worth colour —
 *  whether this reader is the one it is waiting on. */
function WorkflowChip({ wf }: { wf: WorkflowMailBadge }) {
  const tone = wf.actionRequired ? 'await'
    : wf.status === 'Completed' ? 'ok'
      : wf.status === 'Rejected' ? 'bad' : ''
  const text = wf.actionRequired ? 'بانتظار إجرائك'
    : wf.status === 'Completed' ? 'مكتمل'
      : wf.status === 'Rejected' ? 'مرفوض'
        : wf.status === 'ChangesRequested' ? 'بانتظار تعديل'
          : wf.currentStageName ?? wf.definitionName
  return (
    <span className={`wf-chip ${tone ? `wf-chip--${tone}` : ''}`}
      title={`${wf.definitionName} — ${wf.currentStageName ?? ''}`}>
      <span className="wf-step">{wf.stepNumber}/{wf.totalSteps}</span>
      {text}
    </span>
  )
}

const isOverdue = (it: InternalListItem) =>
  !!it.responseDeadline && new Date(it.responseDeadline) < new Date() &&
  it.status !== 'Replied' && it.status !== 'Closed' && it.status !== 'Archived'

/** المراسلات — internal correspondence between staff and departments. */
export default function InternalListPage() {
  const { t } = useTranslation()
  const { dateTime } = useDateFormat()
  const navigate = useNavigate()
  const toast = useToast()
  const { choose } = useConfirmDialog()
  const [params] = useSearchParams()
  const canCreate = auth.hasPermission('Correspondence.Create')

  const folderParam = params.get('folder')
  const [folder, setFolder] = useState(folderParam && FOLDERS.some((f) => f.key === folderParam) ? folderParam : 'all')
  const [pageSize, setPageSize] = useState(20)
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
    queryKey: ['internal-mail', folder, pageSize, fStatus, fClass, fFrom, fTo],
    queryFn: () => internalMail.list({
      folder, pageSize,
      status: fStatus === '' ? undefined : fStatus,
      classification: fClass === '' ? undefined : fClass,
      from: fFrom || undefined,
      to: fTo ? `${fTo}T23:59:59` : undefined,
    }),
  })

  /* One column, two questions. An inbox is read by who wrote to you; a sent folder by who you
     wrote to — "from" there is the person doing the reading, on every row, which tells them
     nothing.

     Direction is a property of the pair (message, reader), not of the folder, so the cell always
     follows the row. */
  const to = t('corr.col.to', { defaultValue: 'إلى' })
  const from = t('corr.col.from')
  const party = (it: InternalListItem) =>
    (it.isOutgoing ? it.recipientNames?.join('، ') : it.senderName) || '—'

  const rows = (data?.items ?? []).filter((it) =>
    !query || it.subject.includes(query) || it.mailNumber.includes(query) || party(it).includes(query))

  /* The header is read from what is actually on screen, not from which tab is open. No folder is
     purely one direction — الوارد holds a message you sent to a group you are in, المحذوفات holds
     both by definition — and a header the rows below it contradict is worse than no header. When
     they do disagree, every row says which way it went itself. */
  const dirs = new Set(rows.map((it) => it.isOutgoing))
  const mixed = dirs.size > 1
  const partyLabel = mixed
    ? t('corr.col.fromTo', { defaultValue: 'من / إلى' })
    : dirs.has(true) ? to : from
  const partyPrefix = (it: InternalListItem) => (mixed ? (it.isOutgoing ? to : from) : null)

  // Excel-style per-column sort + value checklist over the loaded page.
  const columns = useMemo<ColumnDef<InternalListItem>[]>(() => [
    { key: 'status',         accessor: (it) => t(`corr.status.${it.status}`, { defaultValue: it.status }), sortValue: (it) => it.status },
    { key: 'number',         accessor: (it) => it.mailNumber },
    { key: 'read',           accessor: (it) => (it.isRead ? t('corr.read.Read') : t('corr.read.Undelivered')), sortValue: (it) => (it.isRead ? 1 : 0) },
    { key: 'date',           accessor: (it) => dateTime(it.mailDate), sortValue: (it) => it.mailDate ?? '' },
    { key: 'from',           accessor: (it) => party(it) },
    { key: 'direction',      accessor: (it) => t(`corr.dir.${it.isOutgoing ? 'Outgoing' : 'Incoming'}`), sortValue: (it) => (it.isOutgoing ? 1 : 0) },
    { key: 'action',         accessor: (it) => (it.actionType ? t(`corr.action.${it.actionType}`, { defaultValue: it.actionType }) : '—') },
    { key: 'subject',        accessor: (it) => it.subject ?? '' },
    { key: 'deadline',       accessor: (it) => (it.responseDeadline ? dateTime(it.responseDeadline) : '—'), sortValue: (it) => it.responseDeadline ?? '' },
    { key: 'classification', accessor: (it) => t(`corr.class.${it.classification}`, { defaultValue: it.classification }) },
    // Not `party`: the callback is redefined on every render, and depending on it would rebuild
    // the column set — and reset the checklist filters — on each keystroke.
  ], [t, dateTime])
  const { viewRows, getColumnProps } = useExcelTable(rows, columns)

  // Attach straight from a row's context menu, without opening the message first.
  const attach = useMailAttach({
    upload: (mailId, file) => internalMail.addAttachment(mailId, file),
    onDone: refetch,
    onError: (m) => toast.error(m),
    onSuccess: (m) => toast.success(m),
  })

  async function act(fn: () => Promise<unknown>) {
    try { await fn(); await refetch(); toast.success(t('common.saved')) }
    catch { toast.error(t('corr.errors.saveFailed')) }
  }

  async function copyText(text: string) {
    try { await navigator.clipboard.writeText(text); toast.success(t('corr.ctx.copied')) }
    catch { toast.error(t('corr.ctx.copyFailed')) }
  }

  // Fetches the full message, then prints it through the Settings letterhead.
  async function printRow(id: number) {
    try {
      // Fetch first: the row has no attachment list, and the choice depends on it.
      const mail = await internalMail.get(id)
      const mode = await askPrintMode(choose, mailHasAttachments(mail))
      if (!mode) return
      await printInternalMail(mail, t, dateTime, mode)
    } catch { toast.error(t('corr.errors.printFailed')) }
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
            <button className="mbx__new" style={{ marginInlineStart: 'auto' }} onClick={() => navigate('/app/internal/compose')}>
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
                    <th className="mbx__c-read"><ColumnHeaderMenu label={t('corr.col.read')} {...getColumnProps('read')} /></th>
                    <th><ColumnHeaderMenu label={t('corr.col.date')} {...getColumnProps('date')} /></th>
                    <th><ColumnHeaderMenu label={partyLabel} {...getColumnProps('from')} /></th>
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
                    <tr className={`mbx__row ${it.isRead ? '' : 'is-unread'} ${isOverdue(it) ? 'is-overdue' : ''}`} onClick={() => navigate(`/app/internal/${it.id}`)}>
                      {/* data-cell drives the phone card layout in mail.css — which cells
                          survive below 640px and where they land. */}
                      <td data-cell="status"><span className={`badge ${it.status === 'Draft' ? 'internal' : it.status === 'Overdue' ? 'secret' : 'public'}`}>{t(`corr.status.${it.status}`, { defaultValue: it.status })}</span></td>
                      <td data-cell="number" className="mono">{it.mailNumber}</td>
                      <td data-cell="read" className="mbx__c-read">{it.isRead
                        ? <Check size={14} className="mbx__read" aria-label={t('corr.read.Read')} />
                        : <span className="mbx__unread" title={t('corr.read.Undelivered')} />}</td>
                      <td data-cell="date" className="mono mbx__nowrap">{dateTime(it.mailDate)}</td>
                      <td data-cell="party" className="mbx__ellip" title={`${partyPrefix(it) ? `${partyPrefix(it)}: ` : ''}${party(it)}`}>
                        {partyPrefix(it) && <span className="mbx__party-dir">{partyPrefix(it)}</span>}
                        {party(it)}
                      </td>
                      {/* Direction as this reader sees it: صادر when they sent it. The phone card
                          drops this cell, which is why the party cell carries the label too. */}
                      <td data-cell="dir"><span className={`corr-dir ${it.isOutgoing ? 'corr-dir--out' : 'corr-dir--in'}`}>{t(`corr.dir.${it.isOutgoing ? 'Outgoing' : 'Incoming'}`)}</span></td>
                      <td data-cell="action" className="col-opt3">{it.actionType ? <span className="corr-type">{t(`corr.action.${it.actionType}`, { defaultValue: it.actionType })}</span> : <span className="muted">—</span>}</td>
                      <td data-cell="subject" className="mbx__c-subject mbx__ellip">
                        {!it.isRead && <span className="mbx__dot" aria-hidden />}
                        {it.subject}
                        {it.hasAttachments && <Paperclip size={13} aria-label={t('corr.hasAttachments')} className="mbx__clip" />}
                        {/* Workflow rides in the subject cell rather than a column of its own: an
                            institution that uses no workflows sees the table exactly as before. */}
                        {it.workflow && <WorkflowChip wf={it.workflow} />}
                      </td>
                      <td data-cell="deadline" className="mono mbx__nowrap col-opt2">{it.responseDeadline ? <span className={isOverdue(it) ? 'corr__od' : ''}>{dateTime(it.responseDeadline)}</span> : '—'}</td>
                      <td data-cell="class" className="col-opt1"><span className={`badge ${CLASS_CLS[it.classification] ?? 'internal'}`}>{t(`corr.class.${it.classification}`, { defaultValue: it.classification })}</span></td>
                      <td data-cell="acts" className="mbx__c-actions" onClick={(e) => e.stopPropagation()}>
                        <div className="mbx__acts">
                          <button className="mbx__act" title={t('corr.open')} onClick={() => navigate(`/app/internal/${it.id}`)}><Eye size={15} aria-hidden /></button>
                          <button className="mbx__act" title={t('corr.reply')} onClick={() => navigate(`/app/internal/compose?replyTo=${it.id}`)}><Reply size={15} aria-hidden /></button>
                          <button className="mbx__act" title={t('corr.forward')} onClick={() => navigate(`/app/internal/compose?forward=${it.id}`)}><Forward size={15} aria-hidden /></button>
                          {/* In المحذوفات the only sensible action is putting it back. */}
                          {folder === 'deleted'
                            ? auth.hasPermission('Correspondence.Delete') &&
                              <button className="mbx__act" title={t('corr.restore')} onClick={() => act(() => internalMail.restore(it.id))}><RotateCcw size={15} aria-hidden /></button>
                            : <>
                                {auth.hasPermission('Correspondence.Archive') &&
                                  <button className="mbx__act" title={t('corr.archive')} onClick={() => act(() => internalMail.archive(it.id))}><ArchiveIcon size={15} aria-hidden /></button>}
                                {auth.hasPermission('Correspondence.Delete') &&
                                  <button className="mbx__act" title={t('corr.delete')} onClick={() => act(() => internalMail.remove(it.id))}><Trash2 size={15} aria-hidden /></button>}
                              </>}
                        </div>
                      </td>
                    </tr>
                    </ContextMenuTrigger>

                    <ContextMenuContent className="w-56">
                      <ContextMenuLabel className="text-xs text-muted-foreground truncate max-w-[220px]">
                        {it.mailNumber} — {it.subject}
                      </ContextMenuLabel>
                      <ContextMenuSeparator />

                      <ContextMenuItem onSelect={() => navigate(`/app/internal/${it.id}`)}>
                        <Eye className="ml-2 h-4 w-4 opacity-60" />
                        {t('corr.open')}
                        <ContextMenuShortcut>↵</ContextMenuShortcut>
                      </ContextMenuItem>
                      <ContextMenuItem onSelect={() => window.open(`/app/internal/${it.id}`, '_blank')}>
                        <FolderOpen className="ml-2 h-4 w-4 opacity-60" />
                        {t('corr.ctx.openNewTab')}
                      </ContextMenuItem>

                      <ContextMenuSeparator />
                      <ContextMenuItem onSelect={() => navigate(`/app/internal/compose?replyTo=${it.id}`)}>
                        <Reply className="ml-2 h-4 w-4 opacity-60" />
                        {t('corr.reply')}
                      </ContextMenuItem>
                      <ContextMenuItem onSelect={() => navigate(`/app/internal/compose?replyTo=${it.id}&all=1`)}>
                        <ReplyAll className="ml-2 h-4 w-4 opacity-60" />
                        {t('corr.replyAll')}
                      </ContextMenuItem>
                      <ContextMenuItem onSelect={() => navigate(`/app/internal/compose?forward=${it.id}`)}>
                        <Forward className="ml-2 h-4 w-4 opacity-60" />
                        {t('corr.forward')}
                      </ContextMenuItem>
                      {/* An archived message is closed — its attachments no longer change. */}
                      <ContextMenuItem disabled={attach.busy || it.status === 'Archived'}
                        onSelect={() => attach.pickFor(it.id)}>
                        <Paperclip className="ml-2 h-4 w-4 opacity-60" />
                        إرفاق ملف
                      </ContextMenuItem>

                      <ContextMenuSeparator />
                      <ContextMenuItem onSelect={() => copyText(it.mailNumber)}>
                        <Copy className="ml-2 h-4 w-4 opacity-60" />
                        {t('corr.ctx.copyNumber')}
                        <ContextMenuShortcut>⌘C</ContextMenuShortcut>
                      </ContextMenuItem>
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
                          <ContextMenuItem onSelect={() => act(() => internalMail.restore(it.id))}>
                            <RotateCcw className="ml-2 h-4 w-4 opacity-60" />
                            {t('corr.restore')}
                          </ContextMenuItem>
                        )
                      ) : (
                        <>
                          {auth.hasPermission('Correspondence.Archive') && (
                            <ContextMenuItem onSelect={() => act(() => internalMail.archive(it.id))}>
                              <ArchiveIcon className="ml-2 h-4 w-4 opacity-60" />
                              {t('corr.archive')}
                            </ContextMenuItem>
                          )}
                          {auth.hasPermission('Correspondence.Delete') && (
                            <ContextMenuItem className="text-destructive focus:text-destructive" onSelect={() => act(() => internalMail.remove(it.id))}>
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
    </div>
  )
}

/** Live per-folder count badge shown inside its tab. */
function FolderCount({ k }: { k: string }) {
  const { data: count } = useQuery({
    queryKey: ['internal-mail', 'count', k],
    queryFn: () => internalMail.list({ folder: k, pageSize: 1 }).then((r) => r.totalCount),
  })
  return <span className="mbx__tabcount">{count ?? 0}</span>
}
