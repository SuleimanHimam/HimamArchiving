import { useEffect, useMemo, useState, useRef, useCallback } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { AxiosError } from 'axios'
import {
  Bold, Italic, Underline, Strikethrough, AlignRight, AlignCenter, AlignLeft, AlignJustify,
  List, ListOrdered, Undo2, Redo2, RemoveFormatting, Type, Superscript, Subscript,
  IndentIncrease, IndentDecrease, Table as TableIcon, Image as ImageIcon, Link as LinkIcon,
  Quote, Minus, Baseline, Copy, Scissors, ClipboardPaste, Paintbrush, Calendar, Sigma,
  Droplet, Trash2, ZoomIn, ZoomOut, PanelRight, Highlighter, X, PenLine, Stamp, Printer, FileUp,
  ChevronRight, ChevronLeft, Check, WrapText, PaintBucket, Square, SquareDashed,
  Save, FilePlus, GripVertical, Palette,
} from 'lucide-react'
import { auth } from '../../lib/auth'
import {
  reportTemplates, printLayout, parseLayout, renderReportHtml, sanitizeRichHtml, watermarkSvg,
  closePreviewWindow,
  EMPTY_RICH_HTML, tokenChip, TOKENS, TOKEN_LABEL, SCOPES, TARGETS, IMAGE_TOKENS,
  type ReportTemplate, type TemplateScope, type TemplateTarget, type RenderCtx, type Watermark,
  type PageBorder,
} from '../../lib/reportTemplates'
import { organization, type OrgUnitDto, type UserLookupDto } from '../../lib/organization'
import { documents, type DocumentTypeDto } from '../../lib/documents'
import { diwan, type MailCategory } from '../../lib/diwan'
import { account, type UserSeal } from '../../lib/account'
import { brandingApi } from '../../lib/branding'
import { importDocx, DocxImportError } from '../../lib/docxImport'
import { useToast } from '../../components/toast'
import { useConfirmDialog } from '../../components/ui/confirm-dialog'
import { LabeledInput, LabeledSelect } from '../../components/ui/labeled-input'
import WatermarkDialog, { type WmDraft } from './WatermarkDialog'
import BordersDialog from './BordersDialog'
import ElementFormatPanel from './ElementFormatPanel'
import { readFormat, type ElementFormat } from './elementFormat'
import {
  DEFAULT_BORDERS, draftToPageBorder, draftToBlockCss, pageBorderToDraft,
  type BordersDraft, type BordersTab,
} from './borders'
import '../documents/documents.css'
import '../settings/settings.css'
import './report-designer.css'

const errOf = (e: unknown, f: string) => (e as AxiosError<{ error?: string }>).response?.data?.error ?? f

const TRANSPARENT_PX = 'data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs='
const FONTS = ['Segoe UI', 'Tahoma', 'Arial', 'Times New Roman', 'Amiri', 'Reem Kufi', 'IBM Plex Sans Arabic']
const SIZES: { v: string; label: string }[] = [
  { v: '2', label: '10' }, { v: '3', label: '12' }, { v: '4', label: '14' },
  { v: '5', label: '18' }, { v: '6', label: '24' }, { v: '7', label: '36' },
]
const LINE_SPACINGS = ['1', '1.15', '1.5', '2', '2.5', '3']
const ZOOMS = [0.5, 0.75, 1, 1.25, 1.5]
const SYMBOLS = ['٪', '﷼', '©', '®', '™', '§', '•', '—']
// A4 / Letter portrait pixel sizes (~96dpi); landscape swaps them.
const PAGE_DIMS: Record<string, [number, number]> = { A4: [794, 1123], Letter: [816, 1056] }
/* Sheet geometry, in the same CSS pixels the canvas is laid out in. PAD_* must match the
   `.rd-page` padding in report-designer.css — the editable's padding is the page margin. */
const PAD_TOP = 48
const PAD_BOTTOM = 48
const SHEET_GAP = 28   // the grey channel between two sheets, as in Word's print layout

const FIELD_GROUPS: { title: string; tokens: string[] }[] = [
  { title: 'عام', tokens: ['institutionName', 'number', 'subject', 'date', 'sentDate', 'confidentiality', 'priority', 'classification', 'raisedTo', 'fileReference', 'keywords'] },
  { title: 'المرسِل والمستلم', tokens: ['recipient', 'recipientName', 'sender', 'senderName'] },
]

const SAMPLE_TOKENS: Record<string, string> = {
  institutionName: 'المؤسسة', number: 'OUT-2026-0001', subject: 'نموذج معاينة الترويسة',
  date: '2026/07/17', sentDate: '2026/07/17', recipient: 'وزارة المالية', recipientName: 'مدير عام',
  sender: 'الإدارة العامة', senderName: 'أمين السر', confidentiality: 'داخلي', priority: 'عادي',
  classification: 'للتوقيع', raisedTo: 'المدير العام', fileReference: 'صندوق رقم 402', keywords: 'مالية، تعميم',
  body: 'هذا نصٌّ تجريبي لمعاينة تصميم المحتوى داخل الترويسة.',
}
const SAMPLE_BODY_HTML = '<p>هذا نصٌّ تجريبي لمعاينة تصميم المحتوى داخل الترويسة. سيُستبدل بالمحتوى الفعلي للكتاب عند الطباعة.</p><p>سطر ثانٍ لبيان تباعد الفقرات.</p>'
const SAMPLE_ROWS = [{ name: 'مرفق-1.pdf', size: '250 KB' }, { name: 'صورة.png', size: '120 KB' }]

/** How the text behaves around a picture — Word's «التفاف النص». */
type WrapMode = 'inline' | 'right' | 'left' | 'block' | 'behind' | 'front'
const WRAP_MODES: { k: WrapMode; label: string }[] = [
  { k: 'inline', label: 'محاذاة مع النص (سطري)' },
  { k: 'right', label: 'التفاف — الصورة إلى اليمين' },
  { k: 'left', label: 'التفاف — الصورة إلى اليسار' },
  { k: 'block', label: 'أعلى وأسفل (سطر مستقل)' },
  { k: 'behind', label: 'خلف النص' },
  { k: 'front', label: 'أمام النص' },
]

type WmType = 'none' | 'text' | 'image'
type Tab = 'home' | 'insert' | 'layout' | 'view'
const TABS: { id: Tab; label: string }[] = [
  { id: 'home', label: 'بداية' }, { id: 'insert', label: 'إدراج' },
  { id: 'layout', label: 'تخطيط الصفحة' }, { id: 'view', label: 'عرض' },
]

// Human label for a removable element (shared by the items list and the context menu).
function nodeLabel(node: Element): string {
  const tk = node.getAttribute('data-token')
  if (tk) return tk === 'body' ? 'المحتوى (نص الكتاب)' : tk === 'attachments' ? 'جدول المرفقات'
    : (IMAGE_TOKENS[tk] ?? TOKEN_LABEL[tk] ?? tk)
  return node.tagName === 'TABLE' ? 'جدول' : node.tagName === 'IMG' ? 'صورة' : node.tagName === 'HR' ? 'خط فاصل'
    : node.classList.contains('rd-pagebreak') ? 'فاصل صفحات' : 'عنصر'
}
const ITEM_SELECTOR = '[data-token], table, img:not([data-token]), .rd-pagebreak, hr'

