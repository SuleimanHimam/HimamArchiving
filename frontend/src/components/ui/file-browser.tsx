import * as React from 'react'
import {
  ArrowUpDown, Calendar, Check, ChevronLeft, ChevronRight, Columns2, FileText,
  Filter, Image as ImageIcon, LayoutGrid, List as ListIcon, Monitor, Search,
  Sheet, X, FolderPlus, FilePlus2, FolderOpen, Upload, Trash2, Users,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { FileCard } from '@/components/ui/file-card-collections'
import { fileBadgeOf, fileFormatOf, formatBytes } from '@/lib/fileFormats'
import { FilesystemItem, type FilesystemNode } from '@/components/ui/filesystem-item'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator,
  DropdownMenuSub, DropdownMenuSubContent, DropdownMenuSubTrigger, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import {
  FolderDialog, FolderGlyph, type FolderDraft,
} from '@/components/ui/folder-dialog'
import {
  ContextMenu, ContextMenuContent, ContextMenuItem, ContextMenuLabel, ContextMenuSeparator,
  ContextMenuSub, ContextMenuSubContent, ContextMenuSubTrigger, ContextMenuTrigger,
} from '@/components/ui/context-menu'

export type BrowserFolder = {
  id: number
  name: string
  parentId: number | null
  documentCount: number
  /** Palette key from FOLDER_COLORS; anything unknown falls back to blue. */
  color?: string | null
  /** Key from FOLDER_ICONS; null draws a plain folder. */
  icon?: string | null
  /** Someone else's folder, opened up to this user. The row says whose and offers less. */
  shared?: boolean
  /** Whether this user may change the folder or add to it. */
  canEdit?: boolean
  ownerName?: string | null
}

export type BrowserFile = {
  id: number
  name: string
  /** Primary attachment extension; null renders a plain page with no badge. */
  extension: string | null
  attachmentCount: number
  sizeBytes: number
  /** Business kind (نوع الوثيقة) — shown as the item's kind line. */
  kind?: string
  caption?: string
  createdAt?: string | null
  updatedAt?: string | null
  /** Where the row came from. A plain file has no document behind it, so it
   *  offers a different menu and a different move endpoint. */
  origin?: 'document' | 'file'
  /** Object URL of the file itself, when it is an image the browser can paint.
   *  Supplied by the page, which owns fetching and revoking it. */
  previewSrc?: string | null
  /** Someone else's file, shared with this user. */
  shared?: boolean
  canEdit?: boolean
  ownerName?: string | null
}

export type FileBrowserView = 'grid' | 'list' | 'columns' | 'gallery'
export type FileBrowserSortKey = 'name' | 'kind' | 'size' | 'created' | 'modified'

export type BrowserDateFilter = {
  /** Relative preset in days, or null when `from`/`to` carry a custom range. */
  days: number | null
  from?: string
  to?: string
}

export type BrowserFilters = {
  /** Selected file extensions; empty means no file-type filter. */
  extensions: string[]
  modified: BrowserDateFilter | null
  created: BrowserDateFilter | null
}

const EMPTY_FILTERS: BrowserFilters = { extensions: [], modified: null, created: null }

const VIEW_OPTIONS: Array<{ value: FileBrowserView; label: string; icon: typeof LayoutGrid }> = [
  { value: 'grid', label: 'شبكة', icon: LayoutGrid },
  { value: 'list', label: 'قائمة', icon: ListIcon },
  { value: 'columns', label: 'أعمدة', icon: Columns2 },
  { value: 'gallery', label: 'معرض', icon: Monitor },
]

const SORT_OPTIONS: Array<{ value: FileBrowserSortKey; label: string }> = [
  { value: 'name', label: 'الاسم' },
  { value: 'kind', label: 'النوع' },
  { value: 'size', label: 'الحجم' },
  { value: 'created', label: 'تاريخ الإنشاء' },
  { value: 'modified', label: 'تاريخ التعديل' },
]

// Relative cutoffs for the date filters. The value is the number of days back,
// so the label can be translated without changing what it means.
const DATE_PRESETS: Array<{ days: number; label: string }> = [
  { days: 1, label: 'منذ يوم' },
  { days: 3, label: 'منذ 3 أيام' },
  { days: 7, label: 'منذ أسبوع' },
  { days: 30, label: 'منذ شهر' },
  { days: 90, label: 'منذ 3 أشهر' },
  { days: 180, label: 'منذ 6 أشهر' },
  { days: 365, label: 'منذ سنة' },
]

/** Extension → the group and label the file-type filter lists it under. */
const TYPE_GROUPS: Array<{
  group: string
  icon: typeof FileText
  types: Array<{ ext: string; label: string }>
}> = [
  {
    group: 'مستندات',
    icon: FileText,
    types: [
      { ext: 'pdf', label: 'PDF' },
      { ext: 'docx', label: 'مستند Word' },
      { ext: 'doc', label: 'مستند Word (قديم)' },
      { ext: 'pptx', label: 'عرض PowerPoint' },
    ],
  },
  {
    group: 'جداول',
    icon: Sheet,
    types: [
      { ext: 'xlsx', label: 'مصنّف Excel' },
      { ext: 'xls', label: 'مصنّف Excel (قديم)' },
      { ext: 'csv', label: 'CSV' },
    ],
  },
  {
    group: 'صور',
    icon: ImageIcon,
    types: [
      { ext: 'png', label: 'صورة PNG' },
      { ext: 'jpg', label: 'صورة JPEG' },
      { ext: 'tiff', label: 'صورة TIFF' },
      { ext: 'gif', label: 'صورة GIF' },
    ],
  },
]

function formatStamp(value: string | null | undefined) {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return date.toLocaleDateString('ar', { year: 'numeric', month: 'short', day: 'numeric' })
}

function dateFilterLabel(filter: BrowserDateFilter) {
  if (filter.days !== null) {
    return DATE_PRESETS.find((p) => p.days === filter.days)?.label ?? `منذ ${filter.days} يومًا`
  }
  return `${filter.from ?? '…'} ← ${filter.to ?? '…'}`
}

/** True when `value` falls inside the filter's window. */
function matchesDateFilter(value: string | null | undefined, filter: BrowserDateFilter) {
  const time = value ? Date.parse(value) : Number.NaN
  if (Number.isNaN(time)) return false
  if (filter.days !== null) {
    const cutoff = Date.now() - filter.days * 86_400_000
    return time >= cutoff
  }
  const from = filter.from ? Date.parse(filter.from) : Number.NEGATIVE_INFINITY
  // `to` names a day; include everything up to its final millisecond.
  const to = filter.to ? Date.parse(filter.to) + 86_399_999 : Number.POSITIVE_INFINITY
  return time >= from && time <= to
}

// ─── Custom date range dialog ───────────────────────────────────────────────

const WEEKDAYS = ['أحد', 'اثن', 'ثلا', 'أرب', 'خمي', 'جمع', 'سبت']

function toISODate(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function MonthGrid({
  month,
  from,
  to,
  onPick,
}: {
  month: Date
  from: string
  to: string
  onPick: (iso: string) => void
}) {
  const year = month.getFullYear()
  const monthIndex = month.getMonth()
  const first = new Date(year, monthIndex, 1)
  const daysInMonth = new Date(year, monthIndex + 1, 0).getDate()
  const lead = first.getDay()
  const cells: Array<number | null> = [
    ...Array.from({ length: lead }, () => null),
    ...Array.from({ length: daysInMonth }, (_, i) => i + 1),
  ]

  return (
    <div className="fb__month">
      <div className="fb__monthname">
        {month.toLocaleDateString('ar', { month: 'long', year: 'numeric' })}
      </div>
      <div className="fb__daynames">
        {WEEKDAYS.map((d) => <span key={d}>{d}</span>)}
      </div>
      <div className="fb__days">
        {cells.map((day, i) => {
          if (day === null) return <span key={`p${i}`} />
          const iso = toISODate(new Date(year, monthIndex, day))
          const isEdge = iso === from || iso === to
          const inRange = Boolean(from && to && iso > from && iso < to)
          return (
            <button
              key={iso}
              type="button"
              className={cn('fb__day', isEdge && 'fb__day--on', inRange && 'fb__day--in')}
              onClick={() => onPick(iso)}
            >
              {day}
            </button>
          )
        })}
      </div>
    </div>
  )
}

const RANGE_PRESETS: Array<{ label: string; range: () => [string, string] }> = [
  { label: 'آخر 7 أيام', range: () => [toISODate(new Date(Date.now() - 7 * 86_400_000)), toISODate(new Date())] },
  { label: 'هذا الشهر', range: () => {
    const now = new Date()
    return [toISODate(new Date(now.getFullYear(), now.getMonth(), 1)), toISODate(now)]
  } },
  { label: 'آخر شهر', range: () => [toISODate(new Date(Date.now() - 30 * 86_400_000)), toISODate(new Date())] },
  { label: 'آخر 3 أشهر', range: () => [toISODate(new Date(Date.now() - 90 * 86_400_000)), toISODate(new Date())] },
  { label: 'هذه السنة', range: () => {
    const now = new Date()
    return [toISODate(new Date(now.getFullYear(), 0, 1)), toISODate(now)]
  } },
  { label: 'آخر 12 شهرًا', range: () => [toISODate(new Date(Date.now() - 365 * 86_400_000)), toISODate(new Date())] },
]

function DateRangeDialog({
  open,
  onOpenChange,
  onApply,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  onApply: (from: string, to: string) => void
}) {
  // The caller remounts this on open (keyed by which filter asked for it), so
  // the fields start empty without an effect that resets them.
  const [from, setFrom] = React.useState('')
  const [to, setTo] = React.useState('')
  const [anchor, setAnchor] = React.useState(() => new Date())

  // First click sets the start; the next one closes the range (and swaps if
  // the user picked backwards).
  const pick = (iso: string) => {
    if (!from || (from && to)) { setFrom(iso); setTo('') }
    else if (iso < from) { setTo(from); setFrom(iso) }
    else setTo(iso)
  }

  const nextMonth = new Date(anchor.getFullYear(), anchor.getMonth() + 1, 1)

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="fb__rangedlg">
        <DialogHeader>
          <DialogTitle>نطاق تاريخ مخصّص</DialogTitle>
        </DialogHeader>

        <div className="fb__rangefields">
          <label>
            <span>من</span>
            <input value={from} onChange={(e) => setFrom(e.target.value)} placeholder="YYYY-MM-DD" />
          </label>
          <label>
            <span>إلى</span>
            <input value={to} onChange={(e) => setTo(e.target.value)} placeholder="YYYY-MM-DD" />
          </label>
        </div>

        <div className="fb__calendar">
          <button type="button" className="fb__calnav" aria-label="الشهر السابق"
            onClick={() => setAnchor(new Date(anchor.getFullYear(), anchor.getMonth() - 1, 1))}>
            <ChevronRight size={16} />
          </button>
          <MonthGrid month={anchor} from={from} to={to} onPick={pick} />
          <MonthGrid month={nextMonth} from={from} to={to} onPick={pick} />
          <button type="button" className="fb__calnav" aria-label="الشهر التالي"
            onClick={() => setAnchor(new Date(anchor.getFullYear(), anchor.getMonth() + 1, 1))}>
            <ChevronLeft size={16} />
          </button>
        </div>

        <div className="fb__rangepresets">
          {RANGE_PRESETS.map((preset) => (
            <button key={preset.label} type="button" onClick={() => {
              const [a, b] = preset.range()
              setFrom(a); setTo(b)
            }}>{preset.label}</button>
          ))}
        </div>

        <DialogFooter>
          <button type="button" className="btn btn-ghost" onClick={() => onOpenChange(false)}>إلغاء</button>
          <button type="button" className="btn" disabled={!from || !to}
            onClick={() => { onApply(from, to); onOpenChange(false) }}>تطبيق</button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

// ─── Browser ────────────────────────────────────────────────────────────────

export type FileBrowserProps = {
  folders: BrowserFolder[]
  files: BrowserFile[]
  currentFolderId: number | null
  onFolderChange: (id: number | null) => void
  onOpenFile: (file: BrowserFile) => void
  rootLabel?: string
  loading?: boolean
  search: string
  onSearchChange: (value: string) => void
  /**
   * Right-click menu bodies. Supplied by the page so the browser's menus stay
   * identical to the register table's, permissions included.
   */
  renderFileMenu?: (file: BrowserFile) => React.ReactNode
  /** The actions are supplied by the browser because their dialogs live here —
   *  the page decides whether to offer them, the browser runs them. */
  renderFolderMenu?: (folder: BrowserFolder, actions: FolderActions) => React.ReactNode
  /** Drag a document onto a folder (or the breadcrumb) to move it. */
  onMoveFile?: (file: BrowserFile, folderId: number | null) => void
  /** Rendered at the end of the toolbar, so the page can fold its own controls
   *  into this bar instead of stacking a second one above it. */
  toolbarExtra?: React.ReactNode
  /** Right-click the empty canvas → create. Omit either to hide that entry. */
  onCreateFolder?: (name: string, parentId: number | null, color: string, icon: string | null) => void
  onCreateFile?: (parentId: number | null) => void
  /** Rename / recolour / re-badge an existing folder. */
  onEditFolder?: (folderId: number, name: string, color: string, icon: string | null) => void
  /** Files chosen from the disk, kept as plain files in `parentId` (null = root). */
  onUploadFiles?: (parentId: number | null, files: File[]) => void
  /** The same picker, but each file is registered as a document instead. */
  onUploadDocuments?: (parentId: number | null, files: File[]) => void
  /** Show the collapsible folder tree beside the contents. */
  folderTree?: boolean
  /** Trash pane, listed under the tree the way a drive lists it. */
  trash?: TrashPane
  className?: string
}

export type TrashPane = {
  /** True while the browser is showing deleted items rather than a folder. */
  active: boolean
  /** How many items are in there, shown beside the row. */
  count: number
  onOpen: () => void
  /** Destroy everything in the trash. Confirmation is the page's business. */
  onEmpty: () => void
}

/** Handed to `renderFolderMenu` so a page-built menu can open the browser's own dialogs. */
export type FolderActions = {
  /** Open the folder dialog on this folder. */
  edit: () => void
  /** Open the file picker, filing whatever is chosen into this folder. */
  upload: () => void
}

/** Custom type so the browser ignores drags that did not start inside it. */
const DRAG_MIME = 'application/x-archiving-document'

export function FileBrowser({
  folders,
  files,
  currentFolderId,
  onFolderChange,
  onOpenFile,
  rootLabel = 'الوثائق',
  loading = false,
  search,
  onSearchChange,
  renderFileMenu,
  renderFolderMenu,
  onMoveFile,
  toolbarExtra,
  onCreateFolder,
  onCreateFile,
  onEditFolder,
  onUploadFiles,
  onUploadDocuments,
  folderTree = false,
  trash,
  className,
}: FileBrowserProps) {
  const [view, setView] = React.useState<FileBrowserView>('grid')
  const [sortKey, setSortKey] = React.useState<FileBrowserSortKey>('name')
  const [sortAsc, setSortAsc] = React.useState(true)
  const [selected, setSelected] = React.useState<string | null>(null)
  const [filters, setFilters] = React.useState<BrowserFilters>(EMPTY_FILTERS)
  const [typeQuery, setTypeQuery] = React.useState('')
  const [rangeFor, setRangeFor] = React.useState<'modified' | 'created' | null>(null)
  // One dialog for both jobs: «مجلد جديد» and «تعديل المجلد» ask for exactly the
  // same three things, and a second copy of the pickers would drift from this one.
  const [folderDlg, setFolderDlg] = React.useState<
    | { mode: 'create'; parent: number | null }
    | { mode: 'edit'; id: number; initial: FolderDraft }
    | null>(null)

  // The picker is one hidden input reused by every entry point. The request is
  // state, not a ref, so opening it is a plain render-safe setState; the effect
  // below is the only thing that touches the element.
  const fileInput = React.useRef<HTMLInputElement>(null)
  const [pickReq, setPickReq] =
    React.useState<{ parent: number | null; as: 'file' | 'document'; at: number } | null>(null)
  React.useEffect(() => { if (pickReq) fileInput.current?.click() }, [pickReq])

  const openEditFolder = React.useCallback((folder: BrowserFolder) => {
    setFolderDlg({
      mode: 'edit',
      id: folder.id,
      initial: { name: folder.name, color: folder.color ?? 'blue', icon: folder.icon ?? null },
    })
  }, [])

  // The parent is passed in rather than read from the current folder: the tree's
  // menus create where they were opened, which is not always where you are.
  const openCreateFolder = React.useCallback((parent: number | null) => {
    setFolderDlg({ mode: 'create', parent })
  }, [])

  // `at` distinguishes two requests for the same folder, which would otherwise
  // be the same state value and never re-open the picker.
  const pickFiles = React.useCallback((parentId: number | null, as: 'file' | 'document' = 'file') => {
    setPickReq({ parent: parentId, as, at: Date.now() })
  }, [])

  const submitFolder = (draft: FolderDraft) => {
    if (!folderDlg) return
    if (folderDlg.mode === 'edit') onEditFolder?.(folderDlg.id, draft.name, draft.color, draft.icon)
    else onCreateFolder?.(draft.name, folderDlg.parent, draft.color, draft.icon)
  }

  const [history, setHistory] = React.useState<{ stack: Array<number | null>; index: number }>({
    stack: [currentFolderId],
    index: 0,
  })
  const canBack = history.index > 0
  const canForward = history.index < history.stack.length - 1

  // Sidebar width, dragged by the divider and remembered per browser. Clamped so
  // the pane can never be dragged to nothing or over the contents it navigates.
  const SIDE_MIN = 150
  const SIDE_MAX = 460
  const [sideWidth, setSideWidth] = React.useState(() => {
    const saved = Number(localStorage.getItem('fb.tree.width'))
    return Number.isFinite(saved) && saved >= SIDE_MIN && saved <= SIDE_MAX ? saved : 224
  })
  const sideRef = React.useRef<HTMLElement>(null)

  const startResize = (e: React.PointerEvent<HTMLDivElement>) => {
    e.preventDefault()
    const handle = e.currentTarget
    handle.setPointerCapture(e.pointerId)
    const rect = sideRef.current?.getBoundingClientRect()
    if (!rect) return
    // The pane hugs the reading-start edge, so which side the pointer measures
    // from flips with the direction: right edge in RTL, left edge in LTR.
    const rtl = getComputedStyle(handle).direction === 'rtl'
    const move = (ev: PointerEvent) => {
      const next = rtl ? rect.right - ev.clientX : ev.clientX - rect.left
      setSideWidth(Math.min(SIDE_MAX, Math.max(SIDE_MIN, Math.round(next))))
    }
    const stop = () => {
      handle.removeEventListener('pointermove', move)
      handle.removeEventListener('pointerup', stop)
      handle.removeEventListener('pointercancel', stop)
    }
    handle.addEventListener('pointermove', move)
    handle.addEventListener('pointerup', stop)
    handle.addEventListener('pointercancel', stop)
  }

  React.useEffect(() => { localStorage.setItem('fb.tree.width', String(sideWidth)) }, [sideWidth])

  /**
   * The flat folder list as a nested tree for the sidebar.
   *
   * Every branch on the path to the folder being viewed starts expanded —
   * otherwise opening a nested folder from the grid would leave the tree
   * collapsed and the current position invisible.
   */
  const treeNodes = React.useMemo<FilesystemNode[]>(() => {
    if (!folderTree) return []
    const byParent = new Map<number | null, BrowserFolder[]>()
    for (const f of folders) {
      const bucket = byParent.get(f.parentId)
      if (bucket) bucket.push(f)
      else byParent.set(f.parentId, [f])
    }

    const onPath = new Set<number>()
    for (let id = currentFolderId; id !== null; ) {
      onPath.add(id)
      id = folders.find((f) => f.id === id)?.parentId ?? null
    }

    const build = (parentId: number | null): FilesystemNode[] =>
      (byParent.get(parentId) ?? [])
        .slice()
        .sort((a2, b2) => a2.name.localeCompare(b2.name, 'ar'))
        .map((f) => ({
          id: f.id,
          name: f.name,
          nodes: build(f.id),
          defaultOpen: onPath.has(f.id),
        }))

    const nodes: FilesystemNode[] = [
      { id: 'root', name: rootLabel, nodes: build(null), defaultOpen: true },
    ]
    // A leaf, deliberately outside the folder hierarchy: the trash is not a place
    // documents live in, it is where they wait to stop existing.
    if (trash) nodes.push({ id: 'trash', name: `سلة المحذوفات${trash.count ? ` (${trash.count})` : ''}` })
    return nodes
  }, [folderTree, folders, currentFolderId, rootLabel, trash])

  const navigate = React.useCallback((id: number | null) => {
    setHistory((h) => {
      const stack = [...h.stack.slice(0, h.index + 1), id]
      return { stack, index: stack.length - 1 }
    })
    setSelected(null)
    onFolderChange(id)
  }, [onFolderChange])

  const step = React.useCallback((delta: -1 | 1) => {
    setHistory((h) => {
      const index = h.index + delta
      if (index < 0 || index >= h.stack.length) return h
      onFolderChange(h.stack[index])
      return { ...h, index }
    })
    setSelected(null)
  }, [onFolderChange])

  const byId = React.useMemo(() => new Map(folders.map((f) => [f.id, f])), [folders])

  const trail = React.useMemo(() => {
    const out: BrowserFolder[] = []
    let id = currentFolderId
    const guard = new Set<number>()
    while (id !== null && !guard.has(id)) {
      guard.add(id)
      const folder = byId.get(id)
      if (!folder) break
      out.unshift(folder)
      id = folder.parentId
    }
    return out
  }, [byId, currentFolderId])

  const collator = React.useMemo(() => new Intl.Collator('ar', { numeric: true, sensitivity: 'base' }), [])

  const sortedFolders = React.useMemo(
    // The trash holds documents, never folders — but the tree beside it still
    // lists every folder, so this filters the contents pane alone.
    () => (trash?.active ? [] : folders.filter((f) => f.parentId === currentFolderId))
      .sort((a, b) => collator.compare(a.name, b.name)),
    [folders, currentFolderId, collator, trash?.active],
  )

  // Files must clear every active filter. Folders are unaffected: they carry
  // no extension or attachment date of their own.
  const filteredFiles = React.useMemo(() => files.filter((file) => {
    if (filters.extensions.length && !filters.extensions.includes((file.extension ?? '').toLowerCase())) return false
    if (filters.modified && !matchesDateFilter(file.updatedAt ?? file.createdAt, filters.modified)) return false
    if (filters.created && !matchesDateFilter(file.createdAt, filters.created)) return false
    return true
  }), [files, filters])

  const sortedFiles = React.useMemo(() => {
    const dir = sortAsc ? 1 : -1
    const out = [...filteredFiles]
    out.sort((a, b) => {
      let result = 0
      if (sortKey === 'kind') result = (a.extension ?? '').localeCompare(b.extension ?? '')
      else if (sortKey === 'size') result = a.sizeBytes - b.sizeBytes
      else if (sortKey === 'created') result = Date.parse(a.createdAt ?? '') - Date.parse(b.createdAt ?? '')
      else if (sortKey === 'modified') {
        result = Date.parse(a.updatedAt ?? a.createdAt ?? '') - Date.parse(b.updatedAt ?? b.createdAt ?? '')
      }
      if (Number.isNaN(result)) result = 0
      // Ties fall back to name, which ignores the direction — like Finder.
      if (result === 0) return collator.compare(a.name, b.name)
      return result * dir
    })
    return out
  }, [filteredFiles, sortKey, sortAsc, collator])

  const itemCount = sortedFolders.length + sortedFiles.length
  const ViewIcon = VIEW_OPTIONS.find((v) => v.value === view)?.icon ?? LayoutGrid
  const selectedFile = React.useMemo(
    () => sortedFiles.find((f) => `f${f.id}` === selected) ?? null,
    [sortedFiles, selected],
  )

  const activeFilterCount =
    (filters.extensions.length ? 1 : 0) + (filters.modified ? 1 : 0) + (filters.created ? 1 : 0)

  const toggleExtension = (ext: string) => setFilters((f) => ({
    ...f,
    extensions: f.extensions.includes(ext)
      ? f.extensions.filter((e) => e !== ext)
      : [...f.extensions, ext],
  }))

  const visibleTypeGroups = React.useMemo(() => {
    const q = typeQuery.trim().toLowerCase()
    if (!q) return TYPE_GROUPS
    return TYPE_GROUPS
      .map((g) => ({ ...g, types: g.types.filter((t) => t.label.toLowerCase().includes(q) || t.ext.includes(q)) }))
      .filter((g) => g.types.length > 0)
  }, [typeQuery])

  const toggleSortColumn = (key: FileBrowserSortKey) => {
    if (sortKey === key) setSortAsc((v) => !v)
    else { setSortKey(key); setSortAsc(true) }
  }

  const sortCaret = (key: FileBrowserSortKey) =>
    sortKey === key ? <span className="fb__caret">{sortAsc ? '▲' : '▼'}</span> : null

  // ── Drag & drop ──────────────────────────────────────────────────────
  // Documents are the draggable payload; folders and the breadcrumb are the
  // drop targets. `dropTarget` drives the highlight; 'root' means the
  // breadcrumb root, distinct from a folder id.
  const [dropTarget, setDropTarget] = React.useState<number | 'root' | null>(null)

  const dragProps = (file: BrowserFile) => (onMoveFile ? {
    draggable: true,
    onDragStart: (e: React.DragEvent) => {
      // Origin travels with the id: a document and a plain file can share an id,
      // and they move through different endpoints.
      e.dataTransfer.setData(DRAG_MIME, `${file.origin ?? 'document'}:${file.id}`)
      e.dataTransfer.effectAllowed = 'move'
    },
    onDragEnd: () => setDropTarget(null),
  } : {})

  const dropProps = (target: number | 'root') => (onMoveFile ? {
    onDragOver: (e: React.DragEvent) => {
      if (!e.dataTransfer.types.includes(DRAG_MIME)) return
      e.preventDefault()
      e.dataTransfer.dropEffect = 'move'
      setDropTarget(target)
    },
    // dragleave also fires when the pointer crosses into a child of the target,
    // which would flicker the highlight off; ignore those.
    onDragLeave: (e: React.DragEvent) => {
      if (e.currentTarget.contains(e.relatedTarget as Node | null)) return
      setDropTarget((current) => (current === target ? null : current))
    },
    onDrop: (e: React.DragEvent) => {
      e.preventDefault()
      const raw = e.dataTransfer.getData(DRAG_MIME)
      setDropTarget(null)
      const [origin, id] = raw.split(':')
      const row = files.find((f) => f.id === Number(id) && (f.origin ?? 'document') === origin)
      if (row) onMoveFile(row, target === 'root' ? null : target)
    },
  } : {})

  const isDrop = (target: number | 'root') => dropTarget === target

  /** Wraps an item in its right-click menu, when the page supplied one. */
  const withMenu = (node: React.ReactElement, menu: React.ReactNode) => {
    if (!menu) return node
    return (
      <ContextMenu key={node.key}>
        <ContextMenuTrigger asChild>
          {/* Radix still opens this item's menu (same-element handler), but the
              event stops here so the canvas menu behind it stays closed. */}
          {React.cloneElement(node as React.ReactElement<{ onContextMenu?: React.MouseEventHandler }>,
            { onContextMenu: (e: React.MouseEvent) => e.stopPropagation() })}
        </ContextMenuTrigger>
        <ContextMenuContent className="w-60">{menu}</ContextMenuContent>
      </ContextMenu>
    )
  }

  type MenuParts = {
    Item: React.ElementType
    Separator: React.ElementType
    Sub: React.ElementType
    SubTrigger: React.ElementType
    SubContent: React.ElementType
  }

  const sortMenuBody = (C: MenuParts) => (
    <>
      {SORT_OPTIONS.map((option) => (
        <C.Item key={option.value} onSelect={() => setSortKey(option.value)}>
          <Check size={14} className={cn('fb__tick', sortKey !== option.value && 'fb__tick--off')} />
          {option.label}
        </C.Item>
      ))}
      <C.Separator />
      <C.Item onSelect={() => setSortAsc((v) => !v)}>{sortAsc ? 'تصاعدي ▲' : 'تنازلي ▼'}</C.Item>
    </>
  )

  const filterMenuBody = (C: MenuParts) => (
    <>
      <C.Sub>
        <C.SubTrigger><FileText size={14} /> نوع الملف</C.SubTrigger>
        <C.SubContent className="w-64 p-0">
          <div className="fb__typesearch">
            <Search size={13} />
            <input value={typeQuery} onChange={(e) => setTypeQuery(e.target.value)}
              placeholder="ابحث في أنواع الملفات…" />
          </div>
          <div className="fb__typelist">
            {visibleTypeGroups.length === 0 && <p className="fb__typeempty">لا نتائج.</p>}
            {visibleTypeGroups.map((group) => (
              <div key={group.group}>
                <div className="fb__typegroup">{group.group}</div>
                {group.types.map((type) => (
                  <button key={type.ext} type="button" className="fb__typeitem"
                    onClick={() => toggleExtension(type.ext)}>
                    <Check size={13} className={cn('fb__tick',
                      !filters.extensions.includes(type.ext) && 'fb__tick--off')} />
                    <group.icon size={14} />
                    <span>{type.label}</span>
                  </button>
                ))}
              </div>
            ))}
          </div>
        </C.SubContent>
      </C.Sub>

      {(['modified', 'created'] as const).map((which) => (
        <C.Sub key={which}>
          <C.SubTrigger>
            <Calendar size={14} /> {which === 'modified' ? 'تاريخ التعديل' : 'تاريخ الإنشاء'}
          </C.SubTrigger>
          <C.SubContent>
            {DATE_PRESETS.map((preset) => (
              <C.Item key={preset.days}
                onSelect={() => setFilters((f) => ({ ...f, [which]: { days: preset.days } }))}>
                <Check size={13} className={cn('fb__tick',
                  filters[which]?.days !== preset.days && 'fb__tick--off')} />
                {preset.label}
              </C.Item>
            ))}
            <C.Separator />
            <C.Item onSelect={() => setRangeFor(which)}>نطاق تاريخ مخصّص…</C.Item>
          </C.SubContent>
        </C.Sub>
      ))}

      {activeFilterCount > 0 && (
        <>
          <C.Separator />
          <C.Item onSelect={() => setFilters(EMPTY_FILTERS)}>مسح كل عوامل التصفية</C.Item>
        </>
      )}
    </>
  )

  const DROPDOWN_PARTS: MenuParts = {
    Item: DropdownMenuItem, Separator: DropdownMenuSeparator,
    Sub: DropdownMenuSub, SubTrigger: DropdownMenuSubTrigger, SubContent: DropdownMenuSubContent,
  }
  const CONTEXT_PARTS: MenuParts = {
    Item: ContextMenuItem, Separator: ContextMenuSeparator,
    Sub: ContextMenuSub, SubTrigger: ContextMenuSubTrigger, SubContent: ContextMenuSubContent,
  }

  /**
   * A file's artwork. An image document shows the picture it actually is;
   * everything else keeps the format card, which says more about a PDF than a
   * first-page render ever would at this size.
   */
  const fileArt = (file: BrowserFile) => (file.previewSrc ? (
    <img src={file.previewSrc} alt="" className="fb__thumb" draggable={false} loading="lazy" />
  ) : (
    <FileCard formatFile={fileFormatOf(file.extension)} hideBadge={!file.extension}
      badgeLabel={fileBadgeOf(file.extension) ?? undefined} />
  ))

  /** Create actions, aimed at whichever folder the menu was opened on. */
  const createItems = (parentId: number | null) => (
    <>
      {onCreateFolder && (
        <ContextMenuItem onSelect={() => openCreateFolder(parentId)}>
          <FolderPlus className="ml-2 h-4 w-4 opacity-60" />
          مجلد جديد
        </ContextMenuItem>
      )}
      {onCreateFile && (
        <ContextMenuItem onSelect={() => onCreateFile(parentId)}>
          <FilePlus2 className="ml-2 h-4 w-4 opacity-60" />
          وثيقة جديدة
        </ContextMenuItem>
      )}
      {onUploadFiles && (
        <ContextMenuItem onSelect={() => pickFiles(parentId)}>
          <Upload className="ml-2 h-4 w-4 opacity-60" />
          رفع ملف…
        </ContextMenuItem>
      )}
      {onUploadDocuments && (
        <ContextMenuItem onSelect={() => pickFiles(parentId, 'document')}>
          <FilePlus2 className="ml-2 h-4 w-4 opacity-60" />
          رفع وتسجيله كوثيقة…
        </ContextMenuItem>
      )}
    </>
  )

  /** The tree pane's own menu — right-clicking its empty space acts on the root. */
  const rootTreeMenu = (
    <>
      <ContextMenuLabel>{rootLabel}</ContextMenuLabel>
      <ContextMenuSeparator />
      <ContextMenuItem onSelect={() => navigate(null)}>
        <FolderOpen className="ml-2 h-4 w-4 opacity-60" />
        فتح الجذر
      </ContextMenuItem>
      {createItems(null)}
    </>
  )

  /** The tree draws the same coloured, badged folder the grid does — one folder,
   *  one look, wherever it appears. */
  const treeIcon = (node: FilesystemNode) => {
    if (node.id === 'trash') return <Trash2 className="fb__treetrash" size={18} />
    const folder = node.id === 'root' ? undefined : folders.find((f) => f.id === node.id)
    return <FolderGlyph className="fb__treefolder" color={folder?.color} icon={folder?.icon} />
  }

  const emptyTrashItem = trash && (
    <ContextMenuItem className="text-destructive focus:text-destructive"
      disabled={trash.count === 0} onSelect={() => trash.onEmpty()}>
      <Trash2 className="ml-2 h-4 w-4 opacity-60" />
      إفراغ سلة المحذوفات
    </ContextMenuItem>
  )

  /**
   * A tree row's menu. Folder rows reuse the page's folder menu verbatim, so a
   * folder offers the same actions here as it does in the grid — one menu to
   * learn, not two that drift apart.
   */
  const treeMenu = (node: FilesystemNode) => {
    if (node.id === 'trash') return (
      <>
        <ContextMenuLabel>سلة المحذوفات</ContextMenuLabel>
        <ContextMenuSeparator />
        <ContextMenuItem onSelect={() => trash?.onOpen()}>
          <FolderOpen className="ml-2 h-4 w-4 opacity-60" />
          فتح السلة
        </ContextMenuItem>
        {emptyTrashItem}
      </>
    )
    if (node.id === 'root') return rootTreeMenu
    const folder = folders.find((f) => f.id === node.id)
    if (!folder) return null
    return renderFolderMenu?.(folder, {
      edit: () => openEditFolder(folder),
      upload: () => pickFiles(folder.id),
    })
  }

  const fileTile = (file: BrowserFile) => withMenu(
    <button
      key={`f${file.id}`}
      type="button"
      className={cn('fb__tile', selected === `f${file.id}` && 'fb__tile--on')}
      onClick={() => setSelected(`f${file.id}`)}
      onDoubleClick={() => onOpenFile(file)}
      title={file.name}
      {...dragProps(file)}
    >
      <span className="fb__tileart">
        {fileArt(file)}
        {file.attachmentCount > 1 && <span className="fb__count">{file.attachmentCount}</span>}
      </span>
      <span className="fb__tilename">{file.name}</span>
    </button>,
    renderFileMenu?.(file),
  )

  const folderTile = (folder: BrowserFolder) => withMenu(
    <button
      key={`d${folder.id}`}
      type="button"
      className={cn('fb__tile', selected === `d${folder.id}` && 'fb__tile--on',
        isDrop(folder.id) && 'fb__tile--drop')}
      onClick={() => setSelected(`d${folder.id}`)}
      onDoubleClick={() => navigate(folder.id)}
      title={`${folder.name} · ${folder.documentCount}`}
      {...dropProps(folder.id)}
    >
      <span className="fb__tileart">
        <FolderGlyph className="fb__folder" color={folder.color} icon={folder.icon} />
        {/* Borrowed, not yours. Marked on the art itself so it survives every view. */}
        {folder.shared && (
          <span className="fb__lent" title={folder.ownerName ? `مشترَك من ${folder.ownerName}` : 'مشترَك معك'}>
            <Users size={11} aria-hidden />
          </span>
        )}
      </span>
      <span className="fb__tilename fb__tilename--folder">{folder.name}</span>
    </button>,
    renderFolderMenu?.(folder, { edit: () => openEditFolder(folder), upload: () => pickFiles(folder.id) }),
  )

  /** Preview + metadata pane, shared by the columns and gallery views. */
  const infoPane = selectedFile ? (
    <div className="fb__info">
      <div className="fb__infohead">
        <span className="fb__infoart">
          {fileArt(selectedFile)}
        </span>
        <div>
          <div className="fb__infoname">{selectedFile.name}</div>
          <div className="fb__infokind">
            {selectedFile.kind ?? 'وثيقة'}{selectedFile.sizeBytes ? ` · ${formatBytes(selectedFile.sizeBytes)}` : ''}
          </div>
        </div>
      </div>
      <div className="fb__infosec">معلومات</div>
      <dl className="fb__infolist">
        <div><dt>رقم الوثيقة</dt><dd className="mono">{selectedFile.caption ?? '—'}</dd></div>
        <div><dt>أُنشئت</dt><dd>{formatStamp(selectedFile.createdAt)}</dd></div>
        <div><dt>عُدّلت</dt><dd>{formatStamp(selectedFile.updatedAt ?? selectedFile.createdAt)}</dd></div>
        <div><dt>الحجم</dt><dd>{formatBytes(selectedFile.sizeBytes)}</dd></div>
        <div><dt>المرفقات</dt><dd>{selectedFile.attachmentCount}</dd></div>
      </dl>
    </div>
  ) : (
    <div className="fb__info fb__info--empty">اختر عنصرًا لعرض تفاصيله.</div>
  )

  return (
    <div className={cn('fb', className)}>
      <div className="fb__bar">
        <div className="fb__nav">
          <button type="button" className="fb__iconbtn" disabled={!canBack} onClick={() => step(-1)} aria-label="رجوع">
            <ChevronRight size={16} className="fb__chev--back" />
          </button>
          <button type="button" className="fb__iconbtn" disabled={!canForward} onClick={() => step(1)} aria-label="تقدّم">
            <ChevronLeft size={16} className="fb__chev--fwd" />
          </button>
        </div>

        <nav className="fb__crumbs" aria-label="المسار">
          {trash?.active ? (
            <>
              <Trash2 size={14} className="fb__crumbicon" />
              <span className="fb__crumb">سلة المحذوفات</span>
            </>
          ) : (
          <>
          {/* Each crumb wears its own folder. The path used to carry one blue glyph at the root
              and bare text after it, so walking into a red folder still showed blue — the colour
              a folder is chosen by has to follow it wherever the folder is named. */}
          <button type="button" className={cn('fb__crumb', isDrop('root') && 'fb__crumb--drop')}
            onClick={() => navigate(null)} disabled={currentFolderId === null} {...dropProps('root')}>
            <FolderGlyph className="fb__crumbicon" />
            <span className="fb__crumbtext">{rootLabel}</span>
          </button>
          {trail.map((folder, i) => (
            <React.Fragment key={folder.id}>
              <ChevronLeft size={13} className="fb__crumbsep" aria-hidden />
              <button type="button" className={cn('fb__crumb', isDrop(folder.id) && 'fb__crumb--drop')}
                disabled={i === trail.length - 1}
                onClick={() => navigate(folder.id)} {...dropProps(folder.id)}>
                <FolderGlyph className="fb__crumbicon" color={folder.color} icon={folder.icon} />
                <span className="fb__crumbtext">{folder.name}</span>
              </button>
            </React.Fragment>
          ))}
          </>
          )}
        </nav>

        <div className="fb__tools">
          {/* View */}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <button type="button" className="fb__toolbtn" aria-label="طريقة العرض">
                <ViewIcon size={14} />
                <ChevronDownGlyph />
              </button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start">
              {VIEW_OPTIONS.map((option) => (
                <DropdownMenuItem key={option.value} onSelect={() => setView(option.value)}>
                  <Check size={14} className={cn('fb__tick', view !== option.value && 'fb__tick--off')} />
                  <option.icon size={14} />
                  {option.label}
                </DropdownMenuItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>

          {/* Sort */}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <button type="button" className="fb__toolbtn" aria-label="الترتيب">
                <ArrowUpDown size={14} />
                <ChevronDownGlyph />
              </button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start">
              {sortMenuBody(DROPDOWN_PARTS)}
            </DropdownMenuContent>
          </DropdownMenu>

          {/* Filters */}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <button type="button" className={cn('fb__toolbtn', activeFilterCount > 0 && 'fb__toolbtn--on')}
                aria-label="عوامل التصفية">
                <Filter size={14} />
                {activeFilterCount > 0 && <span className="fb__filtcount">{activeFilterCount}</span>}
              </button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" className="min-w-56">
              {filterMenuBody(DROPDOWN_PARTS)}
            </DropdownMenuContent>
          </DropdownMenu>

          <div className="fb__search">
            <Search size={14} className="fb__searchicon" />
            <input value={search} onChange={(e) => onSearchChange(e.target.value)}
              placeholder="بحث" aria-label="بحث في الوثائق" />
          </div>

          {toolbarExtra && (
            <>
              <span className="fb__bardiv" aria-hidden />
              {toolbarExtra}
            </>
          )}
        </div>
      </div>

      {activeFilterCount > 0 && (
        <div className="fb__chips">
          {filters.extensions.length > 0 && (
            <span className="fb__chip">
              النوع: {filters.extensions.map((e) => e.toUpperCase()).join('، ')}
              <button type="button" onClick={() => setFilters((f) => ({ ...f, extensions: [] }))} aria-label="إزالة">
                <X size={12} />
              </button>
            </span>
          )}
          {(['modified', 'created'] as const).map((which) => filters[which] && (
            <span className="fb__chip" key={which}>
              {which === 'modified' ? 'عُدّلت' : 'أُنشئت'}: {dateFilterLabel(filters[which]!)}
              <button type="button" onClick={() => setFilters((f) => ({ ...f, [which]: null }))} aria-label="إزالة">
                <X size={12} />
              </button>
            </span>
          ))}
        </div>
      )}

      <div className="fb__main">
      {folderTree && (
        <>
          <ContextMenu>
            <ContextMenuTrigger asChild>
              <aside ref={sideRef} className="fb__side" aria-label="شجرة المجلدات"
                style={{ width: sideWidth }}>
                <ul>
                  {treeNodes.map((node) => (
                    <FilesystemItem
                      key={node.id}
                      node={node}
                      animated
                      selectedId={trash?.active ? 'trash' : (currentFolderId ?? 'root')}
                      onSelect={(picked) => {
                        if (picked.id === 'trash') trash?.onOpen()
                        else navigate(picked.id === 'root' ? null : Number(picked.id))
                      }}
                      renderMenu={treeMenu}
                      renderIcon={treeIcon}
                    />
                  ))}
                </ul>
              </aside>
            </ContextMenuTrigger>
            <ContextMenuContent className="w-56">{rootTreeMenu}</ContextMenuContent>
          </ContextMenu>

          {/* Separator and drag handle in one: it is the visible edge of the pane. */}
          <div
            className="fb__resizer"
            role="separator"
            aria-orientation="vertical"
            aria-label="عرض شجرة المجلدات"
            onPointerDown={startResize}
            onDoubleClick={() => setSideWidth(224)}
          />
        </>
      )}

      <ContextMenu>
        <ContextMenuTrigger asChild>
          <div className="fb__canvas">
      {loading ? (
        <div className="fb__body"><p className="fb__empty">جارٍ التحميل…</p></div>
      ) : itemCount === 0 ? (
        <div className="fb__body"><p className="fb__empty">لا توجد عناصر مطابقة.</p></div>
      ) : view === 'grid' ? (
        <div className="fb__body fb__body--grid">
          {sortedFolders.map(folderTile)}
          {sortedFiles.map(fileTile)}
        </div>
      ) : view === 'list' ? (
        <div className="fb__body fb__body--list">
          <div className="fb__lhead">
            <button type="button" onClick={() => toggleSortColumn('name')}>الاسم {sortCaret('name')}</button>
            <button type="button" onClick={() => toggleSortColumn('modified')}>تاريخ التعديل {sortCaret('modified')}</button>
            <button type="button" onClick={() => toggleSortColumn('size')}>الحجم {sortCaret('size')}</button>
          </div>
          {sortedFolders.map((folder) => withMenu(
            <button key={`d${folder.id}`} type="button"
              className={cn('fb__lrow', selected === `d${folder.id}` && 'fb__lrow--on',
                isDrop(folder.id) && 'fb__lrow--drop')}
              onClick={() => setSelected(`d${folder.id}`)} onDoubleClick={() => navigate(folder.id)}
              {...dropProps(folder.id)}>
              <span className="fb__lname fb__lname--folder"><FolderGlyph className="fb__lart" color={folder.color} icon={folder.icon} />{folder.name}</span>
              <span>—</span>
              <span>{folder.documentCount} وثيقة</span>
            </button>,
            renderFolderMenu?.(folder, { edit: () => openEditFolder(folder), upload: () => pickFiles(folder.id) }),
          ))}
          {sortedFiles.map((file) => withMenu(
            <button key={`f${file.id}`} type="button"
              className={cn('fb__lrow', selected === `f${file.id}` && 'fb__lrow--on')}
              onClick={() => setSelected(`f${file.id}`)} onDoubleClick={() => onOpenFile(file)}
              {...dragProps(file)}>
              <span className="fb__lname">
                <span className="fb__lart fb__lart--card">
                  {fileArt(file)}
                </span>
                {file.name}
              </span>
              <span>{formatStamp(file.updatedAt ?? file.createdAt)}</span>
              <span>{formatBytes(file.sizeBytes)}</span>
            </button>,
            renderFileMenu?.(file),
          ))}
        </div>
      ) : view === 'columns' ? (
        <div className="fb__body fb__body--columns">
          <div className="fb__col">
            {sortedFolders.map((folder) => withMenu(
              <button key={`d${folder.id}`} type="button"
                className={cn('fb__colrow', selected === `d${folder.id}` && 'fb__colrow--on',
                  isDrop(folder.id) && 'fb__colrow--drop')}
                onClick={() => setSelected(`d${folder.id}`)} onDoubleClick={() => navigate(folder.id)}
                {...dropProps(folder.id)}>
                <FolderGlyph className="fb__lart" color={folder.color} icon={folder.icon} />
                <span className="fb__colname--folder">{folder.name}</span>
                <ChevronLeft size={13} className="fb__colchev" />
              </button>,
              renderFolderMenu?.(folder, { edit: () => openEditFolder(folder), upload: () => pickFiles(folder.id) }),
            ))}
            {sortedFiles.map((file) => withMenu(
              <button key={`f${file.id}`} type="button"
                className={cn('fb__colrow', selected === `f${file.id}` && 'fb__colrow--on')}
                onClick={() => setSelected(`f${file.id}`)} onDoubleClick={() => onOpenFile(file)}
                {...dragProps(file)}>
                <span className="fb__lart fb__lart--card">
                  {fileArt(file)}
                </span>
                <span>{file.name}</span>
              </button>,
              renderFileMenu?.(file),
            ))}
          </div>
          {infoPane}
        </div>
      ) : (
        <div className="fb__body fb__body--gallery">
          <div className="fb__stage">
            {selectedFile ? (
              <div className="fb__stagecard">
                {fileArt(selectedFile)}
              </div>
            ) : (
              <p className="fb__empty">اختر عنصرًا من الشريط أدناه.</p>
            )}
          </div>
          {infoPane}
          <div className="fb__strip">
            {sortedFolders.map((folder) => withMenu(
              <button key={`d${folder.id}`} type="button"
                className={cn('fb__stripitem', 'fb__stripitem--folder',
                  selected === `d${folder.id}` && 'fb__stripitem--on',
                  isDrop(folder.id) && 'fb__stripitem--drop')}
                onClick={() => setSelected(`d${folder.id}`)} onDoubleClick={() => navigate(folder.id)}
                title={folder.name} {...dropProps(folder.id)}>
                <FolderGlyph className="fb__stripfolder" color={folder.color} icon={folder.icon} />
                {/* The strip is otherwise glyph-only, and one blue folder looks like
                    any other — the name is what tells them apart. */}
                <span className="fb__stripname">{folder.name}</span>
              </button>,
              renderFolderMenu?.(folder, { edit: () => openEditFolder(folder), upload: () => pickFiles(folder.id) }),
            ))}
            {sortedFiles.map((file) => withMenu(
              <button key={`f${file.id}`} type="button"
                className={cn('fb__stripitem', selected === `f${file.id}` && 'fb__stripitem--on')}
                onClick={() => setSelected(`f${file.id}`)} onDoubleClick={() => onOpenFile(file)}
                title={file.name} {...dragProps(file)}>
                <span className="fb__stripcard">
                  {fileArt(file)}
                </span>
              </button>,
              renderFileMenu?.(file),
            ))}
          </div>
        </div>
      )}

          </div>
        </ContextMenuTrigger>
        <ContextMenuContent className="w-56">
          {trash?.active && (
            <>
              {emptyTrashItem}
              <ContextMenuSeparator />
            </>
          )}
          {!trash?.active && onCreateFolder && (
            <ContextMenuItem onSelect={() => openCreateFolder(currentFolderId)}>
              <FolderPlus className="ml-2 h-4 w-4 opacity-60" />
              مجلد جديد
            </ContextMenuItem>
          )}
          {!trash?.active && onCreateFile && (
            <ContextMenuItem onSelect={() => onCreateFile(currentFolderId)}>
              <FilePlus2 className="ml-2 h-4 w-4 opacity-60" />
              وثيقة جديدة
            </ContextMenuItem>
          )}
          {!trash?.active && onUploadFiles && (
            <ContextMenuItem onSelect={() => pickFiles(currentFolderId)}>
              <Upload className="ml-2 h-4 w-4 opacity-60" />
              رفع ملف…
            </ContextMenuItem>
          )}
          {!trash?.active && onUploadDocuments && (
            <ContextMenuItem onSelect={() => pickFiles(currentFolderId, 'document')}>
              <FilePlus2 className="ml-2 h-4 w-4 opacity-60" />
              رفع وتسجيله كوثيقة…
            </ContextMenuItem>
          )}
          {!trash?.active && (onCreateFolder || onCreateFile || onUploadFiles || onUploadDocuments) && <ContextMenuSeparator />}
          <ContextMenuSub>
            <ContextMenuSubTrigger>
              <ArrowUpDown className="ml-2 h-4 w-4 opacity-60" />
              ترتيب حسب
            </ContextMenuSubTrigger>
            <ContextMenuSubContent>{sortMenuBody(CONTEXT_PARTS)}</ContextMenuSubContent>
          </ContextMenuSub>
          <ContextMenuSub>
            <ContextMenuSubTrigger>
              <Filter className="ml-2 h-4 w-4 opacity-60" />
              تصفية
            </ContextMenuSubTrigger>
            <ContextMenuSubContent className="min-w-56">{filterMenuBody(CONTEXT_PARTS)}</ContextMenuSubContent>
          </ContextMenuSub>
        </ContextMenuContent>
      </ContextMenu>
      </div>

      {/* Kept out of the menus themselves: a <ContextMenuItem> unmounts on select,
          taking any input inside it with it before the change event can fire. */}
      <input
        ref={fileInput}
        type="file"
        multiple
        hidden
        onChange={(e) => {
          const chosen = Array.from(e.target.files ?? [])
          e.target.value = ''            // same file twice in a row must still fire
          if (!chosen.length) return
          const target = pickReq?.parent ?? null
          if (pickReq?.as === 'document') onUploadDocuments?.(target, chosen)
          else onUploadFiles?.(target, chosen)
        }}
      />

      <div className="fb__foot">
        {itemCount} عنصرًا
        {selectedFile && <> · «{selectedFile.name}» محدد</>}
      </div>

      <FolderDialog
        key={folderDlg === null ? 'closed'
          : folderDlg.mode === 'edit' ? `e${folderDlg.id}` : `c${folderDlg.parent ?? 'root'}`}
        open={folderDlg !== null}
        mode={folderDlg?.mode ?? 'create'}
        initial={folderDlg?.mode === 'edit' ? folderDlg.initial : undefined}
        onOpenChange={(open) => { if (!open) setFolderDlg(null) }}
        onSubmit={submitFolder}
      />

      <DateRangeDialog
        key={rangeFor ?? 'closed'}
        open={rangeFor !== null}
        onOpenChange={(open) => { if (!open) setRangeFor(null) }}
        onApply={(from, to) => {
          if (rangeFor) setFilters((f) => ({ ...f, [rangeFor]: { days: null, from, to } }))
        }}
      />
    </div>
  )
}

/** Small caret matching the reference toolbar's split buttons. */
function ChevronDownGlyph() {
  return <span className="fb__caretdown" aria-hidden>▾</span>
}

export default FileBrowser
