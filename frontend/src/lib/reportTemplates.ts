import { api } from './api'
import { account } from './account'
import { attachScreenGuard, SCREEN_GUARD_CSS } from './screenGuard'

// ── Scope / target enums (mirror backend) ──
export type TemplateScope = 0 | 1 | 2   // Default | OrgUnit | User
export type TemplateTarget = 0 | 1 | 2  // Correspondence | Document | All
export const SCOPE_LABEL: Record<string, string> = { Default: 'افتراضي (كل المؤسسة)', OrgUnit: 'قسم/إدارة', User: 'مستخدم' }
export const TARGET_LABEL: Record<string, string> = { Correspondence: 'المراسلات', Document: 'الوثائق', All: 'الكل' }
export const SCOPES: { v: TemplateScope; label: string }[] = [
  { v: 0, label: 'افتراضي (كل المؤسسة)' }, { v: 1, label: 'قسم/إدارة' }, { v: 2, label: 'مستخدم' },
]
export const TARGETS: { v: TemplateTarget; label: string }[] = [
  { v: 2, label: 'الكل' }, { v: 0, label: 'المراسلات' }, { v: 1, label: 'الوثائق' },
]

// ── Designer blocks ──
export type Align = 'start' | 'center' | 'end'
export type Block =
  | { type: 'logo'; align: Align; width: number }
  | { type: 'header'; text: string; align: Align; fontSize: number; bold: boolean; color?: string }
  | { type: 'text'; text: string; align: Align }
  | { type: 'fields'; columns: number; items: { label: string; token: string }[] }
  | { type: 'table'; title?: string; columns: { header: string; token: string }[] }
  | { type: 'divider' }
  | { type: 'spacer'; height: number }
  | { type: 'footer'; text: string; align: Align }

export const BLOCK_LABEL: Record<Block['type'], string> = {
  logo: 'الشعار', header: 'ترويسة / عنوان', text: 'نص', fields: 'حقول (بيانات)',
  table: 'جدول', divider: 'خط فاصل', spacer: 'مسافة', footer: 'تذييل',
}

// Placeholder tokens available in text/header/fields. Values are supplied at render time.
export const TOKENS: { token: string; label: string }[] = [
  { token: 'institutionName', label: 'اسم المؤسسة' },
  { token: 'number', label: 'رقم المعاملة/الكتاب' },
  { token: 'subject', label: 'الموضوع' },
  { token: 'date', label: 'التاريخ' },
  { token: 'sentDate', label: 'تاريخ الإرسال' },
  { token: 'recipient', label: 'الجهة المستلمة' },
  { token: 'recipientName', label: 'اسم المستلم' },
  { token: 'sender', label: 'الجهة المرسِلة' },
  { token: 'senderName', label: 'اسم المرسِل' },
  { token: 'confidentiality', label: 'السرية' },
  { token: 'priority', label: 'الأولوية' },
  { token: 'classification', label: 'تصنيف البريد' },
  { token: 'raisedTo', label: 'مرفوع إلى' },
  { token: 'fileReference', label: 'اسم الملف/الصندوق' },
  { token: 'keywords', label: 'كلمات مفتاحية' },
  { token: 'body', label: 'المحتوى (نص الكتاب)' },
]

export interface ReportTemplate {
  id: number
  institutionId: number
  scope: string          // 'Default' | 'OrgUnit' | 'User'
  orgUnitId: number | null
  orgUnitName: string | null
  userId: number | null
  userName: string | null
  target: string         // 'Correspondence' | 'Document' | 'All'
  /** Narrowed to one document type (Document target) — null = every type. */
  documentTypeId: number | null
  documentTypeName: string | null
  /** Narrowed to one mail category (Correspondence target) — null = every category. */
  mailCategoryId: number | null
  mailCategoryName: string | null
  name: string
  logoBase64: string | null
  layoutJson: string
  isActive: boolean
  /** Whether this caller may change it — false for the institution defaults, which are the
   *  system administrator's. A design is only ever visible to its owner or its own department. */
  canEdit: boolean
}

export interface SaveReportTemplate {
  scope: TemplateScope
  orgUnitId?: number | null
  userId?: number | null
  target: TemplateTarget
  name: string
  logoBase64?: string | null
  layoutJson: string
  isActive?: boolean
  documentTypeId?: number | null
  mailCategoryId?: number | null
}

export interface ResolvedTemplate {
  templateId: number | null
  name: string
  logoBase64: string | null
  layoutJson: string
  source: string   // User | OrgUnit | Default | None
  /** True when the chosen design was tied to the printed type rather than being the scope's catch-all. */
  matchedType: boolean
}

export const reportTemplates = {
  /** The saved designs. Narrow with `documentTypeId`/`mailCategoryId` to see what would apply to
   *  one kind of item — the server returns that type's designs plus the catch-alls. */
  list: (params?: {
    scope?: TemplateScope
    target?: TemplateTarget
    documentTypeId?: number | null
    mailCategoryId?: number | null
  }) => api.get<ReportTemplate[]>('/report-templates', { params }).then((r) => r.data),
  get: (id: number) => api.get<ReportTemplate>(`/report-templates/${id}`).then((r) => r.data),
  create: (b: SaveReportTemplate) => api.post<ReportTemplate>('/report-templates', b).then((r) => r.data),
  update: (id: number, b: SaveReportTemplate) => api.put<ReportTemplate>(`/report-templates/${id}`, b).then((r) => r.data),
  remove: (id: number) => api.delete(`/report-templates/${id}`),
  /** Which design applies to a print job. Omit `orgUnitId` and the server uses the user's own
   *  department, so a department letterhead applies without the caller looking it up. */
  resolve: (params: {
    target?: TemplateTarget
    orgUnitId?: number | null
    userId?: number | null
    documentTypeId?: number | null
    mailCategoryId?: number | null
  }) => api.get<ResolvedTemplate>('/report-templates/resolve', { params }).then((r) => r.data),
}

// ── Rendering (shared by the designer preview and the print popup) ──

const esc = (s: string) => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]!))
const applyTokens = (text: string, tokens: Record<string, string>) =>
  text.replace(/\{\{\s*(\w+)\s*\}\}/g, (_, k) => tokens[k] ?? '')

const TOKEN_RE = /\{\{\s*(\w+)\s*\}\}/g

/* ── Rich-text tokens ──────────────────────────────────────────────────────────
   Letter bodies come from the editor as HTML and must print with their formatting
   intact. That HTML is stored server-side and could be crafted by any caller, so it
   is sanitised to a whitelist before it reaches the print window — tags, attributes
   and individual style properties are all filtered. */

