import { useEffect, useState, useCallback, useMemo, Fragment } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Card, CardContent, CardHeader } from '../../components/ui/card'
import { useTranslation } from 'react-i18next'
import { auth } from '../../lib/auth'
import { documents, type DocumentListItem, type TrashItem, DOC_STATUS_LABELS } from '../../lib/documents'
import { type PagedResult, CONFIDENTIALITY_LABELS } from '../../lib/shared'
import { Download, Printer, Star, Users, Eye, FolderOpen, Pencil, Copy, Trash2, Info, Upload, RotateCcw } from 'lucide-react'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { useAutoRefresh } from '../../lib/useAutoRefresh'
import { foldersApi, folderSharesApi, sharingApi, exportApi, type Folder } from '../../lib/userFeatures'
import { filesApi, fileSharesApi, type UserFile } from '../../lib/files'
import { cn } from '../../lib/utils'
import { useTableColumns } from '../../hooks/useTableColumns'
import { useDocumentThumbnails } from '../../hooks/useDocumentThumbnails'
import {
  ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuLabel,
  ContextMenuSeparator, ContextMenuShortcut, ContextMenuTrigger,
} from '../../components/ui/context-menu'
import {
  useExpandedRow, ExpandCell, ExpandHeadCell, ExpandedRow, DetailFields, DetailField, DetailActions,
} from '../../components/ui/expandable-row'
import { useExcelTable, type ColumnDef } from '../../components/ui/use-excel-table'
import { FileCard } from '../../components/ui/file-card-collections'
import { FileBrowser, type BrowserFile } from '../../components/ui/file-browser'
import '../../styles/file-browser.css'
import { fileFormatOf, fileBadgeOf } from '../../lib/fileFormats'
import { ColumnHeaderMenu } from '../../components/ui/column-filter'
import RequestDispositionModal from '../../components/RequestDispositionModal'
import DocumentPropertiesDialog from '../../components/DocumentPropertiesDialog'
import FilePropertiesDialog from '../../components/FilePropertiesDialog'
import ShareItemDialog, { type ShareKind } from '../../components/ShareItemDialog'
import { FolderDialog } from '../../components/ui/folder-dialog'
import '../../styles/forms.css'

// The display text for a document cell, mirroring the row render switch below — used
// both for the Excel value checklist and (by default) for sorting that column.
function cellText(key: string, d: DocumentListItem): string {
  switch (key) {
    case 'fileType':       return d.fileExtension ? d.fileExtension.toUpperCase() : 'بلا مرفق'
    case 'title':          return d.title ?? ''
    case 'type':           return d.documentTypeName ?? '—'
    case 'confidentiality':return CONFIDENTIALITY_LABELS[d.confidentiality]?.ar ?? d.confidentiality
    case 'status':         return DOC_STATUS_LABELS[d.status] ?? d.status
    case 'location':       return d.boxCode ? d.boxCode : d.physicalLocationName ? `${d.physicalLocationName}${d.boxNumber ? ` · ${d.boxNumber}` : ''}` : '—'
    case 'date':           return d.expiryDate ?? '—'
    case 'documentNumber': return d.documentNumber ?? ''
    case 'documentDate':   return d.documentDate ?? '—'
    case 'version':        return String(d.version ?? '')
    case 'box':            return d.boxNumber ?? '—'
    case 'file':           return d.fileNumber ?? '—'
    case 'createdAt':      return d.createdAt?.slice(0, 10) ?? '—'
    default:
      if (key.startsWith('cf_')) return d.customValues?.[key.slice(3)] ?? '—'
      return ''
  }
}