export default function ReportDesignerPage() {
  const toast = useToast()
  const { confirm } = useConfirmDialog()
  const [searchParams] = useSearchParams()
  // Department- and institution-wide designs need the admin right; a personal one never does.
  const canManageOrg = auth.hasPermission('PrintDesign.Edit') || auth.hasPermission('Organization.Edit')
  const canPrint = auth.hasPermission('Correspondence.Print') || auth.hasPermission('Documents.Print')
  const myUserId = auth.getUser()?.id ?? null

  /* Arriving from a letter (زر «تصميم الطباعة» في البريد الصادر) the designer opens already scoped
     to what that letter would print with: target=المراسلات and its own تصنيف البريد. `templateId`
     jumps straight to editing a saved design. */
  const numParam = (k: string) => { const v = searchParams.get(k); return v && /^\d+$/.test(v) ? Number(v) : null }
  const linkTarget = numParam('target')
  const linkCategoryId = numParam('mailCategoryId')
  const linkDocTypeId = numParam('documentTypeId')
  const linkTemplateId = numParam('templateId')
  // The gallery passes the department it was filtered to, so a new design starts where the user
  // was looking instead of at the institution default.
  const linkScope = numParam('scope')
  const linkOrgUnitId = numParam('orgUnitId')

  const [templates, setTemplates] = useState<ReportTemplate[]>([])
  const [orgUnits, setOrgUnits] = useState<OrgUnitDto[]>([])
  const [users, setUsers] = useState<UserLookupDto[]>([])
  const [instName, setInstName] = useState('المؤسسة')

  const [editId, setEditId] = useState<number | null>(null)
  const [name, setName] = useState('تصميم جديد')
  const [scope, setScope] = useState<TemplateScope>(
    (canManageOrg ? (linkScope ?? 0) : 2) as TemplateScope)
  const [orgUnitId, setOrgUnitId] = useState<number | ''>(linkOrgUnitId ?? '')
  const [userId, setUserId] = useState<number | ''>(myUserId ?? '')
  const [target, setTarget] = useState<TemplateTarget>((linkTarget ?? 2) as TemplateTarget)
  // Optional narrowing: which document type / mail category this design is for. '' = all of them.
  const [docTypeId, setDocTypeId] = useState<number | ''>(linkDocTypeId ?? '')
  const [mailCategoryId, setMailCategoryId] = useState<number | ''>(linkCategoryId ?? '')
  const [mySeal, setMySeal] = useState<UserSeal | null>(null)
  const [docTypes, setDocTypes] = useState<DocumentTypeDto[]>([])
  const [mailCategories, setMailCategories] = useState<MailCategory[]>([])
  const [isActive, setIsActive] = useState(true)
  const [logo, setLogo] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  // Ribbon / view state
  const [tab, setTab] = useState<Tab>('home')
  const [curSize, setCurSize] = useState('3')
  const [zoom, setZoom] = useState(1)
  const [pageSize, setPageSize] = useState<'A4' | 'Letter'>('A4')
  const [orientation, setOrientation] = useState<'portrait' | 'landscape'>('portrait')
  const [showFields, setShowFields] = useState(true)
  const [highlight, setHighlight] = useState(false)

  // Watermark
  const [wmType, setWmType] = useState<WmType>('none')
  const [wmText, setWmText] = useState('مسودة')
  const [wmImage, setWmImage] = useState<string | null>(null)
  const [wmColor, setWmColor] = useState('#9ca3af')
  const [wmOpacity, setWmOpacity] = useState(0.15)
  const [wmSize, setWmSize] = useState(72)
  const [wmRotate, setWmRotate] = useState(-45)
  const [wmFont, setWmFont] = useState('Segoe UI')
  const [wmDialogOpen, setWmDialogOpen] = useState(false)

  // Page background (لون الصفحة) and page frame (حدود الصفحة) — both travel with the design.
  const [pageBg, setPageBg] = useState<string | null>(null)
  const [pageBorder, setPageBorder] = useState<PageBorder | null>(null)
  const [bordersDraft, setBordersDraft] = useState<BordersDraft>(DEFAULT_BORDERS)
  const [bordersTab, setBordersTab] = useState<BordersTab>('borders')
  const [bordersOpen, setBordersOpen] = useState(false)

  // The design is now chosen in the gallery (/app/print-design), which arrives here with an
  // explicit mode. The old dialog only opens when this page is reached without one — a stale
  // bookmark or a direct URL — so the choice is never made twice.
  const [metaDialogOpen, setMetaDialogOpen] = useState(
    () => searchParams.get('mode') === null && linkTemplateId == null)
  const [designMode, setDesignMode] = useState<'new' | 'edit'>(
    linkTemplateId != null || searchParams.get('mode') === 'edit' ? 'edit' : 'new')

  const editorRef = useRef<HTMLDivElement>(null)
  // `setEditorHtml` is defined above the paginator but must trigger it; the ref breaks the cycle.
  const paginateRef = useRef<(() => void) | null>(null)
  const savedRange = useRef<Range | null>(null)
  const imgInputRef = useRef<HTMLInputElement>(null)
  const logoInputRef = useRef<HTMLInputElement>(null)
  const docxInputRef = useRef<HTMLInputElement>(null)
  const painterStyle = useRef<string | null>(null)
  const [painter, setPainter] = useState(false)
  const [tablePicker, setTablePicker] = useState<{ open: boolean; x: number; y: number; r: number; c: number }>({ open: false, x: 0, y: 0, r: 0, c: 0 })
  /* The picture the resize handles belong to. Declared here, above the content writers, so
     replacing the canvas can drop a selection whose image no longer exists. */
  const selImgRef = useRef<HTMLImageElement | null>(null)
  const [imgBox, setImgBox] = useState<{ left: number; top: number; w: number; h: number } | null>(null)
  /** The selected element when it is not a picture — a chip, a placeholder, a table, a rule. */
  const selElRef = useRef<HTMLElement | null>(null)
  const [elBox, setElBox] = useState<
    { left: number; top: number; w: number; h: number; label: string; atomic: boolean } | null>(null)

  // ── Editor content is uncontrolled (contentEditable); read/write innerHTML imperatively. ──
  const refreshLogoImgs = useCallback(() => {
    editorRef.current?.querySelectorAll<HTMLImageElement>('img[data-token="logo"]').forEach((img) => {
      img.src = logo || TRANSPARENT_PX
      img.classList.toggle('rd-logo--empty', !logo)
    })
  }, [logo])

  // Tag every removable element with a stable id so the context menu can target it.
  const scanItems = useCallback(() => {
    editorRef.current?.querySelectorAll<HTMLElement>(ITEM_SELECTOR).forEach((node, i) => {
      if (!node.getAttribute('data-rid')) node.setAttribute('data-rid', `r${Date.now().toString(36)}${i}`)
    })
  }, [])

  const setEditorHtml = useCallback((html: string) => {
    if (editorRef.current) editorRef.current.innerHTML = sanitizeRichHtml(html, { allowTokens: true })
    selImgRef.current = null; setImgBox(null)
    selElRef.current = null; setElBox(null)
    refreshLogoImgs(); scanItems()
    // Content just changed wholesale (load, import, reset) — re-lay it over the sheets.
    requestAnimationFrame(() => paginateRef.current?.())
  }, [refreshLogoImgs, scanItems])

  const getEditorHtml = () => editorRef.current?.innerHTML ?? ''

  const removeItem = (rid: string) => {
    editorRef.current?.querySelector(`[data-rid="${rid}"]`)?.remove()
    // Deleting the selected element must take its outline and handles with it.
    if (!selImgRef.current?.isConnected) { selImgRef.current = null; setImgBox(null) }
    if (!selElRef.current?.isConnected) { selElRef.current = null; setElBox(null) }
    scanItems()
  }

  // ── Right-click context menu ──
  const [menu, setMenu] = useState<{ open: boolean; x: number; y: number; rid: string | null; label: string | null; inTable: boolean; isImg: boolean }>({ open: false, x: 0, y: 0, rid: null, label: null, inTable: false, isImg: false })
  const menuCellRef = useRef<HTMLTableCellElement | null>(null)
  const MENU_W = 320
  /** `on` overrides the element the menu is about — the selection overlay of a pinned picture
   *  covers it, so the right-click lands on the overlay and has to be forwarded here. */
  const onContextMenu = (e: React.MouseEvent, on?: HTMLElement) => {
    e.preventDefault()
    saveSelection()
    const el = editorRef.current
    const from = on ?? (e.target as HTMLElement)
    const tgt = from.closest(ITEM_SELECTOR) as HTMLElement | null
    let rid: string | null = null, label: string | null = null
    if (tgt && el && el.contains(tgt)) {
      rid = tgt.getAttribute('data-rid')
      if (!rid) { rid = `r${Date.now().toString(36)}`; tgt.setAttribute('data-rid', rid) }
      label = nodeLabel(tgt)
    }
    const cell = from.closest('td,th') as HTMLTableCellElement | null
    menuCellRef.current = cell && el?.contains(cell) ? cell : null
    // Right-clicking a picture selects it too, so its handles are up when the menu closes.
    const img = tgt && tgt.tagName === 'IMG' ? (tgt as HTMLImageElement) : null
    if (img) selectImage(img)
    setMenu({
      open: true, rid, label, inTable: !!menuCellRef.current, isImg: !!img,
      x: Math.max(8, Math.min(e.clientX - MENU_W, window.innerWidth - MENU_W - 8)),
      y: Math.max(8, Math.min(e.clientY, window.innerHeight - 440)),
    })
  }
  const [wrapOpen, setWrapOpen] = useState(false)
  const closeMenu = () => { setWrapOpen(false); setMenu((m) => ({ ...m, open: false })) }
  const menuAct = (fn: () => void) => { closeMenu(); fn() }
  useEffect(() => {
    if (!menu.open) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') closeMenu() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [menu.open])

  /* Arriving from a letter, the picker lists only the designs that letter could actually print
     with — its category's designs plus the catch-alls — instead of every design in the system. */
  const loadTemplates = useCallback(async () => {
    try {
      setTemplates(await reportTemplates.list(
        linkCategoryId != null || linkDocTypeId != null
          ? { mailCategoryId: linkCategoryId, documentTypeId: linkDocTypeId }
          : undefined))
    } catch { toast.error('تعذّر تحميل التصاميم') }
  }, [toast, linkCategoryId, linkDocTypeId])

  useEffect(() => {
    try { document.execCommand('styleWithCSS', false, 'true') } catch { /* not supported */ }
    // The canvas starts blank: the designer builds the letterhead from the ribbon and the field
    // list rather than deleting a starter layout someone else chose.
    setEditorHtml(EMPTY_RICH_HTML)
    loadTemplates()
    organization.orgUnits().then(setOrgUnits).catch(() => {})
    organization.users().then(setUsers).catch(() => {})
    brandingApi.get().then((b) => setInstName(b.nameAr || 'المؤسسة')).catch(() => {})
    // Lookups for narrowing a design to one document type / mail category.
    documents.types().then(setDocTypes).catch(() => {})
    diwan.categories().then(setMailCategories).catch(() => {})
    if (myUserId != null) account.seal(myUserId).then(setMySeal).catch(() => {})
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => { refreshLogoImgs() }, [logo, refreshLogoImgs])

  // A `templateId` in the URL opens that design for editing, once the list has arrived.
  const linkLoaded = useRef(false)
  useEffect(() => {
    if (linkLoaded.current || linkTemplateId == null) return
    const t = templates.find((x) => x.id === linkTemplateId)
    if (!t) return
    linkLoaded.current = true
    loadTemplate(t)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [templates, linkTemplateId])

  // The preview lives in one shared window; leaving the designer takes it with us.
  useEffect(() => closePreviewWindow, [])

  const watermark = useMemo<Watermark | undefined>(() => {
    if (wmType === 'text' && wmText.trim()) return { type: 'text', text: wmText, color: wmColor, font: wmFont, opacity: wmOpacity, size: wmSize, rotate: wmRotate }
    if (wmType === 'image' && wmImage) return { type: 'image', image: wmImage, opacity: wmOpacity, size: Math.max(200, wmSize * 4), rotate: wmRotate }
    return undefined
  }, [wmType, wmText, wmImage, wmColor, wmFont, wmOpacity, wmSize, wmRotate])

  // Bridge the committed watermark state ↔ the Word-style dialog draft.
  const wmDraft: WmDraft = {
    type: wmType, text: wmText, image: wmImage, color: wmColor, font: wmFont, size: wmSize,
    semiTransparent: wmOpacity <= 0.28, layout: wmRotate === 0 ? 'horizontal' : 'diagonal',
  }
  const commitWm = (v: WmDraft) => {
    setWmType(v.type); setWmText(v.text); setWmImage(v.image); setWmColor(v.color)
    setWmFont(v.font); setWmSize(v.size)
    setWmOpacity(v.semiTransparent ? 0.15 : 0.5)
    setWmRotate(v.layout === 'diagonal' ? -45 : 0)
  }

  const [pw, ph] = useMemo(() => {
    const [bw, bh] = PAGE_DIMS[pageSize]
    return orientation === 'landscape' ? [bh, bw] : [bw, bh]
  }, [pageSize, orientation])

  const [pageCount, setPageCount] = useState(1)

  /**
   * Lay the content out over discrete sheets, the way Word does.
   *
   * The browser will not reflow a contenteditable into pages, so a block that would cross the
   * bottom of its sheet is pushed down to the top of the next one with a margin. Those margins are
   * presentation only — `currentLayoutJson` strips them, so what gets saved is the design, not this
   * screen's pagination.
   */
  const paginate = useCallback(() => {
    const el = editorRef.current
    if (!el) return
    const period = ph + SHEET_GAP
    const contentEnd = (k: number) => k * period + ph - PAD_BOTTOM
    const contentStart = (k: number) => k * period + PAD_TOP

    const blocks = [...el.children] as HTMLElement[]
    // Reset first: pushes from the previous pass would otherwise be measured as content.
    blocks.forEach((b) => { if (b.dataset.rdPush) { b.style.marginTop = ''; delete b.dataset.rdPush } })

    let k = 0
    let afterBreak = false
    for (const b of blocks) {
      const top = b.offsetTop
      const bottom = top + b.offsetHeight
      // A break marker stays at the foot of the page it ends — it is what the user clicks to
      // remove the break, so moving it to the top of the next sheet would point at the wrong page.
      if (top >= contentStart(k) && (afterBreak || bottom > contentEnd(k))) {
        const target = contentStart(k + 1)
        if (target > top) {
          b.style.marginTop = `${target - top}px`
          b.dataset.rdPush = '1'
          // The new margin can collapse with the previous block's, landing short of the target.
          // One correction pass beats trying to predict which margins collapse.
          const drift = target - b.offsetTop
          if (drift !== 0) b.style.marginTop = `${target - top + drift}px`
        }
        k++
      }
      afterBreak = b.classList.contains('rd-pagebreak')
    }
    setPageCount(k + 1)
  }, [ph])

  // Debounced: pagination is a full measure pass, and typing would otherwise run it per keystroke.
  const pgTimer = useRef<number | undefined>(undefined)
  const schedulePaginate = useCallback(() => {
    window.clearTimeout(pgTimer.current)
    pgTimer.current = window.setTimeout(paginate, 120)
  }, [paginate])

  useEffect(() => { paginateRef.current = paginate; paginate() }, [paginate])
  useEffect(() => () => window.clearTimeout(pgTimer.current), [])

  const paperStyle = useMemo<React.CSSProperties>(() => ({
    width: pw,
    height: pageCount * (ph + SHEET_GAP) - SHEET_GAP,
    transform: `scale(${zoom})`, transformOrigin: 'top center',
  }), [pw, ph, pageCount, zoom])

  const sheetStyle = useMemo<React.CSSProperties>(() => {
    const st: React.CSSProperties = { height: ph, marginBottom: SHEET_GAP }
    // Page colour is a property of the paper, not of the text, so it belongs here and not in
    // the editable. (The frame is drawn by `frameStyle` below, inset from the paper edge.)
    if (pageBg) st.background = pageBg
    if (watermark) {
      // On every sheet, not once down the middle of the stack — a watermark is per page.
      st.backgroundImage = `url("${watermarkSvg(watermark)}")`
      st.backgroundPosition = 'center'; st.backgroundRepeat = 'no-repeat'; st.backgroundSize = '70%'
    }
    return st
  }, [ph, watermark, pageBg])

  /* The page frame, inset from the paper edge the way Word draws it (its default is 24pt from
     the edge). Drawn on the sheet's frame layer rather than as a border on the sheet itself:
     a border on the sheet would run along the very edge of the paper, leaving no white outside
     it and sitting right against the first line of text. */
  const FRAME_INSET = 24
  const frameStyle = useMemo<React.CSSProperties | null>(() => {
    if (!pageBorder) return null
    const b = pageBorder, line = `${b.width}pt ${b.style} ${b.color}`
    return {
      inset: FRAME_INSET,
      borderTop: b.edges.top ? line : undefined,
      borderRight: b.edges.right ? line : undefined,
      borderBottom: b.edges.bottom ? line : undefined,
      borderLeft: b.edges.left ? line : undefined,
      boxShadow: b.shadow ? `3px 3px 0 0 ${b.color}` : undefined,
    }
  }, [pageBorder])

  // Previews use the designer's own signature/stamp: they are the one seal we can legitimately
  // show, and a preview that silently dropped the placeholders would hide a layout mistake until
  // someone printed a real letter.
  const previewCtx = useMemo<RenderCtx>(() => ({
    tokens: { ...SAMPLE_TOKENS, institutionName: instName },
    logo, signature: mySeal?.signatureUrl ?? null, stamp: mySeal?.stampUrl ?? null,
    tableRows: SAMPLE_ROWS, htmlTokens: { body: SAMPLE_BODY_HTML },
  }), [logo, instName, mySeal])

  const pageSetup = useMemo(
    () => ({ size: pageSize, orientation, bg: pageBg, border: pageBorder }),
    [pageSize, orientation, pageBg, pageBorder])

  // ── Selection plumbing ──
  const saveSelection = () => {
    const sel = window.getSelection()
    if (sel && sel.rangeCount && editorRef.current?.contains(sel.anchorNode)) savedRange.current = sel.getRangeAt(0).cloneRange()
  }
  const restoreSelection = () => {
    const el = editorRef.current; if (!el) return
    el.focus()
    const sel = window.getSelection()
    if (savedRange.current && sel) { sel.removeAllRanges(); sel.addRange(savedRange.current) }
  }
  const exec = (cmd: string, val?: string) => { restoreSelection(); document.execCommand(cmd, false, val); saveSelection() }
  const insertHtml = (html: string, trailingSpace = true) => {
    restoreSelection(); document.execCommand('insertHTML', false, trailingSpace ? html + '&nbsp;' : html); saveSelection(); scanItems()
  }

  const insertField = (token: string) => insertHtml(tokenChip(token))
  const insertBody = () => insertHtml('<span class="rd-tok rd-tok--block" contenteditable="false" data-token="body">المحتوى (نص الكتاب)</span>', false)
  const insertAttachments = () => insertHtml('<span class="rd-tok rd-tok--block" contenteditable="false" data-token="attachments">جدول المرفقات</span>', false)
  const insertLogo = () => insertHtml(`<img class="rd-logo${logo ? '' : ' rd-logo--empty'}" data-token="logo" src="${logo || TRANSPARENT_PX}" alt="الشعار" style="width:90px"/>`)
  /* Signature and stamp are placeholders, not pictures the designer owns: the actual image comes
     from whoever sends the letter, so the designer only ever shows an empty, labelled slot. */
  const insertSeal = (token: 'signature' | 'stamp') => insertHtml(
    `<img class="rd-logo rd-logo--empty" data-token="${token}" src="${TRANSPARENT_PX}" alt="${IMAGE_TOKENS[token]}" style="width:140px"/>`)

  const nearestBlock = (node: Node | null): HTMLElement | null => {
    const blocks = ['P', 'DIV', 'H1', 'H2', 'H3', 'H4', 'LI', 'BLOCKQUOTE', 'TD', 'TH']
    let el: HTMLElement | null = node && node.nodeType === 3 ? node.parentElement : (node as HTMLElement | null)
    while (el && el !== editorRef.current && !blocks.includes(el.tagName)) el = el.parentElement
    return el && el !== editorRef.current ? el : null
  }
  const styleBlocks = (css: Partial<CSSStyleDeclaration>) => {
    restoreSelection()
    const el = editorRef.current; const sel = window.getSelection()
    if (!el || !sel || !sel.rangeCount) return
    const range = sel.getRangeAt(0)
    let targets = Array.from(el.querySelectorAll<HTMLElement>('p,div,h1,h2,h3,h4,li,blockquote,td,th')).filter((b) => range.intersectsNode(b))
    if (targets.length === 0) { const nb = nearestBlock(sel.anchorNode); if (nb) targets = [nb] }
    targets.forEach((b) => Object.assign(b.style, css))
    saveSelection()
  }

  /* ── Formatting a selected element ─────────────────────────────────────────────
     execCommand works on a *text range*, so with an element selected — a merge chip, a
     placeholder, a table, a picture — the ribbon's font and colour controls had nothing to act
     on and silently did nothing. When something is selected they write inline style onto it
     instead; with nothing selected they behave exactly as before, on the text. */
  const selectedNode = (): HTMLElement | null => selElRef.current ?? selImgRef.current

  /** Apply CSS to the selected element. Returns false when there is none, so callers fall back. */
  const styleSelected = (css: Partial<CSSStyleDeclaration>): boolean => {
    const node = selectedNode()
    if (!node) return false
    Object.assign(node.style, css)
    measureEl(); measureImg()
    schedulePaginate()
    return true
  }

  /** Read a property as it currently renders, so a toggle knows which way to go. */
  const currentStyle = (prop: keyof CSSStyleDeclaration): string => {
    const node = selectedNode()
    return node ? String(getComputedStyle(node)[prop] ?? '') : ''
  }

  /** A ribbon control: the element when one is selected, otherwise the text command. */
  const applyFormat = (css: Partial<CSSStyleDeclaration>, cmd: string, val?: string) => {
    if (!styleSelected(css)) exec(cmd, val)
  }

  /** A toggle (bold, italic, …) resolved against what the element already shows. */
  const toggleFormat = (
    prop: keyof CSSStyleDeclaration, on: string, off: string, isOn: (cur: string) => boolean, cmd: string,
  ) => {
    if (!selectedNode()) { exec(cmd); return }
    styleSelected({ [prop]: isOn(currentStyle(prop)) ? off : on } as Partial<CSSStyleDeclaration>)
  }

  /**
   * «مسح التنسيق». On an element that means the styling this panel adds — font, colours,
   * borders, spacing — and *not* the properties that define what the element is: a picture's
   * size, a floated picture's wrap, a pinned one's position. Clearing those would move or
   * resize it, which is not what "clear formatting" promises.
   */
  const clearFormatting = () => {
    const node = selectedNode()
    if (!node) { exec('removeFormat'); return }
    for (const prop of ['font', 'fontFamily', 'fontSize', 'fontWeight', 'fontStyle', 'color',
      'backgroundColor', 'background', 'textDecoration', 'textDecorationLine', 'textAlign',
      'lineHeight', 'letterSpacing', 'border', 'borderRadius', 'boxShadow', 'padding',
      'opacity', 'direction'] as const) {
      node.style[prop] = ''
    }
    measureEl(); measureImg(); schedulePaginate()
  }

  /** Open the appearance panel on whatever is selected, seeded with how it looks right now. */
  const openFormatPanel = () => {
    const node = selectedNode()
    if (!node) return
    setFmtPanel({
      label: nodeLabel(node),
      initial: readFormat(node),
      isPicture: node.tagName === 'IMG',
    })
  }

  const setFontSize = (v: string) => {
    setCurSize(v)
    // SIZES carries execCommand's 1–7 scale plus the point size it stands for; an element takes
    // the point size, which is what the print stylesheet works in.
    const pt = SIZES.find((s) => s.v === v)?.label ?? '12'
    applyFormat({ fontSize: `${pt}pt` }, 'fontSize', v)
  }

  const stepFont = (dir: 1 | -1) => {
    const idx = SIZES.findIndex((s) => s.v === curSize)
    const ni = Math.min(SIZES.length - 1, Math.max(0, (idx < 0 ? 2 : idx) + dir))
    setFontSize(SIZES[ni].v)
  }
  // ── Table: sized insert (grid picker) + row/column editing ──
  const TD = 'border:1px solid #d1d5db;padding:6px 8px'
  const TH = `${TD};background:#f3f4f6;font-weight:700`
  const insertTableSized = (rows: number, cols: number) => {
    if (rows < 1 || cols < 1) return
    const head = `<tr>${`<th style="${TH}"><br></th>`.repeat(cols)}</tr>`
    const body = `<tr>${`<td style="${TD}"><br></td>`.repeat(cols)}</tr>`
    const bodyRows = rows > 1 ? head + body.repeat(rows - 1) : body
    insertHtml(`<table style="border-collapse:collapse;width:100%"><tbody>${bodyRows}</tbody></table><p><br></p>`, false)
    setTablePicker((p) => ({ ...p, open: false }))
  }
  const newCell = (tag: 'td' | 'th') => {
    const c = document.createElement(tag); c.setAttribute('style', tag === 'th' ? TH : TD); c.innerHTML = '<br>'; return c
  }
  const tableInsertRow = (where: 'above' | 'below') => {
    const cell = menuCellRef.current; if (!cell) return
    const tr = cell.parentElement as HTMLTableRowElement
    const nr = document.createElement('tr')
    for (let i = 0; i < tr.children.length; i++) nr.appendChild(newCell('td'))
    tr.parentElement?.insertBefore(nr, where === 'above' ? tr : tr.nextSibling)
    scanItems()
  }
  const tableInsertCol = (where: 'right' | 'left') => {
    const cell = menuCellRef.current; if (!cell) return
    const idx = cell.cellIndex
    const table = cell.closest('table') as HTMLTableElement | null; if (!table) return
    Array.from(table.rows).forEach((r) => {
      const model = r.cells[Math.min(idx, r.cells.length - 1)]
      const nc = newCell(model && model.tagName === 'TH' ? 'th' : 'td')
      // RTL: lower cell index is visually to the right, so "right" inserts at idx, "left" after it.
      r.insertBefore(nc, r.cells[where === 'right' ? idx : idx + 1] ?? null)
    })
    scanItems()
  }
  const tableDeleteRow = () => {
    const cell = menuCellRef.current; if (!cell) return
    const table = cell.closest('table') as HTMLTableElement | null
    ;(cell.parentElement as HTMLTableRowElement).remove()
    if (table && table.rows.length === 0) table.remove()
    scanItems()
  }
  const tableDeleteCol = () => {
    const cell = menuCellRef.current; if (!cell) return
    const idx = cell.cellIndex
    const table = cell.closest('table') as HTMLTableElement | null; if (!table) return
    Array.from(table.rows).forEach((r) => { if (r.cells[idx]) r.deleteCell(idx) })
    if (table.rows[0] && table.rows[0].cells.length === 0) table.remove()
    scanItems()
  }
  const insertLink = () => {
    const url = window.prompt('أدخل الرابط (يبدأ بـ https://)')
    if (url && /^https?:\/\//i.test(url)) exec('createLink', url)
    else if (url) toast.error('رابط غير صالح')
  }
  // Insert the date *field* (merge token), so it prints the document's real date — not
  // the machine's current date, which was wrong at design time.
  const insertDate = () => insertField('date')
  const insertPageBreak = () => insertHtml('<div class="rd-pagebreak" style="break-before:page;page-break-before:always"></div><p><br></p>', false)
  const onInsertImageFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const f = e.target.files?.[0]; e.target.value = ''
    if (!f) return
    if (f.size > 2_000_000) { toast.error('حجم الصورة كبير (~2MB)'); return }
    const r = new FileReader(); r.onload = () => insertHtml(`<img src="${r.result}" style="max-width:100%"/>`, false); r.readAsDataURL(f)
  }
  const doPaste = async () => {
    try { const text = await navigator.clipboard.readText(); if (text) insertHtml(text.replace(/\n/g, '<br/>'), false) }
    catch { toast.error('استخدم Ctrl+V للصق') }
  }
  const copyFormat = () => {
    restoreSelection()
    const blk = nearestBlock(window.getSelection()?.anchorNode ?? null)
    painterStyle.current = blk ? blk.style.cssText : null
    setPainter(true)
  }
  /* ── Element selection ─────────────────────────────────────────────────────────
     Everything that is a *thing* rather than text — a merge field, the content or attachments
     placeholder, a table, a divider, a page break — can be clicked and selected. Until now only
     pictures could, so the rest were reachable only by right-clicking exactly the right pixel.
     A selected element is outlined and named, and can be removed from the badge or with Delete. */
  const measureEl = useCallback(() => {
    const node = selElRef.current
    if (!node || !node.isConnected) { selElRef.current = null; setElBox(null); return }
    const r = node.getBoundingClientRect()
    setElBox({
      left: r.left, top: r.top, w: r.width, h: r.height, label: nodeLabel(node),
      /* A table holds text the user edits inside it, so Delete has to stay with the caret there.
         A chip, a placeholder, a rule or a page break has no inside — Delete means the element. */
      atomic: node.tagName !== 'TABLE',
    })
  }, [])

  const selectElement = useCallback((node: HTMLElement | null) => {
    selElRef.current = node
    if (node) measureEl(); else setElBox(null)
  }, [measureEl])

  useEffect(() => {
    if (!elBox) return
    const onMove = () => measureEl()
    window.addEventListener('scroll', onMove, true)
    window.addEventListener('resize', onMove)
    return () => { window.removeEventListener('scroll', onMove, true); window.removeEventListener('resize', onMove) }
  }, [elBox, measureEl])
  useEffect(() => { if (selElRef.current) measureEl() }, [zoom, pageCount, measureEl])

  /* ── Dragging an element to a new place ────────────────────────────────────────
     Cut-and-paste is the alternative, and it loses the element's own markup: a merge chip pasted
     back is plain text. So a drag moves the node itself — the same DOM node, re-parented — and
     the design is unchanged apart from where it sits.

     A drop lands where the pointer is, resolved two different ways: an inline element (a merge
     chip) goes exactly at the caret between two letters, while a block (the content placeholder,
     a table, a rule, a page break) goes above or below a whole paragraph, because dropping one
     into the middle of a sentence would split it. */
  const dragElRef = useRef<{ el: HTMLElement; unit: HTMLElement; x: number; y: number; started: boolean } | null>(null)
  const dropRef = useRef<{ ref: Node; mode: 'before' | 'after' | 'range'; range?: Range } | null>(null)
  const [dropMark, setDropMark] = useState<{ left: number; top: number; w: number; h: number } | null>(null)
  const [dragging, setDragging] = useState(false)
  /** «تنسيق العنصر» — the full appearance panel for whatever is selected. */
  const [fmtPanel, setFmtPanel] = useState<{ label: string; initial: ElementFormat; isPicture: boolean } | null>(null)

  /**
   * What actually moves. A block placeholder, a table or a rule usually sits alone inside a
   * paragraph of its own; moving the bare element out of it would leave that paragraph behind as
   * a blank line. So when the element is its parent's only child, the paragraph travels with it.
   */
  const moveUnit = (el: HTMLElement): HTMLElement => {
    const parent = el.parentElement
    return parent && parent !== editorRef.current && parent.parentElement === editorRef.current
      && parent.childNodes.length === 1
      ? parent : el
  }

  const isBlockElement = (el: HTMLElement) =>
    el.tagName === 'TABLE' || el.tagName === 'HR' || el.classList.contains('rd-pagebreak')
    || el.classList.contains('rd-tok--block')
    || (el.tagName === 'IMG' && getComputedStyle(el).display === 'block')

  /** The browser's two spellings of "what caret is under this point". */
  const caretAt = (x: number, y: number): Range | null => {
    const doc = document as Document & {
      caretRangeFromPoint?: (x: number, y: number) => Range | null
      caretPositionFromPoint?: (x: number, y: number) => { offsetNode: Node; offset: number } | null
    }
    if (doc.caretRangeFromPoint) return doc.caretRangeFromPoint(x, y)
    const pos = doc.caretPositionFromPoint?.(x, y)
    if (!pos) return null
    const r = document.createRange()
    r.setStart(pos.offsetNode, pos.offset)
    r.collapse(true)
    return r
  }

  /** Work out where a drop at this point would land, and draw the marker for it. */
  const updateDropTarget = useCallback((el: HTMLElement, unit: HTMLElement, x: number, y: number) => {
    const root = editorRef.current
    if (!root) return
    const under = document.elementFromPoint(x, y) as HTMLElement | null
    if (!under || !root.contains(under) || unit.contains(under)) { dropRef.current = null; setDropMark(null); return }

    if (isBlockElement(el)) {
      // Nearest top-level block under the pointer; above it or below it, by which half.
      let block: HTMLElement = under
      while (block.parentElement && block.parentElement !== root) block = block.parentElement
      if (block === unit) { dropRef.current = null; setDropMark(null); return }
      const r = block.getBoundingClientRect()
      const after = y > r.top + r.height / 2
      dropRef.current = { ref: block, mode: after ? 'after' : 'before' }
      setDropMark({ left: r.left, top: after ? r.bottom : r.top, w: r.width, h: 2 })
      return
    }

    const range = caretAt(x, y)
    if (!range || !root.contains(range.startContainer) || unit.contains(range.startContainer)) {
      dropRef.current = null; setDropMark(null); return
    }
    const r = range.getBoundingClientRect()
    dropRef.current = { ref: range.startContainer, mode: 'range', range }
    setDropMark({ left: r.left, top: r.top || r.bottom - 18, w: 2, h: r.height || 18 })
  }, [])

  const endDrag = useCallback(() => {
    const drag = dragElRef.current
    dragElRef.current = null
    setDragging(false)
    setDropMark(null)
    const drop = dropRef.current
    dropRef.current = null
    if (!drag?.started || !drop) return

    const moved = drag.unit
    if (drop.mode === 'range' && drop.range) {
      drop.range.insertNode(moved)
    } else {
      const ref = drop.ref as HTMLElement
      ref.parentElement?.insertBefore(moved, drop.mode === 'after' ? ref.nextSibling : ref)
    }
    scanItems()
    selectElement(drag.el)
    schedulePaginate()
  }, [scanItems, selectElement, schedulePaginate])

  /** Begin (or arm) a drag of `el` from this pointer position. */
  const beginElementDrag = useCallback((el: HTMLElement, e: { clientX: number; clientY: number }, immediate = false) => {
    const unit = moveUnit(el)
    dragElRef.current = { el, unit, x: e.clientX, y: e.clientY, started: immediate }
    if (immediate) { setDragging(true); updateDropTarget(el, unit, e.clientX, e.clientY) }
  }, [updateDropTarget])

  useEffect(() => {
    const onMove = (ev: PointerEvent) => {
      const drag = dragElRef.current
      if (!drag) return
      // A few pixels of slop, so a click on an element is still a click.
      if (!drag.started) {
        if (Math.abs(ev.clientX - drag.x) + Math.abs(ev.clientY - drag.y) < 5) return
        drag.started = true
        setDragging(true)
      }
      ev.preventDefault()
      updateDropTarget(drag.el, drag.unit, ev.clientX, ev.clientY)
    }
    const onUp = () => endDrag()
    const onKey = (ev: KeyboardEvent) => {
      if (ev.key !== 'Escape' || !dragElRef.current) return
      dragElRef.current = null; dropRef.current = null; setDragging(false); setDropMark(null)
    }
    window.addEventListener('pointermove', onMove)
    window.addEventListener('pointerup', onUp)
    window.addEventListener('keydown', onKey)
    return () => {
      window.removeEventListener('pointermove', onMove)
      window.removeEventListener('pointerup', onUp)
      window.removeEventListener('keydown', onKey)
    }
  }, [updateDropTarget, endDrag])

  /** Remove the selected element and drop the selection with it. */
  const deleteSelectedElement = useCallback(() => {
    const node = selElRef.current
    if (!node) return
    node.remove()
    selElRef.current = null
    setElBox(null)
    scanItems()
    schedulePaginate()
  }, [scanItems, schedulePaginate])

  useEffect(() => {
    if (!elBox) return
    const onKey = (ev: KeyboardEvent) => {
      if (ev.key === 'Escape') { ev.preventDefault(); selectElement(null); return }
      // Only for elements with no text of their own — inside a table, Delete belongs to the caret.
      if ((ev.key === 'Delete' || ev.key === 'Backspace') && elBox.atomic) {
        ev.preventDefault()
        deleteSelectedElement()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [elBox, selectElement, deleteSelectedElement])

  /* ── Picture selection and resizing ────────────────────────────────────────────
     Chrome removed the browser's own image handles from contenteditable, so a picture, a logo
     or a signature slot could only be sized by whatever width it was inserted at. Clicking one
     now selects it and draws handles over it; the size is written as an inline width/height,
     which is what gets saved and printed. */
  const measureImg = useCallback(() => {
    const img = selImgRef.current
    // A selected picture can be deleted from under the overlay (context menu, undo, reload).
    if (!img || !img.isConnected) { selImgRef.current = null; setImgBox(null); return }
    const r = img.getBoundingClientRect()
    setImgBox({ left: r.left, top: r.top, w: r.width, h: r.height })
  }, [])

  const selectImage = useCallback((img: HTMLImageElement | null) => {
    selImgRef.current = img
    if (img) measureImg(); else setImgBox(null)
  }, [measureImg])

  // The overlay is in viewport coordinates: anything that moves the canvas has to move it too.
  useEffect(() => {
    if (!imgBox) return
    const onMove = () => measureImg()
    window.addEventListener('scroll', onMove, true)
    window.addEventListener('resize', onMove)
    return () => { window.removeEventListener('scroll', onMove, true); window.removeEventListener('resize', onMove) }
  }, [imgBox, measureImg])
  // Zooming or repaginating re-lays the sheet under a selected picture.
  useEffect(() => { if (selImgRef.current) measureImg() }, [zoom, pageCount, measureImg])

  const dragRef = useRef<{ x: number; y: number; w: number; h: number; dx: number; dy: number; ratio: number } | null>(null)
  const startResize = (e: React.PointerEvent, dx: number, dy: number) => {
    const img = selImgRef.current; if (!img) return
    e.preventDefault(); e.stopPropagation()
    ;(e.target as HTMLElement).setPointerCapture(e.pointerId)
    const w = img.offsetWidth, h = img.offsetHeight
    dragRef.current = { x: e.clientX, y: e.clientY, w, h, dx, dy, ratio: h / Math.max(1, w) }
  }
  /** The widest a picture may be: the text area. Wider than this and it prints cropped. */
  const maxImgWidth = () => (editorRef.current ? editorRef.current.clientWidth - 88 : 700)

  /**
   * Write a new size onto a picture, keeping it inside the page and — when the proportions are
   * locked — on its own ratio. `ratio` is the shape at the start of the gesture, not the natural
   * one, so a picture that was deliberately stretched keeps that shape while it is resized.
   */
  const applyImgSize = useCallback((img: HTMLImageElement, w: number, h: number,
                                    ratio: number, lock: boolean, setW: boolean, setH: boolean) => {
    const maxW = maxImgWidth()
    const fit = (v: number, hi: number) => Math.round(Math.min(hi, Math.max(24, v)))
    if (lock) {
      // Clamp the driving axis first, then derive the other, or clamping would skew the shape.
      if (setW) { w = fit(w, maxW); h = Math.max(24, Math.round(w * ratio)) }
      else { h = Math.max(24, Math.round(h)); w = fit(h / ratio, maxW); h = Math.round(w * ratio) }
      img.style.width = `${w}px`; img.style.height = `${h}px`
    } else {
      if (setW) img.style.width = `${fit(w, maxW)}px`
      if (setH) img.style.height = `${fit(h, 4000)}px`
    }
    measureImg()
  }, [measureImg])

  const onResizeMove = (e: React.PointerEvent) => {
    const d = dragRef.current, img = selImgRef.current
    if (!d || !img) return
    /* Shift flips the proportion lock: a corner keeps the shape unless Shift frees it, a side
       handle stretches one axis unless Shift makes the other follow. Ctrl resizes from the
       centre, as Word does — the picture grows on both sides, so the pointer travels half as far. */
    const lock = d.dx && d.dy ? !e.shiftKey : e.shiftKey
    const k = e.ctrlKey || e.metaKey ? 2 : 1
    // The canvas is scaled by `zoom`; pointer travel is in screen pixels.
    const w = d.w + ((e.clientX - d.x) / zoom) * d.dx * k
    const h = d.h + ((e.clientY - d.y) / zoom) * d.dy * k
    applyImgSize(img, w, h, d.ratio, lock, !!d.dx, !!d.dy)
  }
  const endResize = (e: React.PointerEvent) => {
    if (!dragRef.current) return
    dragRef.current = null
    ;(e.target as HTMLElement).releasePointerCapture?.(e.pointerId)
    // A taller picture can push the text onto another sheet.
    schedulePaginate()
  }
  /**
   * Keyboard sizing for the selected picture — the accessible half of the handles, and the only
   * way to hit an exact number of pixels.
   *
   * A modifier is required: Shift + ← → ↑ ↓ resizes in 8px steps with the proportions locked,
   * Ctrl + arrow in 1px steps on one axis, and the two together give a proportional 1px step.
   * A bare arrow is left alone — it moves the caret, as it does everywhere else, and releases
   * the picture. That is what keeps the selection from swallowing ordinary navigation.
   */
  useEffect(() => {
    if (!imgBox) return
    const ARROWS: Record<string, [number, number]> = {
      ArrowRight: [1, 0], ArrowLeft: [-1, 0], ArrowDown: [0, 1], ArrowUp: [0, -1],
    }
    const onKey = (ev: KeyboardEvent) => {
      const img = selImgRef.current
      if (!img) return
      if (ev.key === 'Escape') { ev.preventDefault(); selectImage(null); return }
      const dir = ARROWS[ev.key]
      if (!dir) {
        // A printable key means the user is writing again, not sizing.
        if (ev.key.length === 1 && !ev.ctrlKey && !ev.metaKey && !ev.altKey) selectImage(null)
        return
      }
      const fine = ev.ctrlKey || ev.metaKey
      // A plain arrow belongs to the caret; sizing takes a modifier.
      if (!fine && !ev.shiftKey) { selectImage(null); return }
      ev.preventDefault()
      const step = fine ? 1 : 8
      const w0 = img.offsetWidth, h0 = img.offsetHeight
      applyImgSize(img, w0 + dir[0] * step, h0 + dir[1] * step,
        h0 / Math.max(1, w0), ev.shiftKey, dir[0] !== 0, dir[1] !== 0)
      schedulePaginate()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [imgBox, applyImgSize, selectImage, schedulePaginate])

  /* ── Text wrapping (التفاف النص) ───────────────────────────────────────────────
     Word's picture menu, in the modes CSS reproduces faithfully on screen and on paper:
     inline with the text, floated so the text runs beside it, on a line of its own, or pinned
     behind / in front of the text. Everything is written as inline style, so it saves and
     prints with the design. */
  const wrapOf = (img: HTMLImageElement): WrapMode => {
    const s = img.style
    if (s.position === 'absolute') return s.zIndex === '-1' ? 'behind' : 'front'
    if (s.float === 'right' || s.float === 'left') return s.float
    return s.display === 'block' ? 'block' : 'inline'
  }

  const setWrap = (img: HTMLImageElement, mode: WrapMode) => {
    const el = editorRef.current; if (!el) return
    // Where it sits right now, measured before the layout changes: pinning a picture must leave
    // it exactly where the user was looking at it, not jump it to the top of the page.
    const left = img.offsetLeft, top = img.offsetTop
    const s = img.style
    s.float = ''; s.clear = ''; s.display = ''; s.position = ''; s.left = ''; s.top = ''; s.zIndex = ''
    s.marginLeft = ''; s.marginRight = ''; s.marginTop = ''; s.marginBottom = ''
    if (mode === 'right' || mode === 'left') {
      s.float = mode
      s.marginTop = '4px'; s.marginBottom = '8px'
      // The gap goes on the side the text runs down, physically — never mirrored by direction.
      if (mode === 'right') s.marginLeft = '14px'; else s.marginRight = '14px'
    } else if (mode === 'block') {
      s.display = 'block'; s.clear = 'both'; s.marginTop = '10px'; s.marginBottom = '10px'
    } else if (mode !== 'inline') {
      s.position = 'absolute'
      // A percentage of the containing block, so the picture keeps its place on paper even
      // though the printed sheet is not the same number of pixels wide as this canvas.
      s.left = `${((left / Math.max(1, el.clientWidth)) * 100).toFixed(2)}%`
      s.top = `${Math.round(top)}px`
      s.zIndex = mode === 'behind' ? '-1' : '2'
    }
    selectImage(img)
    schedulePaginate()
  }

  /* Dragging a pinned picture moves it. Only pinned ones: a floated or inline picture belongs
     to the text flow, and its place is decided by where it sits in that flow. */
  const moveRef = useRef<{ x: number; y: number; left: number; top: number } | null>(null)
  const movable = !!selImgRef.current && selImgRef.current.style.position === 'absolute'
  const startMove = (e: React.PointerEvent) => {
    const img = selImgRef.current
    if (!img || img.style.position !== 'absolute') return
    e.preventDefault()
    ;(e.currentTarget as HTMLElement).setPointerCapture(e.pointerId)
    moveRef.current = { x: e.clientX, y: e.clientY, left: img.offsetLeft, top: img.offsetTop }
  }
  const onMoveDrag = (e: React.PointerEvent) => {
    const m = moveRef.current, img = selImgRef.current, el = editorRef.current
    if (!m || !img || !el) return
    const w = el.clientWidth
    const left = Math.min(w - img.offsetWidth, Math.max(0, m.left + (e.clientX - m.x) / zoom))
    img.style.left = `${((left / Math.max(1, w)) * 100).toFixed(2)}%`
    img.style.top = `${Math.round(Math.max(0, m.top + (e.clientY - m.y) / zoom))}px`
    measureImg()
  }
  const endMove = (e: React.PointerEvent) => {
    if (!moveRef.current) return
    moveRef.current = null
    ;(e.currentTarget as HTMLElement).releasePointerCapture?.(e.pointerId)
  }

  /** Context-menu sizes: back to the picture's own size, or the full width of the text area. */
  const sizeImage = (mode: 'natural' | 'full') => {
    const img = selImgRef.current ?? (menu.rid ? editorRef.current?.querySelector<HTMLImageElement>(`img[data-rid="${menu.rid}"]`) : null)
    if (!img) return
    // An unfilled slot has no size of its own — its picture is a 1×1 transparent pixel that
    // arrives only at print time. Both presets therefore work from the shape on screen, so a
    // signature slot does not collapse to nothing or blow up into a page-wide square.
    const slot = img.naturalWidth <= 1
    const ratio = img.offsetHeight / Math.max(1, img.offsetWidth)
    if (mode === 'full') {
      img.style.width = '100%'
      img.style.height = `${Math.round(maxImgWidth() * ratio)}px`
    } else if (slot) {
      const def = img.getAttribute('data-token') === 'logo' ? 90 : 140
      img.style.width = `${def}px`
      img.style.height = `${Math.round(def * ratio)}px`
    } else {
      img.style.width = ''
      img.style.height = ''
    }
    selectImage(img)
    schedulePaginate()
  }

  const IMG_HANDLES: { k: string; dx: number; dy: number }[] = [
    { k: 'nw', dx: -1, dy: -1 }, { k: 'n', dx: 0, dy: -1 }, { k: 'ne', dx: 1, dy: -1 },
    { k: 'e', dx: 1, dy: 0 }, { k: 'se', dx: 1, dy: 1 }, { k: 's', dx: 0, dy: 1 },
    { k: 'sw', dx: -1, dy: 1 }, { k: 'w', dx: -1, dy: 0 },
  ]

  /**
   * Word's "click and type": click anywhere on the blank part of a sheet and start writing.
   *
   * A contenteditable puts no caret in space that holds no text, so clicking halfway down an
   * empty page used to do nothing — the only way to reach that spot was to press Enter from the
   * last line until you got there. This does exactly that, but in one click: empty paragraphs
   * are appended until the text reaches the clicked line, and the caret is left in the last one.
   * The horizontal third that was clicked sets the alignment, as Word does (RTL: right = start).
   */
  const clickToType = (e: React.MouseEvent) => {
    const el = editorRef.current
    if (!el || e.button !== 0) return
    const tgt = e.target as HTMLElement
    // A picture takes the selection (and its resize handles) instead of the caret; anything else
    // that is an element rather than text gets outlined and named.
    const isImg = tgt.tagName === 'IMG'
    selectImage(isImg ? (tgt as HTMLImageElement) : null)
    const item = isImg ? null : tgt.closest(ITEM_SELECTOR) as HTMLElement | null
    const picked = item && el.contains(item) ? item : null
    selectElement(picked)
    /* Arm a drag on the element itself — it only becomes one after a few pixels of movement, so
       a plain click still selects. Not from inside a table: there the pointer is selecting cell
       text, so a table is dragged by the grip on its badge instead. */
    if (picked && picked.tagName !== 'TABLE' && !tgt.closest('td,th')) {
      beginElementDrag(picked, e)
    }
    // Only blank canvas from here. A click that landed on a block, a table or a picture is the
    // browser's to place, and hijacking it would move the caret away from what the user pointed at.
    if (tgt !== el) return

    // getBoundingClientRect is post-transform, offsetTop is not: divide out the zoom so the
    // two agree. `transform-origin: top center` keeps the vertical mapping a plain scale.
    const rect = el.getBoundingClientRect()
    const y = (e.clientY - rect.top) / zoom
    const x = (e.clientX - rect.left) / zoom

    const last = el.lastElementChild as HTMLElement | null
    const bottom = last ? last.offsetTop + last.offsetHeight : PAD_TOP
    if (y <= bottom) return

    // Never past the last sheet's bottom margin: filling the grey channel with paragraphs would
    // add a page the user did not ask for.
    const limit = (pageCount - 1) * (ph + SHEET_GAP) + ph - PAD_BOTTOM
    const target = Math.min(y, limit)
    if (target <= bottom) return

    e.preventDefault()
    const w = el.clientWidth
    const align = x > w * 0.66 ? 'right' : x < w * 0.34 ? 'left' : 'center'

    const addLine = () => {
      const p = document.createElement('p')
      p.innerHTML = '<br>'
      el.appendChild(p)
      return p
    }
    let p = addLine()
    // Bounded: a mis-measured line height must not spin here.
    for (let i = 0; i < 400 && p.offsetTop + p.offsetHeight < target; i++) p = addLine()
    // 'right' is the canvas default (dir="rtl"); leaving it off keeps the saved HTML clean.
    if (align !== 'right') p.style.textAlign = align

    const range = document.createRange()
    range.selectNodeContents(p)
    range.collapse(true)
    const sel = window.getSelection()
    sel?.removeAllRanges(); sel?.addRange(range)
    el.focus()
    savedRange.current = range.cloneRange()
    schedulePaginate()
  }

  const onEditorMouseUp = () => {
    saveSelection()
    if (painter && painterStyle.current != null) {
      const blk = nearestBlock(window.getSelection()?.anchorNode ?? null)
      if (blk) blk.style.cssText += ';' + painterStyle.current
      setPainter(false)
    }
  }

  /**
   * «الحدود والتظليل»: the page tab sets the frame on the paper, the other two write inline
   * style onto whatever paragraphs the selection touches — the same path the ribbon's paragraph
   * controls use, so a bordered paragraph is still an ordinary block that saves and prints.
   */
  const applyBorders = (v: BordersDraft, which: BordersTab) => {
    setBordersDraft(v)
    if (which === 'page') { setPageBorder(draftToPageBorder(v)); return }
    styleBlocks(draftToBlockCss(v, which) as Partial<CSSStyleDeclaration>)
    schedulePaginate()
  }
  const openBorders = (which: BordersTab) => {
    // The page tab opens on the frame that is actually on the page, not on the last paragraph edit.
    if (which === 'page' && pageBorder) setBordersDraft((d) => pageBorderToDraft(pageBorder, d))
    setBordersTab(which); setBordersOpen(true)
  }

  function resetNew() {
    setEditId(null); setName('تصميم جديد')
    setScope((canManageOrg ? (linkScope ?? 0) : 2) as TemplateScope)
    setOrgUnitId(linkOrgUnitId ?? ''); setUserId(myUserId ?? ''); setIsActive(true)
    // Keep whatever the caller scoped us to, so «تصميم جديد» from a letter starts on that
    // letter's target and category rather than dropping back to "كل شيء".
    setTarget((linkTarget ?? 2) as TemplateTarget)
    setDocTypeId(linkDocTypeId ?? ''); setMailCategoryId(linkCategoryId ?? '')
    setLogo(null); setEditorHtml(EMPTY_RICH_HTML)
    setWmType('none'); setWmImage(null); setPageSize('A4'); setOrientation('portrait')
    setPageBg(null); setPageBorder(null); setBordersDraft(DEFAULT_BORDERS)
  }

  function loadTemplate(t: ReportTemplate) {
    setEditId(t.id); setName(t.name)
    setScope(t.scope === 'User' ? 2 : t.scope === 'OrgUnit' ? 1 : 0)
    setOrgUnitId(t.orgUnitId ?? ''); setUserId(t.userId ?? (myUserId ?? ''))
    setTarget(t.target === 'Correspondence' ? 0 : t.target === 'Document' ? 1 : 2)
    setDocTypeId(t.documentTypeId ?? ''); setMailCategoryId(t.mailCategoryId ?? '')
    setIsActive(t.isActive); setLogo(t.logoBase64)
    const parsed = parseLayout(t.layoutJson)
    if (parsed.kind === 'rich') {
      setEditorHtml(parsed.html)
      const wm = parsed.watermark
      if (wm) {
        setWmType(wm.type); setWmText(wm.text ?? 'مسودة'); setWmImage(wm.image ?? null)
        setWmColor(wm.color ?? '#9ca3af'); setWmFont(wm.font ?? 'Segoe UI'); setWmOpacity(wm.opacity ?? 0.15)
        setWmSize(wm.type === 'image' ? Math.round((wm.size ?? 420) / 4) : (wm.size ?? 72)); setWmRotate(wm.rotate ?? -45)
      } else { setWmType('none'); setWmImage(null) }
      setPageSize(parsed.page?.size ?? 'A4'); setOrientation(parsed.page?.orientation ?? 'portrait')
      setPageBg(parsed.page?.bg ?? null); setPageBorder(parsed.page?.border ?? null)
    } else {
      const idTokens: Record<string, string> = {}
      TOKENS.forEach((x) => { idTokens[x.token] = `{{${x.token}}}` })
      setEditorHtml(renderReportHtml(parsed.blocks, { tokens: idTokens, logo: null }))
      setWmType('none'); setWmImage(null); setPageSize('A4'); setOrientation('portrait')
      setPageBg(null); setPageBorder(null)
    }
  }

  // الشعار button in the ribbon: upload a logo image here and drop the logo element in.
  function onLogoPick(e: React.ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0]; e.target.value = ''
    if (!f) return
    if (f.size > 1_500_000) { toast.error('حجم الشعار كبير (الحد ~1.5MB)'); return }
    const reader = new FileReader()
    reader.onload = () => {
      setLogo(reader.result as string)
      // Add a logo element if the design doesn't already have one (existing ones auto-update).
      if (!editorRef.current?.querySelector('img[data-token="logo"]')) insertLogo()
    }
    reader.readAsDataURL(f)
  }
  /**
   * Import a Word file as the design.
   *
   * It replaces the canvas rather than inserting at the cursor: a .docx is a whole page, and
   * dropping one into the middle of an existing design produces a mess nobody asked for. The
   * replacement is confirmed whenever there is anything to lose.
   */
  async function onDocxPick(e: React.ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0]; e.target.value = ''
    if (!f) return
    const hasContent = (editorRef.current?.textContent ?? '').trim().length > 0
    if (hasContent && !(await confirm({
      message: `سيحلّ محتوى «${f.name}» محلّ التصميم الحالي. هل تريد المتابعة؟`,
      destructive: true,
    }))) return

    setBusy(true)
    try {
      const { html, notes, pages } = await importDocx(f)
      setEditorHtml(html)
      if (!name.trim() || name === 'تصميم جديد') setName(f.name.replace(/\.docx$/i, ''))
      toast.success(pages > 1 ? `استُورد ملف Word — ${pages} صفحات` : 'استُورد ملف Word')
      // Shown as toasts, not swallowed: the user is about to save this as a design and needs to
      // know what did not come across before they trust it.
      notes.forEach((n) => toast.toast(n, 'info'))
    } catch (err) {
      toast.error(err instanceof DocxImportError ? err.message : 'تعذّر استيراد الملف')
    } finally { setBusy(false) }
  }

  function currentLayoutJson(): string {
    const tmp = document.createElement('div')
    tmp.innerHTML = getEditorHtml()
    // Picture placeholders are saved empty — the logo comes from the design record and the
    // signature/stamp from whoever sends the letter, so baking any of them into the layout
    // would freeze one person's seal onto every letter printed from this design.
    tmp.querySelectorAll('img[data-token]').forEach((i) => i.setAttribute('src', ''))
    // Sheet pushes are how this screen shows pages; they are not part of the design.
    tmp.querySelectorAll<HTMLElement>('[data-rd-push]').forEach((n) => {
      n.style.marginTop = ''; n.removeAttribute('data-rd-push')
      if (!n.getAttribute('style')) n.removeAttribute('style')
    })
    const html = sanitizeRichHtml(tmp.innerHTML, { allowTokens: true })
    return JSON.stringify({ v: 2, html, watermark, page: pageSetup })
  }

  async function save() {
    if (!name.trim()) { toast.error('أدخل اسم التصميم'); return }
    if (scope === 1 && !orgUnitId) { toast.error('اختر القسم'); return }
    if (scope === 2 && !userId) { toast.error('اختر المستخدم'); return }
    setBusy(true)
    try {
      const body = {
        scope, target, name: name.trim(), isActive,
        orgUnitId: scope === 1 ? Number(orgUnitId) : null,
        userId: scope === 2 ? Number(userId) : null,
        // Each target carries its own narrowing; the other is dropped so switching target
        // cannot leave a stale type attached to the design.
        documentTypeId: target === 1 && docTypeId !== '' ? Number(docTypeId) : null,
        mailCategoryId: target === 0 && mailCategoryId !== '' ? Number(mailCategoryId) : null,
        logoBase64: logo, layoutJson: currentLayoutJson(),
      }
      if (editId) { await reportTemplates.update(editId, body); toast.success('تم حفظ التصميم') }
      else { const created = await reportTemplates.create(body); setEditId(created.id); toast.success('تم إنشاء التصميم') }
      await loadTemplates()
    } catch (e) { toast.error(errOf(e, 'تعذّر حفظ التصميم')) }
    finally { setBusy(false) }
  }

  async function remove() {
    if (!editId) return
    if (!(await confirm({ message: `حذف التصميم «${name}»؟`, destructive: true }))) return
    setBusy(true)
    try { await reportTemplates.remove(editId); toast.success('تم حذف التصميم'); resetNew(); await loadTemplates() }
    catch (e) { toast.error(errOf(e, 'تعذّر حذف التصميم')) }
    finally { setBusy(false) }
  }

  /* One preview, one window: every click replaces the sheet already on screen rather than opening
     another popup. It stops at the preview instead of firing the print dialog, and it only offers
     the طباعة button to someone who actually holds a Print right. */
  const preview = () => printLayout(
    JSON.stringify({ v: 2, html: getEditorHtml(), watermark, page: pageSetup }),
    previewCtx, name,
    { mode: 'preview', allowPrint: canPrint, guard: true },
  )

  // ── Ribbon building blocks ──
  const pd = (e: React.MouseEvent) => e.preventDefault()
  const Cmd = ({ cmd, val, title, children }: { cmd: string; val?: string; title: string; children: React.ReactNode }) => (
    <button type="button" className="rb-btn" title={title} aria-label={title} onMouseDown={pd} onClick={() => exec(cmd, val)}>{children}</button>
  )
  const Act = ({ onRun, title, active, children }: { onRun: () => void; title: string; active?: boolean; children: React.ReactNode }) => (
    <button type="button" className={`rb-btn${active ? ' is-active' : ''}`} title={title} aria-label={title} onMouseDown={pd} onClick={onRun}>{children}</button>
  )
  const Big = ({ onRun, label, active, children }: { onRun: () => void; label: string; active?: boolean; children: React.ReactNode }) => (
    <button type="button" className={`rb-big${active ? ' is-active' : ''}`} onMouseDown={pd} onClick={onRun}>
      <span className="rb-big__ic">{children}</span><span className="rb-big__lb">{label}</span>
    </button>
  )
  const Group = ({ cap, children }: { cap: string; children: React.ReactNode }) => (
    <div className="rb-group"><div className="rb-group__body">{children}</div><div className="rb-group__cap">{cap}</div></div>
  )

  return (
    <div>
      {/* Design settings — a dialog shown before entering the design canvas */}
      {metaDialogOpen && (
      <div className="wm-backdrop" onMouseDown={() => setMetaDialogOpen(false)}>
      <div className="wm-dialog rd-metadlg" dir="rtl" onMouseDown={(e) => e.stopPropagation()} role="dialog" aria-modal="true" aria-label="إعدادات التصميم">
        <div className="wm-dialog__head">
          <h3>إعدادات التصميم</h3>
          <button type="button" className="wm-x" onClick={() => setMetaDialogOpen(false)} aria-label="إغلاق"><X size={18} /></button>
        </div>
        <div className="wm-dialog__body">
        <div className="rd-modeseg" role="tablist" aria-label="نوع التصميم">
          <button type="button" role="tab" aria-selected={designMode === 'new'} className={`rd-modeseg__b${designMode === 'new' ? ' is-active' : ''}`}
            onClick={() => { setDesignMode('new'); resetNew() }}>تصميم جديد</button>
          <button type="button" role="tab" aria-selected={designMode === 'edit'} className={`rd-modeseg__b${designMode === 'edit' ? ' is-active' : ''}`}
            onClick={() => setDesignMode('edit')}>تعديل تصميم محفوظ</button>
        </div>
        <div className="form-grid">
          {designMode === 'edit' && (
            <LabeledSelect label="التصميم المحفوظ" value={editId ?? ''}
              onChange={(e) => { const v = e.target.value; if (!v) resetNew(); else { const t = templates.find((x) => x.id === Number(v)); if (t) loadTemplate(t) } }}>
              <option value="">— اختر تصميمًا —</option>
              {templates.map((t) => <option key={t.id} value={t.id}>{t.name} ({t.scope}{t.orgUnitName ? `: ${t.orgUnitName}` : t.userName ? `: ${t.userName}` : ''}{t.documentTypeName ? ` — ${t.documentTypeName}` : t.mailCategoryName ? ` — ${t.mailCategoryName}` : ''})</option>)}
            </LabeledSelect>
          )}
          <LabeledInput label="اسم التصميم *" value={name} onChange={(e) => setName(e.target.value)} />
          <LabeledSelect label="المستوى" value={scope} onChange={(e) => setScope(Number(e.target.value) as TemplateScope)}>
            {SCOPES.filter((s) => canManageOrg || s.v === 2).map((s) => <option key={s.v} value={s.v}>{s.label}</option>)}
          </LabeledSelect>
          {scope === 1 && (
            <LabeledSelect label="القسم/الإدارة *" value={orgUnitId} onChange={(e) => setOrgUnitId(e.target.value ? Number(e.target.value) : '')}>
              <option value="">—</option>
              {orgUnits.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
            </LabeledSelect>
          )}
          {scope === 2 && (
            <LabeledSelect label="المستخدم *" value={userId} disabled={!canManageOrg}
              onChange={(e) => setUserId(e.target.value ? Number(e.target.value) : '')}
              hint={!canManageOrg ? 'يمكنك تصميم ترويستك الشخصية فقط.' : undefined}>
              <option value="">—</option>
              {users.map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
            </LabeledSelect>
          )}
          <LabeledSelect label="يُطبَّق على" value={target} onChange={(e) => setTarget(Number(e.target.value) as TemplateTarget)}>
            {TARGETS.map((tt) => <option key={tt.v} value={tt.v}>{tt.label}</option>)}
          </LabeledSelect>
          {/* Optional narrowing: a design for one document type / mail category beats the
              catch-all at the same level, so «الشؤون المالية» can print salary letters on a
              different letterhead from its contracts. */}
          {target === 1 && (
            <LabeledSelect label="نوع الوثيقة" value={docTypeId}
              onChange={(e) => setDocTypeId(e.target.value ? Number(e.target.value) : '')}
              hint="اتركه فارغًا ليُطبَّق على كل الأنواع.">
              <option value="">كل الأنواع</option>
              {docTypes.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
            </LabeledSelect>
          )}
          {target === 0 && (
            <LabeledSelect label="تصنيف البريد" value={mailCategoryId}
              onChange={(e) => setMailCategoryId(e.target.value ? Number(e.target.value) : '')}
              hint="اتركه فارغًا ليُطبَّق على كل التصنيفات.">
              <option value="">كل التصنيفات</option>
              {mailCategories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </LabeledSelect>
          )}
          <LabeledSelect label="الحالة" value={isActive ? '1' : '0'} onChange={(e) => setIsActive(e.target.value === '1')}>
            <option value="1">مفعّل</option><option value="0">معطّل</option>
          </LabeledSelect>
        </div>
        </div>
        <div className="wm-dialog__foot">
          <div className="wm-foot-spacer" />
          <button type="button" className="btn btn-primary" onClick={() => setMetaDialogOpen(false)}>متابعة إلى التصميم</button>
        </div>
      </div>
      </div>
      )}

      {/* Word-style ribbon */}
      <div className="rd-shell">
        {/* The page title and its back button used to sit above the ribbon, costing the canvas
            ~110px of height for two words. Both now live on the ribbon's own tab row. */}
        <div className="rb-tabs">
          <Link to="/app/print-design" className="rb-back" title="رجوع إلى التصاميم">
            <ChevronRight size={16} aria-hidden />
            <span>التصاميم</span>
          </Link>
          {TABS.map((t) => (
            <button key={t.id} type="button" className={`rb-tab${tab === t.id ? ' is-active' : ''}`} onClick={() => setTab(t.id)}>{t.label}</button>
          ))}
          <span className="rb-tabs__name" title={name}>{name}</span>

          {/* Save and the rest live on the tab row, where Word keeps them. Below the canvas they
              were a scroll away: the sheet stack is taller than the window, so finishing a design
              meant scrolling past the page you were working on to find حفظ. */}
          <div className="rb-acts">
            <button type="button" className="rb-act rb-act--primary" onClick={save} disabled={busy}>
              <Save size={14} aria-hidden /> {editId ? 'حفظ' : 'إنشاء'}
            </button>
            <button type="button" className="rb-act" onClick={resetNew}>
              <FilePlus size={14} aria-hidden /> جديد
            </button>
            {editId && (
              <button type="button" className="rb-act rb-act--danger" onClick={remove} disabled={busy}>
                <Trash2 size={14} aria-hidden /> حذف
              </button>
            )}
          </div>
        </div>

        <div className="rb-panel">
          {tab === 'home' && <>
            <Group cap="حافظة">
              <Act onRun={doPaste} title="لصق"><ClipboardPaste size={18} /></Act>
              <Cmd cmd="copy" title="نسخ"><Copy size={16} /></Cmd>
              <Cmd cmd="cut" title="قص"><Scissors size={16} /></Cmd>
              <Act onRun={copyFormat} title="نسخ التنسيق" active={painter}><Paintbrush size={16} /></Act>
            </Group>
            {/* With an element selected these write onto it; otherwise they act on the text,
                exactly as they always did. */}
            <Group cap={elBox || imgBox ? 'خط — العنصر المحدَّد' : 'خط'}>
              <select className="rd-tb__sel" defaultValue="" aria-label="الخط"
                onChange={(e) => { const v = e.target.value; e.currentTarget.selectedIndex = 0; if (v) applyFormat({ fontFamily: v }, 'fontName', v) }}>
                <option value="" disabled>الخط</option>
                {FONTS.map((f) => <option key={f} value={f} style={{ fontFamily: f }}>{f}</option>)}
              </select>
              <select className="rd-tb__sel rd-tb__sel--sm" value={curSize} aria-label="الحجم"
                onChange={(e) => setFontSize(e.target.value)}>
                {SIZES.map((s) => <option key={s.v} value={s.v}>{s.label}</option>)}
              </select>
              <Act onRun={() => stepFont(1)} title="تكبير الخط"><span className="rb-a">A˄</span></Act>
              <Act onRun={() => stepFont(-1)} title="تصغير الخط"><span className="rb-a rb-a--sm">A˅</span></Act>
              <span className="rb-sep" />
              <Act onRun={() => toggleFormat('fontWeight', '700', '400', (c) => Number(c) >= 600 || c === 'bold', 'bold')} title="عريض"><Bold size={16} /></Act>
              <Act onRun={() => toggleFormat('fontStyle', 'italic', 'normal', (c) => c === 'italic', 'italic')} title="مائل"><Italic size={16} /></Act>
              <Act onRun={() => toggleFormat('textDecorationLine', 'underline', 'none', (c) => c.includes('underline'), 'underline')} title="تسطير"><Underline size={16} /></Act>
              <Act onRun={() => toggleFormat('textDecorationLine', 'line-through', 'none', (c) => c.includes('line-through'), 'strikeThrough')} title="يتوسطه خط"><Strikethrough size={16} /></Act>
              <Cmd cmd="superscript" title="أس علوي"><Superscript size={16} /></Cmd>
              <Cmd cmd="subscript" title="أس سفلي"><Subscript size={16} /></Cmd>
              <label className="rb-btn rb-color" title="لون النص"><Type size={16} />
                <input type="color" onMouseDown={(e) => e.stopPropagation()} onChange={(e) => applyFormat({ color: e.target.value }, 'foreColor', e.target.value)} /></label>
              <label className="rb-btn rb-color" title="تظليل"><Baseline size={16} />
                <input type="color" onMouseDown={(e) => e.stopPropagation()} onChange={(e) => applyFormat({ backgroundColor: e.target.value }, 'hiliteColor', e.target.value)} /></label>
              <Act onRun={clearFormatting} title="مسح التنسيق"><RemoveFormatting size={16} /></Act>
            </Group>
            <Group cap="فقرة">
              <Cmd cmd="insertUnorderedList" title="قائمة نقطية"><List size={16} /></Cmd>
              <Cmd cmd="insertOrderedList" title="قائمة رقمية"><ListOrdered size={16} /></Cmd>
              <Cmd cmd="outdent" title="إنقاص البادئة"><IndentDecrease size={16} /></Cmd>
              <Cmd cmd="indent" title="زيادة البادئة"><IndentIncrease size={16} /></Cmd>
              <select className="rd-tb__sel rd-tb__sel--sm" defaultValue="" aria-label="تباعد الأسطر" title="تباعد الأسطر"
                onChange={(e) => { const v = e.target.value; e.currentTarget.selectedIndex = 0; if (!v) return; if (!styleSelected({ lineHeight: v })) styleBlocks({ lineHeight: v }) }}>
                <option value="" disabled>تباعد</option>
                {LINE_SPACINGS.map((s) => <option key={s} value={s}>{s}</option>)}
              </select>
              <span className="rb-sep" />
              <Act onRun={() => applyFormat({ textAlign: 'right' }, 'justifyRight')} title="محاذاة يمين"><AlignRight size={16} /></Act>
              <Act onRun={() => applyFormat({ textAlign: 'center' }, 'justifyCenter')} title="توسيط"><AlignCenter size={16} /></Act>
              <Act onRun={() => applyFormat({ textAlign: 'left' }, 'justifyLeft')} title="محاذاة يسار"><AlignLeft size={16} /></Act>
              <Act onRun={() => applyFormat({ textAlign: 'justify' }, 'justifyFull')} title="ضبط"><AlignJustify size={16} /></Act>
              <span className="rb-sep" />
              <Act onRun={() => { if (!styleSelected({ direction: 'rtl', textAlign: 'right' })) styleBlocks({ direction: 'rtl', textAlign: 'right' }) }} title="اتجاه من اليمين"><span className="rb-a rb-a--sm">RTL</span></Act>
              <Act onRun={() => { if (!styleSelected({ direction: 'ltr', textAlign: 'left' })) styleBlocks({ direction: 'ltr', textAlign: 'left' }) }} title="اتجاه من اليسار"><span className="rb-a rb-a--sm">LTR</span></Act>
              <span className="rb-sep" />
              <Act onRun={() => openBorders('borders')} title="الحدود والتظليل"><SquareDashed size={16} /></Act>
              <Act onRun={() => openBorders('shading')} title="تظليل الفقرة"><PaintBucket size={16} /></Act>
            </Group>
            <Group cap="تراجع">
              <Cmd cmd="undo" title="تراجع"><Undo2 size={16} /></Cmd>
              <Cmd cmd="redo" title="إعادة"><Redo2 size={16} /></Cmd>
            </Group>
          </>}

          {tab === 'insert' && <>
            <Group cap="عناصر">
              <Big onRun={insertBody} label="المحتوى"><Type size={20} /></Big>
              <Big onRun={insertAttachments} label="جدول المرفقات"><TableIcon size={20} /></Big>
              <Big onRun={() => { saveSelection(); logoInputRef.current?.click() }} label="الشعار"><ImageIcon size={20} /></Big>
              <Big onRun={() => insertSeal('signature')} label="التوقيع"><PenLine size={20} /></Big>
              <Big onRun={() => insertSeal('stamp')} label="الختم"><Stamp size={20} /></Big>
            </Group>
            <Group cap="جداول وصور">
              <button type="button" className="rb-big" onMouseDown={pd} onClick={(e) => {
                const r = e.currentTarget.getBoundingClientRect(); saveSelection()
                setTablePicker({ open: true, x: Math.max(8, Math.min(r.left, window.innerWidth - 240)), y: r.bottom + 4, r: 0, c: 0 })
              }}>
                <span className="rb-big__ic"><TableIcon size={20} /></span><span className="rb-big__lb">جدول</span>
              </button>
              <Big onRun={() => imgInputRef.current?.click()} label="صورة"><ImageIcon size={20} /></Big>
              <Act onRun={insertLink} title="رابط"><LinkIcon size={16} /></Act>
              <Cmd cmd="formatBlock" val="<blockquote>" title="اقتباس"><Quote size={16} /></Cmd>
              <Cmd cmd="insertHorizontalRule" title="خط فاصل"><Minus size={16} /></Cmd>
            </Group>
            <Group cap="ملف">
              {/* Most letterheads already exist as .docx; this is the way in for them. */}
              <Big onRun={() => docxInputRef.current?.click()} label="من ملف Word"><FileUp size={20} /></Big>
            </Group>
            <Group cap="نص">
              <Big onRun={insertDate} label="التاريخ"><Calendar size={20} /></Big>
              <Big onRun={insertPageBreak} label="فاصل الصفحات"><Minus size={20} /></Big>
            </Group>
            <Group cap="رمز">
              {SYMBOLS.map((s) => <button key={s} type="button" className="rb-btn" title={`إدراج ${s}`} onMouseDown={pd} onClick={() => insertHtml(s, false)}>{s}</button>)}
              <span className="rb-glyph" title="رمز"><Sigma size={16} /></span>
            </Group>
          </>}

          {tab === 'layout' && <>
            <Group cap="خلفية الصفحة">
              <Big onRun={() => setWmDialogOpen(true)} label="علامة مائية" active={wmType !== 'none'}><Droplet size={20} /></Big>
              <Big onRun={() => { setWmType('none'); setWmImage(null) }} label="إزالة العلامة"><Trash2 size={20} /></Big>
              {/* Word's «لون الصفحة»: the swatch picks it, the ✕ returns to plain paper. */}
              <label className="rb-big rb-color" title="لون الصفحة">
                <span className="rb-big__ic"><PaintBucket size={20} /></span>
                <span className="rb-big__lb">لون الصفحة</span>
                <input type="color" value={pageBg ?? '#ffffff'} onChange={(e) => setPageBg(e.target.value)} />
              </label>
              <Act onRun={() => setPageBg(null)} title="بلا لون للصفحة"><X size={16} /></Act>
              <Big onRun={() => openBorders('page')} label="حدود الصفحة" active={!!pageBorder}><Square size={20} /></Big>
            </Group>
            <Group cap="إعداد الصفحة">
              <label className="rb-field"><span>الاتجاه</span>
                <select className="rd-tb__sel rd-tb__sel--sm" value={orientation} onChange={(e) => setOrientation(e.target.value as 'portrait' | 'landscape')}>
                  <option value="portrait">عمودي</option><option value="landscape">أفقي</option>
                </select></label>
              <label className="rb-field"><span>الحجم</span>
                <select className="rd-tb__sel rd-tb__sel--sm" value={pageSize} onChange={(e) => setPageSize(e.target.value as 'A4' | 'Letter')}>
                  <option value="A4">A4</option><option value="Letter">Letter</option>
                </select></label>
            </Group>
          </>}

          {tab === 'view' && <>
            <Group cap="تكبير/تصغير">
              <Act onRun={() => setZoom((z) => Math.max(0.5, Math.round((z - 0.25) * 100) / 100))} title="تصغير"><ZoomOut size={16} /></Act>
              <select className="rd-tb__sel rd-tb__sel--sm" value={zoom} onChange={(e) => setZoom(Number(e.target.value))} aria-label="التكبير">
                {ZOOMS.map((z) => <option key={z} value={z}>{Math.round(z * 100)}%</option>)}
              </select>
              <Act onRun={() => setZoom((z) => Math.min(1.5, Math.round((z + 0.25) * 100) / 100))} title="تكبير"><ZoomIn size={16} /></Act>
            </Group>
            <Group cap="إظهار">
              <Big onRun={() => setShowFields((v) => !v)} label="قائمة الحقول" active={showFields}><PanelRight size={20} /></Big>
              <Big onRun={() => setHighlight((v) => !v)} label="تمييز الحقول" active={highlight}><Highlighter size={20} /></Big>
            </Group>
          </>}

          <div className="rb-spacer" />
          <Group cap="معاينة">
            <Big onRun={preview} label="معاينة الطباعة"><Printer size={20} /></Big>
          </Group>
        </div>

        <div className={`rd-body${showFields ? '' : ' rd-body--nofields'}`}>
          <div className="rd-page-wrap">
            <div className="rd-paper" style={paperStyle}>
              {/* The sheets are drawn behind the text; the editable itself is transparent, so one
                  continuous editing surface still looks like a stack of pages. */}
              <div className="rd-sheets" aria-hidden>
                {Array.from({ length: pageCount }, (_, i) => (
                  <div key={i} className="rd-sheet" style={sheetStyle}>
                    {frameStyle && <span className="rd-sheet__frame" style={frameStyle} />}
                    <span className="rd-sheet__no">صفحة {i + 1} من {pageCount}</span>
                  </div>
                ))}
              </div>
              <div
                ref={editorRef}
                className={`rd-page rich${highlight ? ' rd-page--hl' : ''}${painter ? ' rd-page--painter' : ''}`}
                dir="rtl"
                contentEditable
                suppressContentEditableWarning
                onKeyUp={saveSelection}
                onMouseDown={clickToType}
                onMouseUp={onEditorMouseUp}
                onBlur={saveSelection}
                /* Text arriving without a keystroke — an IME, dictation, a paste — also means the
                   user has moved on from the picture, and the keydown release never fires for it. */
                onInput={() => { if (!dragRef.current) { selectImage(null); selectElement(null) } scanItems(); schedulePaginate() }}
                onContextMenu={onContextMenu}
              />
            </div>
          </div>

          {showFields && (
            <aside className="rd-fields">
              <div className="rd-fields__head">الحقول</div>
              <p className="rd-fields__hint">انقر لإدراج الحقل في موضع المؤشر.</p>
              {/* The letter itself. `body` is what the sender typed in the compose screen and
                  `attachments` the files they attached — both arrive at print time, so the design
                  only marks where they go. They are whole blocks rather than inline chips, hence
                  their own group and their own look. */}
              <div className="rd-fields__group">
                <div className="rd-fields__title">محتوى الكتاب</div>
                <div className="rd-fields__list">
                  <button type="button" className="rd-fieldbtn rd-fieldbtn--block" onMouseDown={pd} onClick={insertBody}>
                    <Type size={13} aria-hidden /> المحتوى (نص الكتاب)
                  </button>
                  <button type="button" className="rd-fieldbtn rd-fieldbtn--block" onMouseDown={pd} onClick={insertAttachments}>
                    <TableIcon size={13} aria-hidden /> جدول المرفقات
                  </button>
                </div>
              </div>

              {FIELD_GROUPS.map((g) => (
                <div key={g.title} className="rd-fields__group">
                  <div className="rd-fields__title">{g.title}</div>
                  <div className="rd-fields__list">
                    {g.tokens.map((tk) => (
                      <button key={tk} type="button" className="rd-fieldbtn" onMouseDown={pd} onClick={() => insertField(tk)}>{TOKEN_LABEL[tk] ?? tk}</button>
                    ))}
                  </div>
                </div>
              ))}
            </aside>
          )}
        </div>
      </div>

      {/* The selected element: outlined, named, and removable from the badge. Click-through, so
          the caret and the text underneath keep working normally. */}
      {elBox && (
        <div className={`rd-elsel${dragging ? ' is-dragging' : ''}`}
          style={{ left: elBox.left, top: elBox.top, width: elBox.w, height: elBox.h }}>
          <span className="rd-elsel__tag">
            {/* The grip is how a table is moved: inside one, the pointer belongs to the cell text. */}
            <button type="button" className="rd-elsel__grip" title="اسحب لنقل العنصر" aria-label={`نقل ${elBox.label}`}
              onPointerDown={(e) => {
                const node = selElRef.current
                if (!node) return
                e.preventDefault(); e.stopPropagation()
                beginElementDrag(node, e, true)
              }}>
              <GripVertical size={12} />
            </button>
            {elBox.label}
            <button type="button" className="rd-elsel__fmt" title="تنسيق العنصر" aria-label={`تنسيق ${elBox.label}`}
              onMouseDown={(e) => e.preventDefault()} onClick={openFormatPanel}>
              <Palette size={12} />
            </button>
            <button type="button" className="rd-elsel__x" title="حذف العنصر" aria-label={`حذف ${elBox.label}`}
              onMouseDown={(e) => e.preventDefault()} onClick={deleteSelectedElement}>
              <Trash2 size={12} />
            </button>
          </span>
        </div>
      )}

      {fmtPanel && (elBox || imgBox) && (
        <ElementFormatPanel
          label={fmtPanel.label} initial={fmtPanel.initial} isPicture={fmtPanel.isPicture}
          at={{ left: (elBox ?? imgBox)!.left, top: (elBox ?? imgBox)!.top }}
          onApply={(css) => styleSelected(css)}
          onClose={() => setFmtPanel(null)} />
      )}

      {/* Where the dragged element would land: a caret-height bar between two letters for an
          inline element, or a full-width rule above/below a paragraph for a block one. */}
      {dropMark && (
        <div className="rd-dropmark"
          style={{ left: dropMark.left, top: dropMark.top, width: dropMark.w, height: dropMark.h }} />
      )}

      {/* Resize handles for the selected picture, drawn over the canvas but outside it. */}
      {imgBox && (
        <div className={`rd-imgsel${movable ? ' rd-imgsel--movable' : ''}`}
          style={{ left: imgBox.left, top: imgBox.top, width: imgBox.w, height: imgBox.h }}
          onPointerDown={startMove} onPointerMove={onMoveDrag} onPointerUp={endMove} onPointerCancel={endMove}
          onContextMenu={(e) => { if (selImgRef.current) onContextMenu(e, selImgRef.current) }}>
          <span className="rd-imgsel__size">{Math.round(imgBox.w / zoom)} × {Math.round(imgBox.h / zoom)} بكسل</span>
          <span className="rd-imgsel__hint">Shift + الأسهم للتحجيم · Ctrl + الأسهم خطوة دقيقة</span>
          <button type="button" className="rd-imgsel__fmt" title="تنسيق الصورة" aria-label="تنسيق الصورة"
            onPointerDown={(e) => e.stopPropagation()} onClick={openFormatPanel}>
            <Palette size={12} /> تنسيق
          </button>
          {IMG_HANDLES.map((h) => (
            <span key={h.k} className={`rd-imgsel__h rd-imgsel__h--${h.k}`}
              onPointerDown={(e) => startResize(e, h.dx, h.dy)}
              onPointerMove={onResizeMove}
              onPointerUp={endResize}
              onPointerCancel={endResize} />
          ))}
        </div>
      )}

      <input ref={imgInputRef} type="file" accept="image/*" style={{ display: 'none' }} onChange={onInsertImageFile} />
      <input ref={logoInputRef} type="file" accept="image/*" style={{ display: 'none' }} onChange={onLogoPick} />
      <input ref={docxInputRef} type="file" accept=".docx" style={{ display: 'none' }} onChange={onDocxPick} />

      <WatermarkDialog open={wmDialogOpen} initial={wmDraft} onCommit={commitWm} onClose={() => setWmDialogOpen(false)} />

      {bordersOpen && (
        <BordersDialog tab={bordersTab} initial={bordersDraft}
          onApply={applyBorders} onClose={() => setBordersOpen(false)} />
      )}

      {menu.open && <>
        <div className="rd-ctx-scrim" onMouseDown={closeMenu} onContextMenu={(e) => { e.preventDefault(); closeMenu() }} />
        <div className="rd-ctxwrap" style={{ left: menu.x, top: menu.y }}>
          {/* Word-style floating mini formatting toolbar */}
          <div className="rd-mini" onMouseDown={(e) => { const t = (e.target as HTMLElement).tagName; if (t !== 'SELECT' && t !== 'OPTION') e.preventDefault() }}>
            <select className="rd-mini__sel" defaultValue="" aria-label="الخط" onChange={(e) => { const v = e.target.value; e.currentTarget.selectedIndex = 0; if (v) exec('fontName', v) }}>
              <option value="" disabled>الخط</option>
              {FONTS.map((f) => <option key={f} value={f} style={{ fontFamily: f }}>{f}</option>)}
            </select>
            <select className="rd-mini__sel rd-mini__sel--sm" value={curSize} aria-label="الحجم" onChange={(e) => { setCurSize(e.target.value); exec('fontSize', e.target.value) }}>
              {SIZES.map((s) => <option key={s.v} value={s.v}>{s.label}</option>)}
            </select>
            <button type="button" className="rd-mini__b" title="تكبير الخط" onClick={() => stepFont(1)}><span className="rb-a">A˄</span></button>
            <button type="button" className="rd-mini__b" title="تصغير الخط" onClick={() => stepFont(-1)}><span className="rb-a rb-a--sm">A˅</span></button>
            <button type="button" className="rd-mini__b" title="نسخ التنسيق" onClick={copyFormat}><Paintbrush size={15} /></button>
            <span className="rd-mini__sep" />
            <button type="button" className="rd-mini__b" title="عريض" onClick={() => exec('bold')}><Bold size={15} /></button>
            <button type="button" className="rd-mini__b" title="مائل" onClick={() => exec('italic')}><Italic size={15} /></button>
            <button type="button" className="rd-mini__b" title="تسطير" onClick={() => exec('underline')}><Underline size={15} /></button>
            <label className="rd-mini__b rb-color" title="تظليل"><Baseline size={15} /><input type="color" onMouseDown={(e) => e.stopPropagation()} onChange={(e) => exec('hiliteColor', e.target.value)} /></label>
            <label className="rd-mini__b rb-color" title="لون النص"><Type size={15} /><input type="color" onMouseDown={(e) => e.stopPropagation()} onChange={(e) => exec('foreColor', e.target.value)} /></label>
            <span className="rd-mini__sep" />
            <button type="button" className="rd-mini__b" title="قائمة نقطية" onClick={() => exec('insertUnorderedList')}><List size={15} /></button>
            <button type="button" className="rd-mini__b" title="قائمة رقمية" onClick={() => exec('insertOrderedList')}><ListOrdered size={15} /></button>
          </div>

          <ul className="rd-ctx" role="menu">
            {menu.rid && <>
              <li role="menuitem" className="rd-ctx__item rd-ctx__item--danger" onMouseDown={(e) => e.preventDefault()} onClick={() => menuAct(() => removeItem(menu.rid!))}>
                <Trash2 size={15} /><span>حذف «{menu.label}»</span></li>
              <li className="rd-ctx__sep" />
            </>}
            {menu.isImg && <>
              {/* Word's «التفاف النص», opened in place rather than as a hover flyout — the menu is
                  already positioned against the window edge, and a flyout would run off it. */}
              <li role="menuitem" className="rd-ctx__item" aria-expanded={wrapOpen}
                onMouseDown={(e) => e.preventDefault()} onClick={() => setWrapOpen((v) => !v)}>
                <WrapText size={15} /><span>التفاف النص</span>
                <ChevronLeft size={14} className={`rd-ctx__caret${wrapOpen ? ' is-open' : ''}`} />
              </li>
              {wrapOpen && WRAP_MODES.map((w) => (
                <li key={w.k} role="menuitem" className={`rd-ctx__item rd-ctx__item--sub${selImgRef.current && wrapOf(selImgRef.current) === w.k ? ' is-on' : ''}`}
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => menuAct(() => { const im = selImgRef.current; if (im) setWrap(im, w.k) })}>
                  <Check size={14} className="rd-ctx__tick" /><span>{w.label}</span>
                </li>
              ))}
              <li role="menuitem" className="rd-ctx__item" onMouseDown={(e) => e.preventDefault()} onClick={() => menuAct(() => sizeImage('natural'))}><ImageIcon size={15} /><span>الحجم الأصلي للصورة</span></li>
              <li role="menuitem" className="rd-ctx__item" onMouseDown={(e) => e.preventDefault()} onClick={() => menuAct(() => sizeImage('full'))}><ImageIcon size={15} /><span>بعرض الصفحة</span></li>
              <li className="rd-ctx__sep" />
            </>}
            {menu.inTable && <>
              <li role="menuitem" className="rd-ctx__item" onMouseDown={(e) => e.preventDefault()} onClick={() => menuAct(() => tableInsertRow('above'))}><TableIcon size={15} /><span>إدراج صف بالأعلى</span></li>
              <li role="menuitem" className="rd-ctx__item" onMouseDown={(e) => e.preventDefault()} onClick={() => menuAct(() => tableInsertRow('below'))}><TableIcon size={15} /><span>إدراج صف بالأسفل</span></li>
              <li role="menuitem" className="rd-ctx__item" onMouseDown={(e) => e.preventDefault()} onClick={() => menuAct(() => tableInsertCol('right'))}><TableIcon size={15} /><span>إدراج عمود لليمين</span></li>
              <li role="menuitem" className="rd-ctx__item" onMouseDown={(e) => e.preventDefault()} onClick={() => menuAct(() => tableInsertCol('left'))}><TableIcon size={15} /><span>إدراج عمود لليسار</span></li>
              <li role="menuitem" className="rd-ctx__item rd-ctx__item--danger" onMouseDown={(e) => e.preventDefault()} onClick={() => menuAct(tableDeleteRow)}><Trash2 size={15} /><span>حذف الصف</span></li>
              <li role="menuitem" className="rd-ctx__item rd-ctx__item--danger" onMouseDown={(e) => e.preventDefault()} onClick={() => menuAct(tableDeleteCol)}><Trash2 size={15} /><span>حذف العمود</span></li>
              <li className="rd-ctx__sep" />
            </>}
            <li role="menuitem" className="rd-ctx__item" onMouseDown={(e) => e.preventDefault()} onClick={() => menuAct(() => exec('cut'))}><Scissors size={15} /><span>قص</span></li>
            <li role="menuitem" className="rd-ctx__item" onMouseDown={(e) => e.preventDefault()} onClick={() => menuAct(() => exec('copy'))}><Copy size={15} /><span>نسخ</span></li>
            <li role="menuitem" className="rd-ctx__item" onMouseDown={(e) => e.preventDefault()} onClick={() => menuAct(doPaste)}><ClipboardPaste size={15} /><span>لصق</span></li>
            <li className="rd-ctx__sep" />
            <li role="menuitem" className="rd-ctx__item" onMouseDown={(e) => e.preventDefault()} onClick={() => menuAct(insertLink)}><LinkIcon size={15} /><span>ارتباط تشعبي…</span></li>
            <li role="menuitem" className="rd-ctx__item" onMouseDown={(e) => e.preventDefault()} onClick={() => menuAct(() => exec('removeFormat'))}><RemoveFormatting size={15} /><span>مسح التنسيق</span></li>
          </ul>
        </div>
      </>}

      {tablePicker.open && <>
        <div className="rd-ctx-scrim" onMouseDown={() => setTablePicker((p) => ({ ...p, open: false }))} />
        <div className="rd-tpick" style={{ left: tablePicker.x, top: tablePicker.y }}>
          <div className="rd-tpick__grid" onMouseLeave={() => setTablePicker((p) => ({ ...p, r: 0, c: 0 }))}>
            {Array.from({ length: 8 }).map((_, ri) => Array.from({ length: 8 }).map((_, ci) => {
              const r = ri + 1, c = ci + 1
              const on = r <= tablePicker.r && c <= tablePicker.c
              return <button key={`${r}-${c}`} type="button" className={`rd-tpick__cell${on ? ' is-on' : ''}`}
                onMouseEnter={() => setTablePicker((p) => ({ ...p, r, c }))}
                onClick={() => insertTableSized(r, c)} />
            }))}
          </div>
          <div className="rd-tpick__lbl">{tablePicker.r > 0 ? `${tablePicker.r} × ${tablePicker.c} جدول` : 'اختر حجم الجدول'}</div>
        </div>
      </>}

    </div>
  )
}