const RICH_TAGS = new Set([
  'P', 'BR', 'SPAN', 'DIV', 'B', 'STRONG', 'I', 'EM', 'U', 'S', 'STRIKE', 'DEL', 'MARK',
  'SUB', 'SUP', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'UL', 'OL', 'LI', 'BLOCKQUOTE',
  'A', 'HR', 'IMG', 'TABLE', 'THEAD', 'TBODY', 'TFOOT', 'TR', 'TH', 'TD', 'CODE', 'PRE',
])

/** Presentation-only properties — nothing that can load a resource or escape the page. */
const SAFE_STYLE = new Set([
  'color', 'background-color', 'background', 'font-size', 'font-family', 'font-weight',
  'font-style', 'text-align', 'text-decoration', 'text-decoration-line', 'line-height',
  'vertical-align', 'direction', 'padding-inline-start', 'padding-left', 'padding-right',
  'margin-inline-start', 'width', 'height', 'break-before', 'page-break-before', 'break-after', 'page-break-after',
  // Text wrapping around a picture (التفاف النص): floats, a line of its own, or pinned
  // behind/in front of the text. `position` is filtered to in-page values below.
  'float', 'clear', 'display', 'position', 'left', 'top', 'z-index', 'max-width',
  'margin', 'margin-left', 'margin-right', 'margin-top', 'margin-bottom', 'margin-inline-end',
  // Borders and shading (الحدود والتظليل) on a paragraph or a table cell.
  'border', 'border-width', 'border-style', 'border-color', 'border-radius', 'box-shadow',
  'border-top', 'border-right', 'border-bottom', 'border-left', 'padding', 'padding-top',
  'padding-bottom',
  // Written by the element formatting panel.
  'opacity', 'letter-spacing',
])

/** `fixed`/`sticky` would pin content to the viewport and repeat it on every printed sheet. */
const SAFE_POSITION = /^\s*(static|relative|absolute)\s*$/

const attrsFor = (tag: string): Set<string> => {
  if (tag === 'A') return new Set(['href', 'title'])
  if (tag === 'IMG') return new Set(['src', 'alt', 'width', 'height'])
  if (tag === 'TD' || tag === 'TH') return new Set(['colspan', 'rowspan'])
  if (tag === 'OL') return new Set(['start', 'type'])
  return new Set()
}