export default function DocumentsListPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { columns } = useTableColumns('documents')
  const { toggle, isExpanded } = useExpandedRow<number>()
  const [searchParams, setSearchParams] = useSearchParams()
  const [data, setData] = useState<PagedResult<DocumentListItem> | null>(null)
  const [search, setSearch] = useState(searchParams.get('search') ?? '')
  const [status, setStatus] = useState('')
  const [favOnly, setFavOnly] = useState(false)
  const [sharedOnly, setSharedOnly] = useState(false)
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')
  const [folderId, setFolderId] = useState('')
  // 'browser' is the Finder-style panel; 'table' is the full register with
  // column filters, context menus and expandable rows. Remembered per browser.
  const [mode, setMode] = useState<'browser' | 'table'>(
    () => (localStorage.getItem('documents.view') === 'table' ? 'table' : 'browser'))
  useEffect(() => { localStorage.setItem('documents.view', mode) }, [mode])
  const [folders, setFolders] = useState<Folder[]>([])
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const toast = useToast()
  const { prompt, confirm } = useConfirmDialog()
  const canDestroy = auth.hasPermission('Disposition.Create')
  const canDelete = auth.hasPermission('Documents.Delete')
  const [reqFor, setReqFor] = useState<{ id: number; number: string } | null>(null)
  const [propsFor, setPropsFor] = useState<{ id: number; title: string } | null>(null)
  const [filePropsFor, setFilePropsFor] = useState<{ id: number; name: string } | null>(null)

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

  // Excel-style per-column sort + value checklist over the loaded page. Column defs follow
  // the visible column set (including custom-field columns), so the header menus always match.
  const rows = useMemo(() => data?.items ?? [], [data])
  const columnDefs = useMemo<ColumnDef<DocumentListItem>[]>(
    () => columns.map((col) => ({ key: col.key, accessor: (d) => cellText(col.key, d) })),
    [columns],
  )
  const { viewRows, getColumnProps } = useExcelTable(rows, columnDefs)



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
        // At the browser's root, show only documents filed into no folder —
        // otherwise a filed document appears both here and inside its folder.
        unfiledOnly: mode === 'browser' && !folderId ? true : undefined,
        page, pageSize: 15,
      })
      setData(res)
    } catch {
      if (!silent) setError(t('documents.loadError'))
    } finally { if (!silent) setLoading(false) }
  }, [search, status, dateFrom, dateTo, favOnly, sharedOnly, folderId, mode, page, t])

  // Drag-and-drop target handler: move a document into a folder (null = out of
  // every folder). Reloads so counts and the current folder's contents settle.
  // ---- Trash -----------------------------------------------------------------
  // A place of its own rather than a filter over the register: deleted documents
  // have different actions (restore / destroy) and no folder, so mixing them into
  // the normal listing would mean disabling half the menu on half the rows.
  const [inTrash, setInTrash] = useState(false)
  const [trashItems, setTrashItems] = useState<TrashItem[]>([])
  // Deleted plain files land in the same bin — one trash, not one per kind.
  const [trashPlain, setTrashPlain] = useState<UserFile[]>([])

  const loadTrash = useCallback(async () => {
    try { setTrashPlain(await filesApi.trash()) } catch { /* the count simply stays stale */ }
    if (!canDelete) return
    try { setTrashItems(await documents.trash()) } catch { /* same */ }
  }, [canDelete])

  // Fetched here rather than by calling loadTrash: the count is wanted on first
  // paint, and an in-flight response must not land after the page has moved on.
  useEffect(() => {
    let alive = true
    filesApi.trash().then((f) => { if (alive) setTrashPlain(f) }).catch(() => {})
    if (canDelete) documents.trash().then((t) => { if (alive) setTrashItems(t) }).catch(() => {})
    return () => { alive = false }
  }, [canDelete])

  // ---- Plain files (no document behind them) ---------------------------------
  // Kept in their own state and merged into the browser's rows, rather than
  // pretended to be documents: everything downstream — the menu, the move
  // endpoint, the trash — has to know which of the two it is holding.
  const [plainFiles, setPlainFiles] = useState<UserFile[]>([])

  const loadFiles = useCallback(async () => {
    try { setPlainFiles(await filesApi.list(folderId ? Number(folderId) : null)) } catch { /* the folder still lists its documents */ }
  }, [folderId])

  useEffect(() => {
    let alive = true
    filesApi.list(folderId ? Number(folderId) : null)
      .then((f) => { if (alive) setPlainFiles(f) })
      .catch(() => {})
    return () => { alive = false }
  }, [folderId])

  const uploadPlainFiles = useCallback(async (parentId: number | null, chosen: File[]) => {
    try {
      for (const file of chosen) await filesApi.upload(file, parentId)
      toast.success(chosen.length === 1 ? 'رُفع الملف.' : `رُفعت ${chosen.length} ملفات.`)
      await loadFiles()
    } catch {
      toast.error('تعذّر رفع الملف.')
    }
  }, [loadFiles, toast])

  const renameFile = useCallback(async (file: UserFile) => {
    const name = (await prompt({
      title: 'إعادة تسمية', label: 'اسم الملف', defaultValue: file.name, confirmText: 'حفظ',
    }))?.trim()
    if (!name || name === file.name) return
    try {
      await filesApi.update(file.id, { name, folderId: file.folderId })
      await loadFiles()
    } catch { toast.error('تعذّر تغيير الاسم.') }
  }, [loadFiles, prompt, toast])

  const deleteFile = useCallback(async (file: UserFile) => {
    if (!(await confirm({ message: `حذف «${file.name}»؟ ينتقل إلى سلة المحذوفات.`, destructive: true }))) return
    try {
      await filesApi.remove(file.id)
      toast.success('حُذف الملف.')
      await Promise.all([loadFiles(), loadTrash()])
    } catch { toast.error('تعذّر حذف الملف.') }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [loadFiles, loadTrash])

  /** Open a plain file in a new tab — the browser previews what it can and downloads the rest. */
  const openPlainFile = useCallback(async (file: UserFile) => {
    try {
      const url = await filesApi.objectUrl(file.id, file.contentType)
      window.open(url, '_blank', 'noopener')
      // Revoked late: revoking immediately would pull the blob out from under the new tab.
      setTimeout(() => URL.revokeObjectURL(url), 60_000)
    } catch { toast.error('تعذّر فتح الملف.') }
  }, [toast])

  const plainRows = useMemo<BrowserFile[]>(() => plainFiles.map((f) => ({
    id: f.id,
    name: f.name,
    extension: f.fileExtension || null,
    attachmentCount: 1,
    sizeBytes: f.sizeBytes,
    kind: 'ملف',
    createdAt: f.createdAt,
    updatedAt: f.updatedAt,
    origin: 'file' as const,
    shared: f.shared,
    canEdit: f.canEdit,
    ownerName: f.ownerName,
  })), [plainFiles])

  // The item whose share list is open. Null closes the dialog; the shape also carries which
  // set of endpoints to talk to, because a folder and a file are shared the same way.
  const [shareFor, setShareFor] = useState<
    { id: number; name: string; kind: ShareKind } | null>(null)

  /* Documents are shared through their own endpoints, which predate the folder and file ones and
     name the recipient column differently. Adapted here rather than changing an API three other
     screens already call — the dialog only ever needed the same three verbs. */
  const documentShareApi = useMemo(() => ({
    list: (id: number) => sharingApi.list(id).then((rows) => rows.map((s) => ({
      userId: s.sharedWithUserId, userName: s.userName, canEdit: s.canEdit, createdAt: s.createdAt,
    }))),
    share: (id: number, userId: number, canEdit: boolean) => sharingApi.share(id, userId, canEdit),
    unshare: (id: number, userId: number) => sharingApi.unshare(id, userId),
    shareEveryone: (id: number, canEdit: boolean) => sharingApi.shareEveryone(id, canEdit),
    unshareEveryone: (id: number) => sharingApi.unshareEveryone(id),
  }), [])

  // ---- Trash actions -----------------------------------------------------
  const restoreDocument = useCallback(async (id: number, title: string) => {
    try {
      await documents.restore(id)
      toast.success(`أُعيدت «${title}».`)
      await Promise.all([loadTrash(), load(true)])
      foldersApi.list().then(setFolders).catch(() => {})
    } catch {
      toast.error('تعذّر استرجاع الوثيقة.')
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [load, loadTrash])

  const purgeDocument = useCallback(async (id: number, title: string) => {
    if (!(await confirm({
      message: `حذف «${title}» نهائيًا؟ يُتلَف محتواها تشفيريًا ولا يمكن استرجاعها.`,
      destructive: true,
    }))) return
    try {
      await documents.purge(id)
      toast.success('أُتلفت الوثيقة نهائيًا.')
      await loadTrash()
    } catch {
      toast.error('تعذّر الإتلاف النهائي.')
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [loadTrash])

  /** One trash holds two kinds of thing; these send each to its own endpoint. */
  const restoreTrashed = useCallback(async (row: BrowserFile) => {
    if (row.origin !== 'file') { await restoreDocument(row.id, row.name); return }
    try {
      await filesApi.restore(row.id)
      toast.success(`أُعيد «${row.name}».`)
      await Promise.all([loadTrash(), loadFiles()])
    } catch { toast.error('تعذّر استرجاع الملف.') }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [restoreDocument, loadTrash, loadFiles])

  const purgeTrashed = useCallback(async (row: BrowserFile) => {
    if (row.origin !== 'file') { await purgeDocument(row.id, row.name); return }
    if (!(await confirm({
      message: `حذف «${row.name}» نهائيًا؟ يُتلَف محتواه تشفيريًا ولا يمكن استرجاعه.`,
      destructive: true,
    }))) return
    try {
      await filesApi.purge(row.id)
      toast.success('أُتلف الملف نهائيًا.')
      await loadTrash()
    } catch { toast.error('تعذّر الإتلاف النهائي.') }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [purgeDocument, loadTrash])

  const emptyTrash = useCallback(async () => {
    if (!(await confirm({
      message: `إفراغ سلة المحذوفات (${trashItems.length + trashPlain.length} عنصرًا)؟ يُتلَف محتواها تشفيريًا ولا يمكن استرجاعه.`,
      destructive: true,
    }))) return
    try {
      for (const f of trashPlain) await filesApi.purge(f.id)
      const { destroyed } = canDelete ? await documents.emptyTrash() : { destroyed: 0 }
      toast.success(`أُتلف ${destroyed + trashPlain.length} عنصرًا نهائيًا.`)
      await loadTrash()
    } catch {
      toast.error('تعذّر إفراغ السلة.')
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trashItems.length, trashPlain, canDelete, loadTrash])

  const trashFiles = useMemo<BrowserFile[]>(() => trashItems.map<BrowserFile>((t) => ({
    id: t.id,
    name: t.title,
    extension: t.fileExtension,
    attachmentCount: t.attachmentCount,
    sizeBytes: t.totalSizeBytes,
    kind: t.documentTypeName,
    caption: t.documentNumber,
    // The trash sorts and reads by when it was thrown away, not when it was written.
    updatedAt: t.deletedAt,
  })).concat(trashPlain.map<BrowserFile>((f) => ({
    id: f.id,
    name: f.name,
    extension: f.fileExtension || null,
    attachmentCount: 1,
    sizeBytes: f.sizeBytes,
    kind: 'ملف',
    updatedAt: f.updatedAt,
    origin: 'file' as const,
  }))), [trashItems, trashPlain])

  const moveToFolder = useCallback(async (row: BrowserFile, targetFolderId: number | null) => {
    const isFile = row.origin === 'file'
    try {
      // Two different things that look alike in the grid, and two endpoints:
      // a document changes its folder, a file changes its own row.
      if (isFile) await filesApi.update(row.id, { name: row.name, folderId: targetFolderId })
      else await foldersApi.moveDocument(row.id, targetFolderId)

      toast.success(targetFolderId === null
        ? (isFile ? 'أُخرج الملف من المجلد.' : 'أُخرجت الوثيقة من المجلد.')
        : (isFile ? 'نُقل الملف إلى المجلد.' : 'نُقلت الوثيقة إلى المجلد.'))
      await (isFile ? loadFiles() : load(true))
      foldersApi.list().then(setFolders).catch(() => {})
    } catch {
      toast.error(isFile ? 'تعذّر نقل الملف.' : 'تعذّر نقل الوثيقة.')
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [load, loadFiles])

  // Toolbar «+ مجلد». The timestamp both opens the dialog and remounts it, so a
  // second folder does not start life holding the first one's name.
  const [newFolderAt, setNewFolderAt] = useState<number | null>(null)

  // Canvas menu → new folder, created under whichever folder is open.
  const createFolder = useCallback(async (name: string, parentId: number | null,
                                          color: string, icon: string | null) => {
    try {
      await foldersApi.create({ name, parentId, color, icon })
      toast.success('أُنشئ المجلد.')
      foldersApi.list().then(setFolders).catch(() => {})
    } catch {
      toast.error('تعذّر إنشاء المجلد.')
    }
  }, [toast])

  /**
   * Remove a document outright — the ordinary way to undo an upload that should
   * not have happened. Distinct from «طلب إتلاف», which is the records-management
   * route for a document that has been kept and now needs formal disposal; a file
   * dragged in by mistake should not have to go through a committee. Soft delete,
   * so it can still be recovered.
   */
  const deleteDocument = useCallback(async (id: number, title: string) => {
    if (!(await confirm({
      message: `حذف «${title}»؟ حذف منطقي — يمكن استرجاعه لاحقًا.`,
      destructive: true,
    }))) return
    try {
      await documents.remove(id)
      toast.success('حُذفت الوثيقة.')
      // The trash badge is a count of what is in there; deleting is the one action
      // that changes it without the trash being open.
      await Promise.all([load(true), loadTrash()])
      foldersApi.list().then(setFolders).catch(() => {})
    } catch {
      toast.error('تعذّر حذف الوثيقة.')
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [load])

  const editFolder = useCallback(async (id: number, name: string, color: string, icon: string | null) => {
    try {
      await foldersApi.update(id, { name, color, icon })
      toast.success('حُفظ المجلد.')
      foldersApi.list().then(setFolders).catch(() => {})
    } catch {
      toast.error('تعذّر تعديل المجلد.')
    }
  }, [toast])

  /**
   * File a document straight from the disk: one document per chosen file, named
   * after it, dropped into the folder the upload started from.
   *
   * No physical place is asked for — this is a born-digital record, and pretending
   * it has a shelf would put a paper original into the register that does not exist.
   * The owning unit and type default the same way the full registration form does.
   */
  const uploadToFolder = useCallback(async (parentId: number | null, chosen: File[]) => {
    try {
      const units = await documents.orgUnits()
      if (units.length === 0) { toast.error('لا توجد وحدة مالكة — أنشئ وحدة تنظيمية أولًا.'); return }
      const types = (await documents.types(units[0].id)).filter((x) => x.isActive)
      if (types.length === 0) { toast.error('لا يوجد نوع وثيقة مفعّل — أنشئ نوعًا أولًا.'); return }

      let done = 0
      for (const file of chosen) {
        const created = await documents.create({
          title: file.name.replace(/\.[^.]+$/, '') || file.name,
          description: null,
          documentTypeId: types[0].id,
          owningOrgUnitId: units[0].id,
          confidentiality: 1,
          keywords: null,
          documentDate: null,
          boxId: null,
        })
        await documents.upload(created.id, file)
        if (parentId) await foldersApi.moveDocument(created.id, parentId)
        done++
      }
      toast.success(done === 1 ? 'رُفع الملف وأُنشئت وثيقته.' : `رُفعت ${done} ملفات وأُنشئت وثائقها.`)
      await load(true)
      foldersApi.list().then(setFolders).catch(() => {})
    } catch {
      toast.error('تعذّر رفع الملف.')
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [load])

  // The register rows, in the shape the browser panel renders.
  const thumbnails = useDocumentThumbnails(data?.items ?? [])
  const browserFiles = useMemo<BrowserFile[]>(() => (data?.items ?? []).map((d) => ({
    id: d.id,
    name: d.title,
    extension: d.fileExtension,
    attachmentCount: d.attachmentCount,
    sizeBytes: d.totalSizeBytes,
    kind: d.documentTypeName,
    caption: d.documentNumber,
    createdAt: d.createdAt,
    updatedAt: d.updatedAt,
    previewSrc: thumbnails[d.id] ?? null,
  })), [data, thumbnails])

  useEffect(() => { load() }, [load])
  useAutoRefresh(() => load(true), 30000)
  useEffect(() => { foldersApi.list().then(setFolders).catch(() => {}) }, [])
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

  // Defined out here so neither branch below narrows `mode` and makes these
  // comparisons look impossible to the compiler.
  const viewSwitch = (
    <div className="docs-viewswitch">
      <button type="button" className={mode === 'browser' ? 'is-on' : ''} onClick={() => setMode('browser')}>
        متصفّح الملفات
      </button>
      <button type="button" className={mode === 'table' ? 'is-on' : ''} onClick={() => setMode('table')}>
        جدول السجل
      </button>
    </div>
  )

  return (
    // In browser mode the panel is the page: it takes the height the shell has
    // left rather than a fixed box with dead space under it.
    <div className={mode === 'browser' ? 'docs-fill' : undefined}>

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
        {/* Same dialog the browser's own «مجلد جديد» opens — one way to make a
            folder, so a folder made from the toolbar can pick a colour too. */}
        <button className="btn btn-ghost btn-sm" title="إنشاء مجلد"
          onClick={() => setNewFolderAt(Date.now())}>+ مجلد</button>
        <input type="date" className="filters__status" dir="ltr" value={dateFrom} title="من تاريخ"
          onChange={(e) => { setDateFrom(e.target.value); setPage(1) }} />
        <input type="date" className="filters__status" dir="ltr" value={dateTo} title="إلى تاريخ"
          onChange={(e) => { setDateTo(e.target.value); setPage(1) }} />
        <button className={`btn btn-sm ${favOnly ? 'btn-primary' : 'btn-ghost'}`}
          onClick={() => { setFavOnly((v) => !v); setPage(1) }}><Star size={15} aria-hidden /> المفضلة</button>
        <button className={`btn btn-sm ${sharedOnly ? 'btn-primary' : 'btn-ghost'}`}
          onClick={() => { setSharedOnly((v) => !v); setPage(1) }}><Users size={15} aria-hidden /> مشاركة معي</button>
      </div>

      {error && <p className="login__error">{error}</p>}

      <Card className={cn('print-hide', mode === 'browser' && 'docs-fill__card')}>
        {/* In browser mode the switcher rides inside the panel's toolbar, so the
            card needs no header of its own — one bar instead of two. */}
        {mode === 'table' && (
          <CardHeader>{viewSwitch}</CardHeader>
        )}
        <CardContent className={mode === 'browser' ? 'docs-fill__body' : undefined}>
      {mode === 'browser' ? (
        <FileBrowser
          folders={folders}
          files={inTrash ? trashFiles : [...browserFiles, ...plainRows]}
          currentFolderId={folderId ? Number(folderId) : null}
          onFolderChange={(id) => {
            // Any move in the tree leaves the trash — it is a destination, not a filter.
            setInTrash(false)
            setFolderId(id === null ? '' : String(id)); setPage(1)
          }}
          onOpenFile={(file) => {
            if (inTrash) return
            if (file.origin === 'file') {
              const row = plainFiles.find((f) => f.id === file.id)
              if (row) openPlainFile(row)
            } else navigate(`/app/documents/${file.id}`)
          }}
          rootLabel={t('documents.title')}
          loading={loading}
          search={search}
          onSearchChange={(v) => { setSearch(v); setPage(1) }}
          onMoveFile={moveToFolder}
          toolbarExtra={viewSwitch}
          onCreateFolder={createFolder}
          onCreateFile={(parentId) => navigate(parentId ? `/app/documents/new?folderId=${parentId}` : '/app/documents/new')}
          folderTree
          trash={{
            active: inTrash,
            count: trashItems.length + trashPlain.length,
            onOpen: () => { setInTrash(true); loadTrash() },
            onEmpty: emptyTrash,
          }}
          onEditFolder={editFolder}
          onUploadFiles={uploadPlainFiles}
          onUploadDocuments={uploadToFolder}
          renderFolderMenu={(folder, actions) => (
            <>
              <ContextMenuLabel>{folder.name}</ContextMenuLabel>
              <ContextMenuSeparator />
              <ContextMenuItem onSelect={() => { setFolderId(String(folder.id)); setPage(1) }}>
                <FolderOpen className="ml-2 h-4 w-4 opacity-60" />
                فتح المجلد
              </ContextMenuItem>
              {/* Putting something in needs the edit grant — offering it on a folder you were
                  only shown produces a button whose whole behaviour is to be refused. */}
              {folder.canEdit !== false && (
                <ContextMenuItem onSelect={() => actions.upload()}>
                  <Upload className="ml-2 h-4 w-4 opacity-60" />
                  رفع ملف إلى المجلد…
                </ContextMenuItem>
              )}
              <ContextMenuSeparator />
              {/* Only what this caller actually holds. A borrowed folder still opens and exports;
                  changing it needs the edit grant, and giving it away stays with its owner. */}
              {folder.canEdit !== false && (
                <ContextMenuItem onSelect={() => actions.edit()}>
                  <Pencil className="ml-2 h-4 w-4 opacity-60" />
                  تعديل المجلد (الاسم واللون والرمز)
                </ContextMenuItem>
              )}
              {!folder.shared && (
                <ContextMenuItem onSelect={() => setShareFor({ id: folder.id, name: folder.name, kind: 'folder' })}>
                  <Users className="ml-2 h-4 w-4 opacity-60" />
                  مشاركة المجلد…
                </ContextMenuItem>
              )}
              <ContextMenuItem onSelect={() => exportApi.exportAll({ folderId: folder.id }).catch(() => toast.error(t('documents.loadError')))}>
                <Download className="ml-2 h-4 w-4 opacity-60" />
                تصدير محتوى المجلد
              </ContextMenuItem>
              {folder.shared && folder.ownerName && (
                <>
                  <ContextMenuSeparator />
                  <ContextMenuLabel className="text-xs font-normal opacity-70">
                    مُشارَك معك من {folder.ownerName}
                  </ContextMenuLabel>
                </>
              )}
            </>
          )}
          renderFileMenu={(file) => (file.origin === 'file' && !inTrash ? (() => {
            const row = plainFiles.find((f) => f.id === file.id)
            if (!row) return null
            return (
              <>
                <ContextMenuLabel>{row.name}</ContextMenuLabel>
                <ContextMenuSeparator />
                <ContextMenuItem onSelect={() => openPlainFile(row)}>
                  <Eye className="ml-2 h-4 w-4 opacity-60" />
                  فتح
                </ContextMenuItem>
                <ContextMenuItem onSelect={() => filesApi.download(row.id, row.name, row.contentType)}>
                  <Download className="ml-2 h-4 w-4 opacity-60" />
                  تنزيل
                </ContextMenuItem>
                <ContextMenuSeparator />
                {row.canEdit && (
                  <ContextMenuItem onSelect={() => renameFile(row)}>
                    <Pencil className="ml-2 h-4 w-4 opacity-60" />
                    إعادة تسمية
                  </ContextMenuItem>
                )}
                <ContextMenuItem onSelect={() => setFilePropsFor({ id: row.id, name: row.name })}>
                  <Info className="ml-2 h-4 w-4 opacity-60" />
                  خصائص
                </ContextMenuItem>
                {!row.shared && (
                  <ContextMenuItem onSelect={() => setShareFor({ id: row.id, name: row.name, kind: 'file' })}>
                    <Users className="ml-2 h-4 w-4 opacity-60" />
                    مشاركة الملف…
                  </ContextMenuItem>
                )}
                {folderId && row.canEdit && (
                  <ContextMenuItem onSelect={() => moveToFolder(file, null)}>
                    <FolderOpen className="ml-2 h-4 w-4 opacity-60" />
                    إخراج من المجلد
                  </ContextMenuItem>
                )}
                {/* Deleting is the owner's alone — an edit grant is help with someone's
                    filing, not permission to undo it. */}
                {!row.shared ? (
                  <>
                    <ContextMenuSeparator />
                    <ContextMenuItem className="text-destructive focus:text-destructive"
                      onSelect={() => deleteFile(row)}>
                      <Trash2 className="ml-2 h-4 w-4 opacity-60" />
                      حذف
                    </ContextMenuItem>
                  </>
                ) : row.ownerName ? (
                  <>
                    <ContextMenuSeparator />
                    <ContextMenuLabel className="text-xs font-normal opacity-70">
                      مُشارَك معك من {row.ownerName}
                    </ContextMenuLabel>
                  </>
                ) : null}
              </>
            )
          })() : inTrash ? (
            <>
              <ContextMenuLabel>{file.caption ?? file.name}</ContextMenuLabel>
              <ContextMenuSeparator />
              <ContextMenuItem onSelect={() => restoreTrashed(file)}>
                <RotateCcw className="ml-2 h-4 w-4 opacity-60" />
                استعادة
              </ContextMenuItem>
              <ContextMenuItem className="text-destructive focus:text-destructive"
                onSelect={() => purgeTrashed(file)}>
                <Trash2 className="ml-2 h-4 w-4 opacity-60" />
                حذف نهائي
              </ContextMenuItem>
            </>
          ) : (
            <>
              <ContextMenuLabel>{file.caption ?? file.name}</ContextMenuLabel>
              <ContextMenuSeparator />
              <ContextMenuItem onSelect={() => navigate(`/app/documents/${file.id}`)}>
                <Eye className="ml-2 h-4 w-4 opacity-60" />
                فتح الوثيقة
                <ContextMenuShortcut>↵</ContextMenuShortcut>
              </ContextMenuItem>
              <ContextMenuItem onSelect={() => window.open(`/app/documents/${file.id}`, '_blank')}>
                <FolderOpen className="ml-2 h-4 w-4 opacity-60" />
                فتح في نافذة جديدة
              </ContextMenuItem>
              {canEdit && (
                <>
                  <ContextMenuSeparator />
                  <ContextMenuItem onSelect={() => navigate(`/app/documents/${file.id}/edit`)}>
                    <Pencil className="ml-2 h-4 w-4 opacity-60" />
                    تعديل
                  </ContextMenuItem>
                </>
              )}
              <ContextMenuSeparator />
              <ContextMenuItem onSelect={() => setShareFor({ id: file.id, name: file.name, kind: 'document' })}>
                <Users className="ml-2 h-4 w-4 opacity-60" />
                مشاركة الوثيقة…
              </ContextMenuItem>
              <ContextMenuItem onSelect={() => copyNumber(file.caption ?? '')}>
                <Copy className="ml-2 h-4 w-4 opacity-60" />
                نسخ رقم الوثيقة
                <ContextMenuShortcut>⌘C</ContextMenuShortcut>
              </ContextMenuItem>
              {canPrint && (
                <ContextMenuItem onSelect={() => navigate(`/app/documents/${file.id}`)}>
                  <Printer className="ml-2 h-4 w-4 opacity-60" />
                  طباعة
                  <ContextMenuShortcut>⌘P</ContextMenuShortcut>
                </ContextMenuItem>
              )}
              <ContextMenuItem onSelect={() => setPropsFor({ id: file.id, title: file.name })}>
                <Info className="ml-2 h-4 w-4 opacity-60" />
                خصائص
              </ContextMenuItem>
              {/* Moving out of a folder has no drag target when the browser is
                  already at the root, so it stays available in the menu. */}
              {folderId && (
                <ContextMenuItem onSelect={() => moveToFolder(file, null)}>
                  <FolderOpen className="ml-2 h-4 w-4 opacity-60" />
                  إخراج من المجلد
                </ContextMenuItem>
              )}
              {(canDelete || canDestroy) && <ContextMenuSeparator />}
              {canDelete && (
                <ContextMenuItem className="text-destructive focus:text-destructive"
                  onSelect={() => deleteDocument(file.id, file.name)}>
                  <Trash2 className="ml-2 h-4 w-4 opacity-60" />
                  حذف الوثيقة
                </ContextMenuItem>
              )}
              {canDestroy && (
                <ContextMenuItem className="text-destructive focus:text-destructive"
                  onSelect={() => setReqFor({ id: file.id, number: file.caption ?? '' })}>
                  <Trash2 className="ml-2 h-4 w-4 opacity-60" />
                  طلب إتلاف
                </ContextMenuItem>
              )}
            </>
          ))}
        />
      ) : (
        <>
      <div className="table-card">
        <table className="reg-table">
          <thead>
            <tr>
              <ExpandHeadCell />
              {columns.map((col) => <th key={col.key}><ColumnHeaderMenu label={col.label} {...getColumnProps(col.key)} /></th>)}
            </tr>
          </thead>
          <tbody>
            {loading && Array.from({ length: 6 }).map((_, i) => (
              <tr key={`sk${i}`} className="reg-row reg-skel">
                <td className="reg-chev-cell" />
                {columns.map((col) => <td key={col.key}><span className="skel-bar" /></td>)}
              </tr>
            ))}
            {!loading && data?.items.length === 0 && (
              <tr><td colSpan={columns.length + 1} className="reg-empty">
                <div className="empty-state">
                  <span className="empty-state__icon" aria-hidden>▤</span>
                  <span className="empty-state__text">{t('documents.empty')}</span>
                  {auth.hasPermission('Documents.Create') && (
                    <Link to="/app/documents/new" className="btn btn-ghost btn-sm">{t('documents.newButton')}</Link>
                  )}
                </div>
              </td></tr>
            )}
            {!loading && viewRows.map((d) => {
              const c = CONFIDENTIALITY_LABELS[d.confidentiality] ?? { ar: d.confidentiality, cls: 'internal' }
              const open = isExpanded(d.id)
              return (
                <Fragment key={d.id}>
                <ContextMenu>
                  <ContextMenuTrigger asChild>
                    <tr
                      onClick={() => navigate(`/app/documents/${d.id}`)}
                      className="reg-row"
                      title="انقر للفتح · انقر بالزر الأيمن للإجراءات السريعة"
                    >
                      <ExpandCell expanded={open} onToggle={() => toggle(d.id)} />
                      {columns.map((col) => {
                        switch (col.key) {
                          case 'fileType': return (
                            <td key={col.key} className="reg-filetype">
                              {/* The wrapper is rendered either way so rows without an
                                  attachment keep the same height as rows with a card. */}
                              <span className="reg-filetype__card" title={d.fileExtension ? `${d.fileExtension.toUpperCase()}${d.attachmentCount > 1 ? ` · ${d.attachmentCount} مرفقات` : ''}` : 'بلا مرفق'}>
                                {d.fileExtension ? (
                                  <>
                                    <FileCard formatFile={fileFormatOf(d.fileExtension)}
                                      badgeLabel={fileBadgeOf(d.fileExtension) ?? undefined} />
                                    {d.attachmentCount > 1 && (
                                      <span className="reg-filetype__count">{d.attachmentCount}</span>
                                    )}
                                  </>
                                ) : (
                                  <span className="reg-filetype__none">—</span>
                                )}
                              </span>
                            </td>
                          )
                          case 'title': return <td key={col.key} className="reg-subject">{d.title}</td>
                          case 'type': return <td key={col.key}>{d.documentTypeName}</td>
                          case 'confidentiality': return <td key={col.key}><span className={`badge ${c.cls}`}>{c.ar}</span></td>
                          case 'status': return <td key={col.key}><span className={`status-pill s-${d.status.toLowerCase()}`}>{DOC_STATUS_LABELS[d.status] ?? d.status}</span></td>
                          case 'location': return <td key={col.key}>{d.boxCode ? d.boxCode : d.physicalLocationName ? `${d.physicalLocationName}${d.boxNumber ? ` · ${d.boxNumber}` : ''}` : '—'}</td>
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

                    <ContextMenuItem onSelect={() => setShareFor({ id: d.id, name: d.title, kind: 'document' })}>
                      <Users className="ml-2 h-4 w-4 opacity-60" />
                      مشاركة الوثيقة…
                    </ContextMenuItem>

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

                    <ContextMenuItem onSelect={() => setPropsFor({ id: d.id, title: d.title })}>
                      <Info className="ml-2 h-4 w-4 opacity-60" />
                      خصائص
                    </ContextMenuItem>

                    {(canDelete || canDestroy) && <ContextMenuSeparator />}
                    {canDelete && (
                      <ContextMenuItem className="text-destructive focus:text-destructive"
                        onSelect={() => deleteDocument(d.id, d.title)}>
                        <Trash2 className="ml-2 h-4 w-4 opacity-60" />
                        حذف الوثيقة
                      </ContextMenuItem>
                    )}
                    {canDestroy && (
                      <ContextMenuItem className="text-destructive focus:text-destructive" onSelect={() => setReqFor({ id: d.id, number: d.documentNumber })}>
                        <Trash2 className="ml-2 h-4 w-4 opacity-60" />
                        طلب إتلاف
                      </ContextMenuItem>
                    )}
                  </ContextMenuContent>
                </ContextMenu>

                {/* Surfaces the fields the user has hidden via column settings,
                    so collapsing columns no longer means losing access to them. */}
                <ExpandedRow colSpan={columns.length + 1} expanded={open}>
                  <DetailFields>
                    <DetailField label="رقم الوثيقة"><span className="mono">{d.documentNumber}</span></DetailField>
                    <DetailField label="نوع الوثيقة">{d.documentTypeName}</DetailField>
                    <DetailField label="تاريخ الوثيقة"><span className="mono">{d.documentDate ?? '—'}</span></DetailField>
                    <DetailField label="تاريخ الانتهاء"><span className="mono">{d.expiryDate ?? '—'}</span></DetailField>
                    <DetailField label="الإصدار"><span className="mono">{d.version}</span></DetailField>
                    <DetailField label="رقم الملف"><span className="mono">{d.fileNumber ?? '—'}</span></DetailField>
                    <DetailField label="المكان">
                      {d.boxCode ? d.boxCode : d.physicalLocationName
                        ? `${d.physicalLocationName}${d.boxNumber ? ` · ${d.boxNumber}` : ''}` : '—'}
                    </DetailField>
                    <DetailField label="أُنشئت في"><span className="mono">{d.createdAt?.slice(0, 10) ?? '—'}</span></DetailField>
                  </DetailFields>
                  <DetailActions>
                    <Link to={`/app/documents/${d.id}`} className="btn btn-ghost btn-sm">فتح الوثيقة</Link>
                    {canEdit && <Link to={`/app/documents/${d.id}/edit`} className="btn btn-ghost btn-sm">تعديل</Link>}
                    <button type="button" className="btn btn-ghost btn-sm" onClick={() => copyNumber(d.documentNumber)}>نسخ الرقم</button>
                    {canDestroy && (
                      <button type="button" className="btn btn-ghost btn-sm text-destructive"
                        onClick={() => setReqFor({ id: d.id, number: d.documentNumber })}>طلب إتلاف</button>
                    )}
                  </DetailActions>
                </ExpandedRow>
                </Fragment>
              )
            })}
          </tbody>
        </table>
      </div>

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
        </>
      )}
        </CardContent>
      </Card>

      {reqFor && (
        <RequestDispositionModal documentId={reqFor.id} documentNumber={reqFor.number}
          onClose={() => setReqFor(null)} />
      )}
      {propsFor && (
        <DocumentPropertiesDialog documentId={propsFor.id} fallbackTitle={propsFor.title}
          onClose={() => setPropsFor(null)} />
      )}
      {filePropsFor && (
        <FilePropertiesDialog fileId={filePropsFor.id} fallbackName={filePropsFor.name}
          onClose={() => setFilePropsFor(null)} />
      )}
      {shareFor && (
        <ShareItemDialog
          key={`${shareFor.kind}${shareFor.id}`}
          id={shareFor.id}
          name={shareFor.name}
          kind={shareFor.kind}
          api={shareFor.kind === 'folder' ? folderSharesApi
            : shareFor.kind === 'document' ? documentShareApi : fileSharesApi}
          onClose={() => {
            setShareFor(null)
            // The badge on the tile comes from the listing, so it only changes once refetched.
            foldersApi.list().then(setFolders).catch(() => {})
            loadFiles()
          }}
        />
      )}
      <FolderDialog
        key={newFolderAt ?? 'closed'}
        open={newFolderAt !== null}
        mode="create"
        onOpenChange={(open) => { if (!open) setNewFolderAt(null) }}
        // Created inside whichever folder the filter is on, so the toolbar makes
        // the folder where you are looking rather than always at the root.
        onSubmit={(d) => createFolder(d.name, folderId ? Number(folderId) : null, d.color, d.icon)}
      />
    </div>
  )
}