/** Keep only whitelisted style declarations, and only if the value loads nothing. */
function safeStyle(value: string): string {
  return value.split(';')
    .map((d) => d.trim())
    .filter((d) => {
      const i = d.indexOf(':')
      if (i < 1) return false
      const prop = d.slice(0, i).trim().toLowerCase()
      const val = d.slice(i + 1).toLowerCase()
      if (!SAFE_STYLE.has(prop)) return false
      if (prop === 'position' && !SAFE_POSITION.test(val)) return false
      return !/url\s*\(|expression\s*\(|javascript:|@import/.test(val)
    })
    .join('; ')
}

/** Sanitise editor HTML for printing: whitelist tags/attributes, drop scripts and handlers.
 *  `allowTokens` keeps the designer's merge-field markup (`class`, `data-token`) — inert
 *  presentation attributes — so a template's chips survive a round-trip through the editor. */
export function sanitizeRichHtml(html: string, opts?: { allowTokens?: boolean }): string {
  if (!html) return ''
  const doc = new DOMParser().parseFromString(html, 'text/html')
  const keepTokenAttrs = opts?.allowTokens === true

  const walk = (node: Element) => {
    for (const child of [...node.children]) {
      walk(child)
      if (!RICH_TAGS.has(child.tagName)) {
        // Unwrap unknown elements so their text survives; drop executable ones entirely.
        if (child.tagName === 'SCRIPT' || child.tagName === 'STYLE') child.remove()
        else child.replaceWith(...Array.from(child.childNodes))
        continue
      }
      const allowed = attrsFor(child.tagName)
      for (const attr of [...child.attributes]) {
        const name = attr.name.toLowerCase()
        if (name === 'style') {
          const cleaned = safeStyle(attr.value)
          if (cleaned) child.setAttribute('style', cleaned)
          else child.removeAttribute('style')
          continue
        }
        // Merge-field bookkeeping (never scriptable) is preserved for the designer only.
        if (keepTokenAttrs && (name === 'class' || name === 'data-token')) continue
        if (!allowed.has(name)) { child.removeAttribute(attr.name); continue }
        // Only inert URL schemes may stay on href/src.
        if (name === 'href' || name === 'src') {
          const v = attr.value.trim().toLowerCase()
          const ok = name === 'src'
            ? v.startsWith('data:image/') || v.startsWith('http://') || v.startsWith('https://')
            : v.startsWith('http://') || v.startsWith('https://') || v.startsWith('mailto:') || v.startsWith('#')
          if (!ok) child.removeAttribute(attr.name)
        }
      }
    }
  }
  walk(doc.body)
  return doc.body.innerHTML
}

export interface RenderCtx {
  tokens: Record<string, string>
  logo: string | null
  /** The sender's signature (توقيع) and stamp (ختم), stamped into the design's placeholders.
   *  Absent when the sender set none — the placeholder is then dropped, not left blank. */
  signature?: string | null
  stamp?: string | null
  /** Rows for the first `table` block, e.g. attachments: [{ name, size }] mapped by column token. */
  tableRows?: Record<string, string>[]
  /** Tokens whose value is rich HTML (e.g. a letter body) — sanitised, then injected unescaped. */
  htmlTokens?: Record<string, string>
}

/** Tokens that stand for a picture rather than text, with the Arabic label shown while designing. */
export const IMAGE_TOKENS: Record<string, string> = {
  logo: 'الشعار',
  signature: 'التوقيع',
  stamp: 'الختم',
}

/** The image a picture token resolves to for this print job, or null when it is unset. */
export function imageTokenSrc(token: string, ctx: RenderCtx): string | null {
  if (token === 'logo') return ctx.logo ?? null
  if (token === 'signature') return ctx.signature ?? null
  if (token === 'stamp') return ctx.stamp ?? null
  return null
}

/** Render an ordered block list to an HTML string. Values are token-substituted then escaped. */
export function renderReportHtml(blocks: Block[], ctx: RenderCtx): string {
  const alignCss = (a: Align) => `text-align:${a === 'start' ? 'right' : a === 'end' ? 'left' : 'center'}`
  const tok = (s: string) => esc(applyTokens(s, ctx.tokens))
  const parts = blocks.map((b) => {
    switch (b.type) {
      case 'logo':
        return ctx.logo
          ? `<div style="${alignCss(b.align)}"><img src="${ctx.logo}" style="width:${b.width}px;max-width:100%;object-fit:contain" /></div>`
          : `<div style="${alignCss(b.align)};color:#9ca3af;font-size:12px">[شعار]</div>`
      case 'header':
        return `<div style="${alignCss(b.align)};font-size:${b.fontSize}px;font-weight:${b.bold ? 700 : 400}${b.color ? `;color:${b.color}` : ''};margin:2px 0">${tok(b.text)}</div>`
      case 'text': {
        // A body token carries editor HTML; splice it in sanitised so bold/colour/lists survive.
        if (ctx.htmlTokens && TOKEN_RE.test(b.text)) {
          TOKEN_RE.lastIndex = 0
          let rich = false
          const html = b.text.replace(TOKEN_RE, (_, k: string) => {
            const raw = ctx.htmlTokens?.[k]
            if (raw === undefined) return esc(ctx.tokens[k] ?? '')
            rich = true
            return sanitizeRichHtml(raw)
          })
          if (rich) {
            return `<div class="rich" style="${alignCss(b.align)};font-size:14px;line-height:1.9;margin:6px 0">${html}</div>`
          }
        }
        const html = tok(b.text).replace(/\n/g, '<br/>')
        return `<div style="${alignCss(b.align)};font-size:14px;line-height:1.9;margin:6px 0;white-space:pre-wrap">${html}</div>`
      }
      case 'fields': {
        const cells = b.items.map((it) =>
          `<div style="display:flex;gap:6px;padding:3px 0"><span style="color:#6b7280;min-width:90px">${esc(it.label)}:</span><span style="font-weight:600">${tok('{{' + it.token + '}}')}</span></div>`).join('')
        return `<div style="display:grid;grid-template-columns:repeat(${Math.max(1, b.columns)},1fr);gap:2px 24px;margin:6px 0;font-size:13px">${cells}</div>`
      }
      case 'table': {
        const head = b.columns.map((c) => `<th style="border:1px solid #d1d5db;padding:6px;background:#f3f4f6;font-size:12px">${esc(c.header)}</th>`).join('')
        const rows = (ctx.tableRows ?? []).map((row) =>
          `<tr>${b.columns.map((c) => `<td style="border:1px solid #d1d5db;padding:6px;font-size:12px">${esc(row[c.token] ?? '')}</td>`).join('')}</tr>`).join('')
        return `${b.title ? `<div style="font-weight:600;margin:8px 0 4px">${esc(b.title)}</div>` : ''}<table style="border-collapse:collapse;width:100%;margin:4px 0"><thead><tr>${head}</tr></thead><tbody>${rows || `<tr><td colspan="${b.columns.length}" style="border:1px solid #d1d5db;padding:6px;color:#9ca3af;font-size:12px">—</td></tr>`}</tbody></table>`
      }
      case 'divider':
        return `<hr style="border:none;border-top:1px solid #cbb68a;margin:10px 0"/>`
      case 'spacer':
        return `<div style="height:${b.height}px"></div>`
      case 'footer':
        return `<div style="${alignCss(b.align)};font-size:12px;color:#6b7280;border-top:1px solid #e5e7eb;margin-top:14px;padding-top:8px">${tok(b.text)}</div>`
    }
  })
  return parts.join('\n')
}

/* ── Rich (Word-style) document ─────────────────────────────────────────────────
   The designer now authors a free WYSIWYG page (contentEditable HTML) instead of an
   ordered block list. Merge fields live in the markup as atomic chips carrying a
   `data-token`; at render time each chip is swapped for its real value, the logo <img>
   gets the resolved logo, `body` is spliced in as sanitised rich HTML, and `attachments`
   expands to a table of `tableRows`. The finished HTML is sanitised before it prints. */

/** Build the attachments table injected wherever the `attachments` field was placed. */
function buildAttachmentsTable(rows: Record<string, string>[]): string {
  const head = `<tr><th style="border:1px solid #d1d5db;padding:6px;background:#f3f4f6">الاسم</th><th style="border:1px solid #d1d5db;padding:6px;background:#f3f4f6">الحجم</th></tr>`
  const body = rows.length
    ? rows.map((r) => `<tr><td style="border:1px solid #d1d5db;padding:6px">${esc(r.name ?? '')}</td><td style="border:1px solid #d1d5db;padding:6px">${esc(r.size ?? '')}</td></tr>`).join('')
    : `<tr><td colspan="2" style="border:1px solid #d1d5db;padding:6px;color:#9ca3af">—</td></tr>`
  return `<table style="border-collapse:collapse;width:100%;margin:6px 0">${head}${body}</table>`
}

/** Render a WYSIWYG template (editor HTML) to a print-ready HTML string. */
export function renderRichDoc(html: string, ctx: RenderCtx): string {
  const doc = new DOMParser().parseFromString(html, 'text/html')

  doc.querySelectorAll('[data-token]').forEach((el) => {
    const tk = el.getAttribute('data-token') || ''
    // Image placeholders: the logo, plus the sender's signature and stamp. An unset image drops
    // its placeholder rather than printing an empty box or a broken-image icon.
    if (el.tagName === 'IMG' && tk in IMAGE_TOKENS) {
      const src = imageTokenSrc(tk, ctx)
      if (src) { el.setAttribute('src', src); el.removeAttribute('data-token') }
      else el.replaceWith(doc.createTextNode(''))
      return
    }
    /* Whatever the designer styled the placeholder with — its font, colour, frame, alignment —
       belongs to the printed value, not to the placeholder. Dropping the element and keeping only
       its text is what used to lose all of it, so the style rides along on the replacement. */
    const styled = el.getAttribute('style')

    if (tk === 'body' || tk === 'attachments') {
      const wrap = doc.createElement('div')
      wrap.innerHTML = tk === 'attachments'
        ? buildAttachmentsTable(ctx.tableRows ?? [])
        : ctx.htmlTokens?.body !== undefined
          ? sanitizeRichHtml(ctx.htmlTokens.body)
          : esc(ctx.tokens.body ?? '').replace(/\n/g, '<br/>')
      if (styled) { wrap.setAttribute('style', styled); el.replaceWith(wrap) }
      else el.replaceWith(...Array.from(wrap.childNodes))
      return
    }

    // A plain field: the chip becomes its value — as a styled span when it carries styling of
    // its own, and as bare text otherwise, so an untouched design prints exactly as before.
    const value = ctx.tokens[tk] ?? ''
    if (styled) {
      const span = doc.createElement('span')
      span.setAttribute('style', styled)
      span.textContent = value
      el.replaceWith(span)
    } else {
      el.replaceWith(doc.createTextNode(value))
    }
  })

  // Any tokens the user typed as literal text rather than inserting as chips.
  const out = applyTokens(doc.body.innerHTML, ctx.tokens)
  return `<div class="rich">${sanitizeRichHtml(out)}</div>`
}

// ── Page setup ─────────────────────────────────────────────────────────────────
/** A frame around the whole page — Word's «حدود الصفحة». Each edge can be dropped. */
export interface PageBorder {
  style: 'solid' | 'dashed' | 'dotted' | 'double'
  width: number                       // pt
  color: string
  edges: { top: boolean; right: boolean; bottom: boolean; left: boolean }
  /** Word's "Shadow" setting: the frame plus a drop shadow on two sides. */
  shadow?: boolean
}

export interface PageSetup {
  size: 'A4' | 'Letter'
  orientation: 'portrait' | 'landscape'
  /** Page colour (لون الصفحة). Printed, so it needs print-color-adjust. */
  bg?: string | null
  border?: PageBorder | null
}
export const DEFAULT_PAGE: PageSetup = { size: 'A4', orientation: 'portrait' }

/** The CSS a page border resolves to, shared by the designer's sheets and the printed page. */
export function pageBorderCss(b: PageBorder | null | undefined): string {
  if (!b) return ''
  const line = `${b.width}pt ${b.style} ${b.color}`
  const side = (on: boolean, name: string) => (on ? `border-${name}:${line};` : '')
  return side(b.edges.top, 'top') + side(b.edges.right, 'right')
    + side(b.edges.bottom, 'bottom') + side(b.edges.left, 'left')
    + (b.shadow ? `box-shadow:3px 3px 0 0 ${b.color};` : '')
}

// ── Watermark ──────────────────────────────────────────────────────────────────
export interface Watermark {
  type: 'text' | 'image'
  text?: string
  image?: string      // data:image/... URL
  color?: string
  font?: string       // font-family for a text watermark
  opacity?: number    // 0–1
  size?: number       // px (font size for text, box size for image)
  rotate?: number     // degrees (0 = horizontal, -45 = diagonal)
}

const escXml = (s: string) => s.replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&apos;' }[c]!))

/** A square SVG data-URL for the watermark — used as a page background in the editor
 *  and as a centred image behind the text when printing. Opacity/rotation are baked in. */
export function watermarkSvg(wm: Watermark): string {
  const S = 1000, c = S / 2
  const op = Math.min(0.9, Math.max(0.02, wm.opacity ?? 0.15))
  const rot = wm.rotate ?? -30
  let inner = ''
  if (wm.type === 'text') {
    const size = wm.size ?? 72
    const color = wm.color ?? '#9ca3af'
    const font = escXml(wm.font || "'Segoe UI',Tahoma,sans-serif")
    inner = `<text x="${c}" y="${c}" fill="${escXml(color)}" fill-opacity="${op}" font-size="${size}" font-family="${font}" font-weight="700" text-anchor="middle" dominant-baseline="middle" transform="rotate(${rot} ${c} ${c})">${escXml(wm.text || '')}</text>`
  } else if (wm.type === 'image' && (wm.image || '').startsWith('data:image/')) {
    const size = wm.size ?? 420
    inner = `<image href="${wm.image}" x="${c - size / 2}" y="${c - size / 2}" width="${size}" height="${size}" opacity="${op}" preserveAspectRatio="xMidYMid meet" transform="rotate(${rot} ${c} ${c})"/>`
  }
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${S}" height="${S}" viewBox="0 0 ${S} ${S}">${inner}</svg>`
  return 'data:image/svg+xml;utf8,' + encodeURIComponent(svg)
}

/** A stored layout is either the new rich HTML (`{v,html,watermark,page}`) or a legacy block array. */
export function parseLayout(layoutJson: string):
  | { kind: 'rich'; html: string; watermark?: Watermark; page?: PageSetup }
  | { kind: 'blocks'; blocks: Block[] } {
  try {
    const p = JSON.parse(layoutJson)
    if (p && typeof p === 'object' && !Array.isArray(p) && typeof p.html === 'string') {
      const wm = p.watermark && typeof p.watermark === 'object' ? (p.watermark as Watermark) : undefined
      const page = p.page && typeof p.page === 'object' ? (p.page as PageSetup) : undefined
      return { kind: 'rich', html: p.html, watermark: wm, page }
    }
    if (Array.isArray(p)) return { kind: 'blocks', blocks: p as Block[] }
  } catch { /* fall through */ }
  return { kind: 'blocks', blocks: DEFAULT_LAYOUT }
}

/** Render any stored layout (rich or legacy blocks) to a print-ready HTML string. */
export function renderLayout(layoutJson: string, ctx: RenderCtx): string {
  const parsed = parseLayout(layoutJson)
  return parsed.kind === 'rich' ? renderRichDoc(parsed.html, ctx) : renderReportHtml(parsed.blocks, ctx)
}

/**
 * What an output window is for.
 *   `print`   — go straight to the browser's print dialog (needs Correspondence/Documents.Print).
 *   `preview` — show the letter on screen only. The print dialog is never opened and, when the
 *               viewer has no Print right, printing is disabled inside the window as well.
 */
export type OutputMode = 'print' | 'preview'

/** Every preview reuses this one window, so repeated clicks replace the sheet on screen instead of
 *  stacking up popups — one design, one preview. Only the report designer previews; correspondence
 *  goes straight to the printer. */
const PREVIEW_WINDOW_NAME = 'archivingPrintPreview'
let previewWindow: Window | null = null
let previewGuardOff: (() => void) | null = null

export interface OutputOptions {
  mode?: OutputMode
  /** Suppress printing from inside the preview window (viewer may preview but not print). */
  allowPrint?: boolean
  /** Arm the screen-capture guard on the opened window. */
  guard?: boolean
}

/**
 * Put the rendered letterhead on screen — or straight into the printer's hands.
 *
 * Printing renders into a hidden frame on the current page and opens the platform's print dialog
 * over it. It used to open a window first, which put the document on screen twice: once in a popup
 * and again in the dialog's own preview panel, and the popup was still sitting there as a blank
 * window after the dialog closed. The dialog is the only screen printing needs.
 *
 * A preview has no dialog to fall back on, so it keeps its window.
 */
function openOutputWindow(
  inner: string, docTitle: string, watermark?: Watermark, page?: PageSetup, opts: OutputOptions = {},
) {
  const mode = opts.mode ?? 'print'
  const allowPrint = opts.allowPrint ?? mode === 'print'
  const docHtml = buildPrintDocument(inner, docTitle, watermark, page, {
    autoPrint: mode === 'print', allowPrint, guard: !!opts.guard,
  })

  if (mode === 'print') { printViaFrame(docHtml); return }

  const w = acquirePreviewWindow()
  write(w, docHtml, opts)
  w.focus()
}

/**
 * The single reusable preview window, opened on first use. Pressing معاينة again replaces the
 * sheet already on screen instead of stacking another popup beside it.
 * Throws when the popup is blocked — a blocked preview must not fail silently.
 */
function acquirePreviewWindow(): Window {
  previewGuardOff?.()
  previewGuardOff = null
  const w = previewWindow && !previewWindow.closed
    ? previewWindow
    : (previewWindow = window.open('', PREVIEW_WINDOW_NAME, 'width=900,height=1000'))
  if (!w) throw new Error('preview-window-blocked')
  return w
}

/**
 * Open the preview window *now*, showing a placeholder until the real content is ready.
 *
 * Resolving a design and fetching a seal are awaits, and by the time they finish the click's
 * user-activation has expired — `window.open` would be blocked. Printing survives that via the
 * hidden-iframe fallback; a preview cannot, so it has to claim its window while the click is
 * still fresh. Safe to call and then not follow up: the window is reused, never stacked.
 */
export function warmPreviewWindow() {
  writePreviewMessage('جارٍ تحضير المعاينة…')
}

/**
 * Replace the placeholder with an error.
 *
 * Every path that calls warmPreviewWindow() must call this if it then fails, otherwise the window
 * sits on "جارٍ تحضير المعاينة…" forever — telling the user it is still working when it gave up.
 * A toast behind the preview window is not enough: the preview is the focused window, so the
 * toast is the one thing the user cannot see.
 */
export function failPreviewWindow(message: string) {
  writePreviewMessage(message, true)
}

/** Paper dimensions in millimetres, so the preview can show a sheet at its true proportions. */
const PAPER_MM: Record<PageSetup['size'], [number, number]> = { A4: [210, 297], Letter: [216, 279] }

/** CSS px per millimetre at the CSS reference resolution (96dpi) — how a sheet is sized on screen. */
const PX_PER_MM = 96 / 25.4

/**
 * The preview window's chrome: the toolbar, and the desk the paper sits on.
 *
 * `color-scheme` is the one line that hands the window to the operating system — scrollbars, form
 * controls and the window's own backdrop then follow whatever light/dark the OS is set to, and the
 * desk follows with it. The paper never does: paper is white under every theme, and a preview that
 * dimmed it would be lying about what comes out of the printer.
 */
const PV_CHROME_CSS = `
  :root { color-scheme: light dark; }
  .pv-bar { position: sticky; top: 0; z-index: 5; display: flex; align-items: center; gap: 8px;
    padding: 7px 12px; background: #0f172a; color: #e2e8f0; user-select: none;
    font: 13px/1.6 'Segoe UI', Tahoma, sans-serif;
    border-block-end: 1px solid #1e293b; box-shadow: 0 1px 8px rgba(0,0,0,.35); }
  .pv-bar__t { font-weight: 600; max-inline-size: 38vw; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .pv-bar__gap { flex: 1; }
  .pv-bar__note { color: #fbbf24; }
  .pv-bar__chip { color: #94a3b8; border: 1px solid #1e293b; border-radius: 999px; padding: 2px 10px; white-space: nowrap; }
  .pv-bar__b { cursor: pointer; border: 1px solid #334155; background: #1e293b; color: inherit;
    border-radius: 7px; padding: 5px 11px; font: inherit; display: inline-flex; align-items: center; gap: 6px; }
  .pv-bar__b:hover { background: #273449; }
  .pv-bar__b:focus-visible { outline: 2px solid #38bdf8; outline-offset: 2px; }
  .pv-bar__b--primary { background: #0ea5e9; border-color: #0ea5e9; color: #052c3d; font-weight: 700; }
  .pv-bar__b--primary:hover { background: #38bdf8; }
  .pv-bar__b--ghost { background: transparent; }
  .pv-zoom { display: inline-flex; align-items: center; border: 1px solid #334155; border-radius: 7px; overflow: hidden; }
  .pv-zoom .pv-bar__b { border: 0; border-radius: 0; padding: 5px 10px; }
  .pv-zoom__v { min-inline-size: 3.4em; text-align: center; font-variant-numeric: tabular-nums; color: #cbd5e1; }
  /* None of the chrome is part of the document — it must never reach paper. */
  @media print { .pv-bar { display: none !important; } }
`

/** The desk, the sheet on it, and the margins that make the sheet match what prints. */
function pvStageCss(pg: PageSetup): string {
  const [w, h] = PAPER_MM[pg.size] ?? PAPER_MM.A4
  const land = pg.orientation === 'landscape'
  return `
  @media screen {
    body.pv { margin: 0; padding: 0; background: #71717a; }
    @media (prefers-color-scheme: dark) { body.pv { background: #27272a; } }
    .pv-stage { display: flex; justify-content: center; padding: 22px 0 46px; }
    .pv-paper { zoom: var(--pv-zoom, 1); position: relative; background: #fff;
      inline-size: ${land ? h : w}mm; min-block-size: ${land ? w : h}mm;
      box-shadow: 0 3px 16px rgba(0,0,0,.45); }
    /* The same margins the printer will use. The preview used to lay the sheet out at a screen
       width with screen padding, so line breaks on screen were not the line breaks on paper —
       which is the one thing a preview exists to show. */
    body.pv .sheet { max-inline-size: none; padding: ${pg.border ? '10mm' : '18mm 16mm'}; margin: ${pg.border ? '8mm' : '0'}; }
    /* Fixed would pin it to the window; inside the preview the watermark belongs to the sheet. */
    body.pv .watermark { position: absolute; }
  }`
}

/** Toolbar for a rendered letterhead: print, zoom, page, close. */
function pvBar(docTitle: string, allowPrint: boolean, pg: PageSetup | null, zoomable: boolean): string {
  // The paper chip is only honest for something we laid out ourselves; a scanned PDF carries its
  // own page size, which we do not know here.
  const page = pg ? `${pg.size} · ${pg.orientation === 'landscape' ? 'أفقي' : 'عمودي'}` : ''
  return `<div class="pv-bar">
      <span class="pv-bar__t">${esc(docTitle)} — معاينة</span>
      <span class="pv-bar__gap"></span>
      ${zoomable ? `<span class="pv-zoom">
        <button type="button" class="pv-bar__b" data-z="out" title="تصغير (Ctrl و -)" aria-label="تصغير">−</button>
        <button type="button" class="pv-bar__b pv-zoom__v" data-z="reset" title="حجم طبيعي (Ctrl و 0)" aria-label="حجم طبيعي">100٪</button>
        <button type="button" class="pv-bar__b" data-z="in" title="تكبير (Ctrl و +)" aria-label="تكبير">+</button>
      </span>
      <button type="button" class="pv-bar__b" data-z="fit" title="ملاءمة عرض النافذة">⤢ ملاءمة</button>` : ''}
      ${page ? `<span class="pv-bar__chip">${esc(page)}</span>` : ''}
      ${allowPrint
        ? `<button type="button" class="pv-bar__b pv-bar__b--primary" data-print="1" title="طباعة (Ctrl و P)">⎙ طباعة</button>`
        : `<span class="pv-bar__note">معاينة فقط — لا تملك صلاحية الطباعة</span>`}
      <button type="button" class="pv-bar__b pv-bar__b--ghost" data-close="1" title="إغلاق (Esc)">✕ إغلاق</button>
    </div>`
}

/**
 * Behaviour for the toolbar, wired to the shortcuts the operating system's own document viewers
 * use: Ctrl +/−/0 to zoom, Ctrl with the wheel to zoom, Esc to close, Ctrl+P to print. Ctrl+wheel
 * and Ctrl± are taken over deliberately — in a window that holds one document and nothing else,
 * zooming the page is what the user meant, not zooming the chrome around it.
 */
function pvScript(paperMm: number): string {
  return `<script>(function(){
    var paperPx = ${Math.round(paperMm * PX_PER_MM)}, z = 1, label = document.querySelector('.pv-zoom__v');
    function apply(){ document.documentElement.style.setProperty('--pv-zoom', z);
      if (label) label.textContent = Math.round(z*100) + '\\u066A'; }
    function set(v){ z = Math.min(3, Math.max(0.25, v)); apply(); }
    function fit(){ var s = document.querySelector('.pv-stage');
      if (s) set((s.clientWidth - 48) / paperPx); }
    document.addEventListener('click', function(e){
      var b = e.target.closest && e.target.closest('[data-z],[data-print],[data-close]');
      if (!b) return;
      if (b.dataset.print) return window.print();
      if (b.dataset.close) return window.close();
      var a = b.dataset.z;
      if (a === 'in') set(z + 0.1); else if (a === 'out') set(z - 0.1);
      else if (a === 'reset') set(1); else if (a === 'fit') fit();
    });
    document.addEventListener('keydown', function(e){
      if (e.key === 'Escape') { window.close(); return; }
      if (!(e.ctrlKey || e.metaKey)) return;
      if (e.key === '+' || e.key === '=') { e.preventDefault(); set(z + 0.1); }
      else if (e.key === '-') { e.preventDefault(); set(z - 0.1); }
      else if (e.key === '0') { e.preventDefault(); set(1); }
    });
    document.addEventListener('wheel', function(e){
      if (!(e.ctrlKey || e.metaKey)) return;
      e.preventDefault(); set(z + (e.deltaY < 0 ? 0.1 : -0.1));
    }, { passive: false });
    // A sheet wider than the window opens fitted — otherwise the first thing the reader meets is
    // a page cut off at the edge and a horizontal scrollbar.
    var s = document.querySelector('.pv-stage');
    if (s && s.clientWidth - 48 < paperPx) fit(); else apply();
    window.addEventListener('resize', function(){ if (z < 1) fit(); });
  })()</script>`
}

function writePreviewMessage(message: string, isError = false) {
  try {
    const w = acquirePreviewWindow()
    w.document.open()
    /* The window the reader is looking at while the design and the seal are still being fetched.
       It shows the sheet it is about to fill rather than an empty dark screen, so the wait reads
       as a document arriving instead of as a window that failed to load. */
    w.document.write(`<!doctype html><html dir="rtl" lang="ar"><head><meta charset="utf-8"/>
      <title>معاينة</title><style>
        :root { color-scheme: light dark; }
        body { margin: 0; min-block-size: 100vh; display: grid; place-items: center; padding: 24px;
          background: #71717a; font: 15px 'Segoe UI', Tahoma, sans-serif; text-align: center; }
        @media (prefers-color-scheme: dark) { body { background: #27272a; } }
        .ph { inline-size: min(420px, 86vw); padding: 34px 26px; border-radius: 10px; background: #fff;
          color: ${isError ? '#b45309' : '#475569'}; box-shadow: 0 3px 16px rgba(0,0,0,.45); }
        .ph__dots { display: flex; justify-content: center; gap: 6px; margin-block-end: 16px; }
        .ph__dots i { inline-size: 8px; block-size: 8px; border-radius: 50%; background: #cbd5e1;
          animation: ph 1s ease-in-out infinite; }
        .ph__dots i:nth-child(2) { animation-delay: .15s } .ph__dots i:nth-child(3) { animation-delay: .3s }
        @keyframes ph { 50% { background: #64748b; transform: translateY(-3px) } }
        .ph__b { cursor: pointer; margin-block-start: 16px; border: 1px solid #cbd5e1; background: #f8fafc;
          color: #334155; border-radius: 7px; padding: 6px 14px; font: inherit; }
        @media (prefers-reduced-motion: reduce) { .ph__dots i { animation: none } }
      </style></head><body><div class="ph">
      ${isError ? '' : '<div class="ph__dots"><i></i><i></i><i></i></div>'}
      <p style="margin:0">${esc(message)}</p>
      ${isError ? '<button class="ph__b" onclick="window.close()">✕ إغلاق</button>' : ''}
      </div></body></html>`)
    w.document.close()
  } catch {
    // Blocked popup — the caller's own open attempt reports it.
  }
}

/**
 * Preview a file (the merged scan of a received letter) in that same window.
 *
 * Separate from the letterhead path because there is nothing to render: the bytes are shown in a
 * frame, and the toolbar prints *that frame*, not the wrapper — `window.print()` on the outer
 * document would print the toolbar and an empty page in most browsers.
 */
export function previewDocumentUrl(
  url: string, docTitle: string, opts: { allowPrint?: boolean; guard?: boolean } = {},
) {
  const w = acquirePreviewWindow()
  const allowPrint = opts.allowPrint ?? true
  /* No zoom controls and no paper here: the bytes are a PDF, and the built-in viewer inside the
     frame brings the platform's own zoom, page navigation and scrolling. Adding a second set
     above it would leave two zoom levels multiplying each other. */
  const html = `<!doctype html><html dir="rtl" lang="ar"><head><meta charset="utf-8"/>
    <title>${esc(docTitle)} — معاينة</title><style>
      html, body { height: 100%; margin: 0; }
      body { display: flex; flex-direction: column; background: #71717a; }
      @media (prefers-color-scheme: dark) { body { background: #27272a; } }
      ${PV_CHROME_CSS}
      .pv-bar { position: static; }
      .pv-frame { flex: 1; border: 0; width: 100%; }
      ${opts.guard ? SCREEN_GUARD_CSS : ''}
    </style></head><body>
    ${pvBar(docTitle, allowPrint, null, false)}
    <iframe id="pv" class="pv-frame" src="${esc(url)}"></iframe>
    <script>document.addEventListener('click', function(e){
      var b = e.target.closest && e.target.closest('[data-print],[data-close]');
      if (!b) return;
      if (b.dataset.close) return window.close();
      // Print the frame, not the wrapper: window.print() here would send the toolbar and a
      // blank page to the printer in most browsers.
      var f = document.getElementById('pv'); f.contentWindow.focus(); f.contentWindow.print();
    });
    document.addEventListener('keydown', function(e){ if (e.key === 'Escape') window.close(); });</script>
    </body></html>`
  write(w, html, { mode: 'preview', allowPrint, guard: opts.guard })
  w.focus()
}

function write(w: Window, docHtml: string, opts: OutputOptions) {
  // Re-previewing an edited design would otherwise throw the reader back to the top of the page.
  const scroll = (() => { try { return w.scrollY } catch { return 0 } })()
  w.document.open()
  w.document.write(docHtml)
  w.document.close()
  // Restored twice: once now, and once after the logo and any embedded images have settled and
  // changed the page height.
  if (scroll > 0) {
    const restore = () => { try { w.scrollTo(0, scroll) } catch { /* window replaced or closed */ } }
    restore()
    setTimeout(restore, 60)
  }
  if (!opts.guard) return
  // Same-origin (about:blank), so the guard can be armed directly rather than inlined as a script.
  const off = attachScreenGuard(w.document, { blockPrint: !(opts.allowPrint ?? opts.mode !== 'preview') })
  if ((opts.mode ?? 'print') === 'preview') previewGuardOff = off
}

/** Close any open preview window — used when leaving the screen that opened it. */
export function closePreviewWindow() {
  previewGuardOff?.()
  previewGuardOff = null
  if (previewWindow && !previewWindow.closed) previewWindow.close()
  previewWindow = null
}

/** Print a legacy block layout. */
export function printReport(blocks: Block[], ctx: RenderCtx, docTitle = 'طباعة') {
  openOutputWindow(renderReportHtml(blocks, ctx), docTitle)
}

/** Render a stored layout string (rich or legacy) to a print or preview window. */
export function printLayout(layoutJson: string, ctx: RenderCtx, docTitle = 'طباعة', opts: OutputOptions = {}) {
  const parsed = parseLayout(layoutJson)
  const inner = parsed.kind === 'rich' ? renderRichDoc(parsed.html, ctx) : renderReportHtml(parsed.blocks, ctx)
  openOutputWindow(
    inner, docTitle,
    parsed.kind === 'rich' ? parsed.watermark : undefined,
    parsed.kind === 'rich' ? parsed.page : undefined,
    opts,
  )
}

/** Renders the document into an off-screen iframe and fires its print dialog. */
function printViaFrame(html: string) {
  const frame = document.createElement('iframe')
  frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;'
  document.body.appendChild(frame)
  const doc = frame.contentDocument
  if (!doc) { frame.remove(); return }
  doc.open(); doc.write(html); doc.close()
  // The document's own onload script calls print(); clean the frame up afterwards.
  setTimeout(() => frame.remove(), 60_000)
}

interface PrintDocOptions {
  /** Fire the browser print dialog as soon as the document loads. */
  autoPrint?: boolean
  /** False for a preview the viewer is not allowed to print — adds the banner and hides the button. */
  allowPrint?: boolean
  /** Inline the screen-capture guard's stylesheet (the behaviour is armed by the opener). */
  guard?: boolean
}

function buildPrintDocument(
  inner: string, docTitle: string, watermark?: Watermark, page?: PageSetup, opts: PrintDocOptions = {},
) {
  const wmLayer = watermark ? `<div class="watermark"><img src="${watermarkSvg(watermark)}" alt=""/></div>` : ''
  const pg = page ?? DEFAULT_PAGE
  const autoPrint = opts.autoPrint ?? true
  const allowPrint = opts.allowPrint ?? true
  // A preview carries its own toolbar: printing from here when allowed, and a plain statement of
  // why the button is missing when it is not.
  const bar = autoPrint ? '' : pvBar(docTitle, allowPrint, pg, true)
  const [pw, ph] = PAPER_MM[pg.size] ?? PAPER_MM.A4
  const paperMm = pg.orientation === 'landscape' ? ph : pw
  return `<!doctype html><html dir="rtl" lang="ar"><head><meta charset="utf-8"/><title>${esc(docTitle)}</title>
    <style>
      /* margin:0 is what removes the browser's own date/title header and page footer: they are
         drawn inside the page margin, so leaving room for them is exactly what makes them
         appear. The page margins move onto the body below instead. */
      @page { size: ${pg.size} ${pg.orientation}; margin: 0; }
      ${autoPrint ? '' : PV_CHROME_CSS + pvStageCss(pg)}
      ${opts.guard ? SCREEN_GUARD_CSS : ''}
      .pagebreak { break-before: page; page-break-before: always; height: 0; }
      body { font-family: 'Segoe UI', Tahoma, sans-serif; color:#1f2937; margin:0; padding:24px 0;${pg.bg ? ` background:${pg.bg};` : ''} }
      /* The margins live on the sheet, not on the body: a picture set to "behind/in front of
         text" is positioned against its containing block, and that block has to be the sheet —
         the same relationship the designer's page has — or the picture would land elsewhere
         on paper than it did on screen. */
      .sheet { max-width: 780px; margin: 0 auto; position: relative; z-index: 1; padding: 0 34px;
        ${pg.bg ? `background:${pg.bg};` : ''}${pageBorderCss(pg.border)} }
      /* A frame stands off the paper edge (Word's default is 24pt) and holds the text off itself,
         and a page colour has to reach the paper — browsers drop backgrounds from print unless
         told otherwise. */
      ${pg.border ? '.sheet { margin: 24px auto; padding: 20px 34px; }' : ''}
      ${pg.bg || pg.border ? 'html, body, .sheet { -webkit-print-color-adjust: exact; print-color-adjust: exact; }' : ''}
      /* Watermark sits behind the text and repeats on every printed page. */
      .watermark { position: fixed; inset: 0; z-index: 0; display: flex; align-items: center; justify-content: center; pointer-events: none; }
      .watermark img { width: 70%; max-width: 520px; }
      /* Editor content keeps its own formatting; these only supply the defaults
         the editor relied on from the app stylesheet. */
      .rich p { margin: 0 0 .55em; }
      .rich h1 { font-size: 20pt; margin: .4em 0 .3em; }
      .rich h2 { font-size: 17pt; margin: .4em 0 .3em; }
      .rich h3 { font-size: 14pt; margin: .4em 0 .3em; }
      .rich h4 { font-size: 12.5pt; margin: .4em 0 .3em; }
      .rich ul, .rich ol { padding-inline-start: 1.6em; margin: .4em 0; }
      .rich ul { list-style: disc; }
      .rich ol { list-style: decimal; }
      .rich blockquote { padding-inline-start: 1em; border-inline-start: 3px solid #cbd5e1; color:#475569; margin:.5em 0; }
      .rich hr { border:none; border-top:1px solid #cbd5e1; margin:1em 0; }
      .rich img { max-width: 100%; height: auto; }
      .rich table { border-collapse: collapse; width: 100%; margin:.6em 0; }
      .rich th, .rich td { border: 1px solid #d1d5db; padding: 6px 8px; }
      .rich th { background:#f3f4f6; font-weight:700; }
      .rich mark { padding: 0 .1em; }
      /* Print in full colour and avoid splitting a paragraph across pages. */
      @media print {
        /* The margins @page no longer carries. Left/right hold on every sheet; top and bottom
           apply to the first and last sheet only, which is how block padding paginates. */
        body { padding: 0; }
        /* A framed page keeps the frame clear of the paper edge — Word draws it that way, and a
           line on the very edge falls inside the printer's own unprintable border. The text then
           sits inside the frame rather than against it. */
        .sheet { max-width: none; padding: ${pg.border ? '10mm' : '18mm 16mm'};${pg.border ? ' margin: 8mm;' : ''} }
        .rich, .rich * { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
        .watermark, .watermark * { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
        .rich p, .rich li, .rich tr { break-inside: avoid; }
      }
    </style></head><body${autoPrint ? '' : ' class="pv"'}>${bar}${
      // In preview the sheet sits on a desk, as a page of the size it will print at. When printing
      // there is no desk and no paper — the paper is real — so the sheet is emitted bare, exactly
      // as before: nothing about what reaches the printer changes.
      autoPrint
        ? `${wmLayer}<div class="sheet">${inner}</div>`
        : `<div class="pv-stage"><div class="pv-paper">${wmLayer}<div class="sheet">${inner}</div></div></div>`
    }
    ${autoPrint
      ? `<script>window.onload=function(){setTimeout(function(){window.print()},150)}</script>`
      : pvScript(paperMm)}
    </body></html>`
}

/** Resolve the applicable design (User → OrgUnit → Default) and open the print dialog with it. */
export async function printWithTemplate(opts: {
  target: TemplateTarget
  /** `print` fires the print dialog; `preview` renders into the reusable preview window. */
  mode?: OutputMode
  /** Let the viewer print from inside a preview window. Defaults to "only in print mode". */
  allowPrint?: boolean
  /** Arm the screen-capture guard on the opened window (sensitive letters). */
  guard?: boolean
  orgUnitId?: number | null
  userId?: number | null
  /** The document type being printed — picks that type's letterhead when one is defined. */
  documentTypeId?: number | null
  /** The mail category being printed — picks that category's letterhead when one is defined. */
  mailCategoryId?: number | null
  /** Whose signature/stamp to seal the letter with — the sender. Defaults to `userId`. */
  sealUserId?: number | null
  tokens: Record<string, string>
  overrideLogo?: string | null
  tableRows?: Record<string, string>[]
  /** Tokens carrying editor HTML (e.g. `body`) — printed with their formatting intact. */
  htmlTokens?: Record<string, string>
  title?: string
}) {
  // The seal belongs to whoever sent the letter, not whoever is at the printer — a clerk
  // printing someone else's outgoing letter must reproduce that person's signature. Falls back
  // to the current user when the letter has no identified sender.
  const sealUserId = opts.sealUserId ?? opts.userId ?? null
  const [resolved, seal] = await Promise.all([
    reportTemplates.resolve({
      target: opts.target, orgUnitId: opts.orgUnitId, userId: opts.userId,
      documentTypeId: opts.documentTypeId, mailCategoryId: opts.mailCategoryId,
    }),
    // A missing seal must never stop a print job, so a failed lookup prints unsealed.
    sealUserId != null
      ? account.seal(sealUserId).catch(() => null)
      : Promise.resolve(null),
  ])
  const layoutJson = resolved.layoutJson?.trim() ? resolved.layoutJson : JSON.stringify(DEFAULT_LAYOUT)
  printLayout(layoutJson, {
    tokens: opts.tokens,
    logo: opts.overrideLogo || resolved.logoBase64,
    signature: seal?.signatureUrl ?? null,
    stamp: seal?.stampUrl ?? null,
    tableRows: opts.tableRows,
    htmlTokens: opts.htmlTokens,
  }, opts.title, { guard: opts.guard, mode: opts.mode, allowPrint: opts.allowPrint })
  return resolved
}

/** Friendly Arabic label per token (used by the designer's field chips). */
export const TOKEN_LABEL: Record<string, string> = Object.fromEntries(TOKENS.map((t) => [t.token, t.label] as [string, string]))

/** Markup for one merge-field chip — an atomic, non-editable span the renderer swaps for a value. */
export function tokenChip(token: string, label = TOKEN_LABEL[token] ?? token): string {
  return `<span class="rd-tok" contenteditable="false" data-token="${token}">${esc(label)}</span>`
}

/** A blank page for a brand-new design. Not literally empty: a contentEditable with no block has
 *  nowhere to put the caret, so the first keystroke behaves erratically — one empty paragraph gives
 *  the cursor a home while still showing the designer a clean sheet. */
export const EMPTY_RICH_HTML = '<p><br></p>'

/** The letterhead printing falls back to when a print job resolves to no saved design at all —
 *  never used to seed the designer, which starts blank. */
export const DEFAULT_LAYOUT: Block[] = [
  { type: 'logo', align: 'center', width: 90 },
  { type: 'header', text: '{{institutionName}}', align: 'center', fontSize: 20, bold: true },
  { type: 'divider' },
  { type: 'fields', columns: 2, items: [
    { label: 'الرقم', token: 'number' }, { label: 'التاريخ', token: 'date' },
    { label: 'الموضوع', token: 'subject' }, { label: 'التصنيف', token: 'classification' },
  ] },
  { type: 'spacer', height: 8 },
  { type: 'text', text: '{{body}}', align: 'start' },
  { type: 'spacer', height: 24 },
  { type: 'footer', text: '{{institutionName}} — وثيقة رسمية', align: 'center' },
]
