// npm install @tiptap/react @tiptap/pm @tiptap/starter-kit @tiptap/extensions \
//   @tiptap/extension-text-style @tiptap/extension-highlight @tiptap/extension-text-align \
//   @tiptap/extension-image @tiptap/extension-table @tiptap/extension-table-row \
//   @tiptap/extension-table-header @tiptap/extension-table-cell
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useEditor, EditorContent, Extension, type Editor } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import { Placeholder } from '@tiptap/extensions'
import { TextStyleKit } from '@tiptap/extension-text-style'
import Highlight from '@tiptap/extension-highlight'
import TextAlign from '@tiptap/extension-text-align'
import { Image } from '@tiptap/extension-image'
import { Table, TableRow, TableCell, TableHeader } from '@tiptap/extension-table'
import { useTranslation } from 'react-i18next'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter,
} from './ui/dialog'
import {
  ContextMenu, ContextMenuTrigger, ContextMenuContent, ContextMenuItem,
  ContextMenuSeparator, ContextMenuSub, ContextMenuSubTrigger, ContextMenuSubContent,
  ContextMenuShortcut,
} from './ui/context-menu'
import { useConfirmDialog } from './ui/confirm-dialog'
import './richtexteditor.css'

/* ────────────────────────────────────────────────────────────────────────────
   Icons — stroke-based, inherit currentColor, sized by the button's font-size
   ──────────────────────────────────────────────────────────────────────────── */

const ICONS: Record<string, ReactNode> = {
  undo: <><path d="M3 8h11a5 5 0 0 1 0 10h-6" /><path d="M7 4 3 8l4 4" /></>,
  redo: <><path d="M21 8H10a5 5 0 0 0 0 10h6" /><path d="m17 4 4 4-4 4" /></>,
  bold: <><path d="M7 4h6a4 4 0 0 1 0 8H7z" /><path d="M7 12h7a4 4 0 0 1 0 8H7z" /></>,
  italic: <><path d="M15 4h-6" /><path d="M15 20H9" /><path d="M14 4 10 20" /></>,
  underline: <><path d="M7 4v7a5 5 0 0 0 10 0V4" /><path d="M5 20h14" /></>,
  strike: <><path d="M4 12h16" /><path d="M16 7a4 3 0 0 0-4-3h-1a3 3 0 0 0-1 5.9" /><path d="M8 17a4 3 0 0 0 4 3h1a3 3 0 0 0 1-5.9" /></>,
  textColor: <><path d="m6 16 5-12 5 12" /><path d="M8 12h6" /></>,
  highlight: <><path d="m9 11 4-4 5 5-4 4z" /><path d="M13 7 16 4l4 4-3 3" /><path d="M9 11 7 17l6-2" /></>,
  alignStart: <><path d="M4 6h16" /><path d="M4 12h10" /><path d="M4 18h13" /></>,
  alignCenter: <><path d="M4 6h16" /><path d="M7 12h10" /><path d="M5 18h14" /></>,
  alignEnd: <><path d="M4 6h16" /><path d="M10 12h10" /><path d="M7 18h13" /></>,
  justify: <><path d="M4 6h16" /><path d="M4 12h16" /><path d="M4 18h16" /></>,
  orderedList: <><path d="M10 6h10" /><path d="M10 12h10" /><path d="M10 18h10" /><path d="M4 5h1v4" /><path d="M4 9h2" /><path d="M4 15h2v2H4v2h2" /></>,
  bulletList: <><path d="M9 6h11" /><path d="M9 12h11" /><path d="M9 18h11" /><circle cx="5" cy="6" r="1.2" /><circle cx="5" cy="12" r="1.2" /><circle cx="5" cy="18" r="1.2" /></>,
  indent: <><path d="M4 6h16" /><path d="M11 12h9" /><path d="M4 18h16" /><path d="m4 10 3 2-3 2z" /></>,
  outdent: <><path d="M4 6h16" /><path d="M11 12h9" /><path d="M4 18h16" /><path d="m7 10-3 2 3 2z" /></>,
  table: <><rect x="3" y="4" width="18" height="16" rx="1.5" /><path d="M3 10h18" /><path d="M9 10v10" /><path d="M15 10v10" /></>,
  image: <><rect x="3" y="4" width="18" height="16" rx="1.5" /><circle cx="8.5" cy="9.5" r="1.5" /><path d="m4 17 5-5 4 4 3-2 4 4" /></>,
  hr: <><path d="M4 12h16" /><path d="M6 7h12" opacity=".4" /><path d="M6 17h12" opacity=".4" /></>,
  rtl: <><path d="M10 4h7" /><path d="M14 4v16" /><path d="M10 4a3 3 0 0 0 0 6h1V4" /><path d="m7 17-3 3 3 3" transform="translate(0,-3)" /></>,
  ltr: <><path d="M7 4h7" /><path d="M10 4v16" /><path d="M7 4a3 3 0 0 0 0 6h1V4" /><path d="m17 14 3 3-3 3" /></>,
  find: <><circle cx="11" cy="11" r="6" /><path d="m20 20-4.5-4.5" /></>,
  clearFormat: <><path d="M8 5h12" /><path d="M13 5 9 19" /><path d="m15 12 6 6" /><path d="m21 12-6 6" /></>,
  cut: <><circle cx="6" cy="6" r="2.5" /><circle cx="6" cy="18" r="2.5" /><path d="M8 7 20 18" /><path d="M8 17 20 6" /></>,
  copy: <><rect x="9" y="9" width="11" height="11" rx="2" /><path d="M5 15V5a2 2 0 0 1 2-2h8" /></>,
  paste: <><path d="M9 4h6v3H9z" /><path d="M8 5H6a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h5" /><path d="M16 5h2a2 2 0 0 1 2 2v3" /><rect x="13" y="13" width="8" height="8" rx="1.5" /></>,
  link: <><path d="M10 13a5 5 0 0 0 7 0l2-2a5 5 0 0 0-7-7l-1 1" /><path d="M14 11a5 5 0 0 0-7 0l-2 2a5 5 0 0 0 7 7l1-1" /></>,
  font: <><path d="m5 18 5-12 5 12" /><path d="M7 13h6" /><path d="M17 18h3" /></>,
  paragraph: <><path d="M13 4v16" /><path d="M17 4v16" /><path d="M17 4H9a5 5 0 0 0 0 10h4" /></>,
  grow: <><path d="m3 18 4-11 4 11" /><path d="M4.5 14h5" /><path d="M17 14v6" /><path d="M14 17h6" /></>,
  shrink: <><path d="m3 18 4-11 4 11" /><path d="M4.5 14h5" /><path d="M14 17h6" /></>,
}

/* These glyphs encode a side (ragged edge, indent arrow, list markers). They are
   drawn left-to-right, so they read backwards in RTL and get mirrored by CSS. */
const DIRECTIONAL_ICONS = new Set(['alignStart', 'alignEnd', 'indent', 'outdent', 'bulletList', 'orderedList'])

function Icon({ name }: { name: keyof typeof ICONS }) {
  return (
    <svg className={`rte__icon${DIRECTIONAL_ICONS.has(name as string) ? ' rte__icon--dir' : ''}`}
      viewBox="0 0 24 24" fill="none" stroke="currentColor"
      strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden focusable="false">
      {ICONS[name]}
    </svg>
  )
}

/* ────────────────────────────────────────────────────────────────────────────
   Types
   ──────────────────────────────────────────────────────────────────────────── */

export interface WordCountStats { words: number; chars: number; pages: number }

export interface RichTextEditorProps {
  value: string
  onChange: (html: string) => void
  placeholder?: string
  dir?: 'rtl' | 'ltr'
  lang?: 'ar' | 'en'
  readOnly?: boolean
  onSave?: (html: string) => void
  onWordCount?: (stats: WordCountStats) => void
}

/* ────────────────────────────────────────────────────────────────────────────
   Document constants — A4 at 96dpi
   ──────────────────────────────────────────────────────────────────────────── */

const PAGE_HEIGHT_PX = 1123
const FONTS_AR = ['Noto Naskh Arabic', 'Amiri', 'Tajawal', 'IBM Plex Sans Arabic']
const FONTS_LATIN = ['Times New Roman', 'Arial', 'Calibri']
const SIZES = [8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 60, 72]
const HEADINGS = [0, 1, 2, 3, 4] as const

/** Word's standard swatch row — kept as CSS variables so themes can restyle them. */
const SWATCHES = [
  'var(--rte-sw-1)', 'var(--rte-sw-2)', 'var(--rte-sw-3)', 'var(--rte-sw-4)',
  'var(--rte-sw-5)', 'var(--rte-sw-6)', 'var(--rte-sw-7)', 'var(--rte-sw-8)',
  'var(--rte-sw-9)', 'var(--rte-sw-10)',
]

/* ────────────────────────────────────────────────────────────────────────────
   Custom extension — per-block indent + direction
   Stored as data-attributes so all styling stays in the stylesheet (no inline styles).
   ──────────────────────────────────────────────────────────────────────────── */

const MAX_INDENT = 8

const BlockAttributes = Extension.create({
  name: 'blockAttributes',
  addGlobalAttributes() {
    return [{
      types: ['paragraph', 'heading'],
      attributes: {
        indent: {
          default: 0,
          parseHTML: (el) => Number(el.getAttribute('data-indent')) || 0,
          renderHTML: (attrs) => (attrs.indent ? { 'data-indent': attrs.indent } : {}),
        },
        blockDir: {
          default: null,
          parseHTML: (el) => el.getAttribute('dir'),
          renderHTML: (attrs) => (attrs.blockDir ? { dir: attrs.blockDir } : {}),
        },
      },
    }]
  },
})

/* ────────────────────────────────────────────────────────────────────────────
   Paste sanitiser — Word's "Keep Text Only + basic format"
   Keeps structure/emphasis, drops every foreign style, class, colour and font.
   ──────────────────────────────────────────────────────────────────────────── */

const ALLOWED_TAGS = new Set([
  'P', 'BR', 'B', 'STRONG', 'I', 'EM', 'U', 'S', 'STRIKE', 'DEL',
  'H1', 'H2', 'H3', 'H4', 'UL', 'OL', 'LI', 'BLOCKQUOTE', 'A', 'HR',
  'TABLE', 'THEAD', 'TBODY', 'TR', 'TH', 'TD',
])

function sanitizePastedHTML(html: string): string {
  const doc = new DOMParser().parseFromString(html, 'text/html')
  const walk = (node: Element) => {
    ;[...node.children].forEach((child) => {
      walk(child)
      if (!ALLOWED_TAGS.has(child.tagName)) {
        child.replaceWith(...Array.from(child.childNodes))
        return
      }
      const href = child.tagName === 'A' ? child.getAttribute('href') : null
      ;[...child.attributes].forEach((a) => child.removeAttribute(a.name))
      if (href) child.setAttribute('href', href)
    })
  }
  walk(doc.body)
  return doc.body.innerHTML
}

/* ────────────────────────────────────────────────────────────────────────────
   Helpers
   ──────────────────────────────────────────────────────────────────────────── */

const IS_WORD_CHAR = /[\p{L}\p{N}_]/u

/** Word behaviour: with no selection a character format applies to the caret's word. */
function markChain(editor: Editor) {
  const chain = editor.chain().focus()
  const { empty, $from } = editor.state.selection
  if (!empty) return chain
  const text = $from.parent.textContent
  const offset = $from.parentOffset
  if (!text) return chain
  let s = offset
  let e = offset
  while (s > 0 && IS_WORD_CHAR.test(text[s - 1])) s--
  while (e < text.length && IS_WORD_CHAR.test(text[e])) e++
  if (s === e) return chain
  const base = $from.start()
  return chain.setTextSelection({ from: base + s, to: base + e })
}

function activeBlockType(editor: Editor): 'heading' | 'paragraph' {
  return editor.isActive('heading') ? 'heading' : 'paragraph'
}

interface Match { from: number; to: number }

function findMatches(editor: Editor, query: string, matchCase: boolean): Match[] {
  const out: Match[] = []
  if (!query) return out
  const needle = matchCase ? query : query.toLowerCase()
  editor.state.doc.descendants((node, pos) => {
    if (!node.isText || !node.text) return
    const hay = matchCase ? node.text : node.text.toLowerCase()
    let i = hay.indexOf(needle)
    while (i !== -1) {
      out.push({ from: pos + i, to: pos + i + query.length })
      i = hay.indexOf(needle, i + needle.length)
    }
  })
  return out
}

/* ────────────────────────────────────────────────────────────────────────────
   Component
   ──────────────────────────────────────────────────────────────────────────── */

export default function RichTextEditor({
  value,
  onChange,
  placeholder,
  dir = 'rtl',
  lang = 'ar',
  readOnly = false,
  onSave,
  onWordCount,
}: RichTextEditorProps) {
  const { t } = useTranslation()
  const pageRef = useRef<HTMLDivElement>(null)
  const [findOpen, setFindOpen] = useState(false)
  const [stats, setStats] = useState<WordCountStats>({ words: 0, chars: 0, pages: 1 })

  const tr = useCallback(
    (key: string, fallbackAr: string, fallbackEn: string) =>
      t(key, { defaultValue: lang === 'ar' ? fallbackAr : fallbackEn }),
    [t, lang],
  )

  /** Western digits everywhere — matches the system-wide Latin-numeral rule. */
  const num = useCallback((n: number) => String(n), [])

  const editor = useEditor({
    editable: !readOnly,
    extensions: [
      StarterKit.configure({
        heading: { levels: [1, 2, 3, 4] },
        link: { openOnClick: false },
      }),
      BlockAttributes,
      TextStyleKit,
      Highlight.configure({ multicolor: true }),
      // `alignments` is an allowlist — setTextAlign() silently no-ops for any value
      // missing from it, and it defaults to left/center/right/justify. This editor
      // uses the logical start/end (they flip with RTL), so they must be listed or
      // the align-start/end buttons do nothing. left/right stay for pasted HTML.
      TextAlign.configure({
        types: ['heading', 'paragraph'],
        alignments: ['start', 'center', 'end', 'justify', 'left', 'right'],
        defaultAlignment: 'start',
      }),
      Image.configure({ allowBase64: true }),
      Table.configure({ resizable: true }), TableRow, TableHeader, TableCell,
      Placeholder.configure({ placeholder: placeholder ?? '' }),
    ],
    content: value,
    onUpdate: ({ editor }) => onChange(editor.getHTML()),
    editorProps: {
      attributes: { class: 'rte__content', dir, lang, spellcheck: 'true' },
      transformPastedHTML: sanitizePastedHTML,
      handleKeyDown: (_view, event) => {
        const mod = event.ctrlKey || event.metaKey
        if (mod && event.key.toLowerCase() === 'h') { event.preventDefault(); setFindOpen(true); return true }
        if (mod && event.key.toLowerCase() === 's') { event.preventDefault(); onSave?.(editorRef.current?.getHTML() ?? ''); return true }
        if (mod && event.key.toLowerCase() === 'y') { event.preventDefault(); editorRef.current?.commands.redo(); return true }
        if (event.key === 'Tab' && !event.shiftKey) {
          const ed = editorRef.current
          if (!ed) return false
          event.preventDefault()
          if (ed.isActive('listItem')) ed.commands.sinkListItem('listItem')
          else ed.commands.insertContent('    ')
          return true
        }
        if (event.key === 'Tab' && event.shiftKey) {
          const ed = editorRef.current
          if (ed?.isActive('listItem')) { event.preventDefault(); ed.commands.liftListItem('listItem'); return true }
        }
        return false
      },
    },
  })

  // handleKeyDown closes over the first render; keep a live handle.
  const editorRef = useRef<Editor | null>(null)
  useEffect(() => { editorRef.current = editor }, [editor])

  // Controlled value: only push when it genuinely diverges (avoids caret jumps).
  useEffect(() => {
    if (editor && value !== editor.getHTML()) editor.commands.setContent(value || '', { emitUpdate: false })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value])

  useEffect(() => { editor?.setEditable(!readOnly) }, [editor, readOnly])

  // Word / character / page counters.
  useEffect(() => {
    if (!editor) return
    const recount = () => {
      const text = editor.state.doc.textBetween(0, editor.state.doc.content.size, ' ', ' ')
      const words = text.trim() ? text.trim().split(/\s+/).length : 0
      const chars = text.length
      const pages = Math.max(1, Math.ceil((pageRef.current?.scrollHeight ?? 0) / PAGE_HEIGHT_PX))
      const next = { words, chars, pages }
      setStats(next)
      onWordCount?.(next)
    }
    recount()
    editor.on('update', recount)
    return () => { editor.off('update', recount) }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editor])

  if (!editor) return null

  return (
    <div className="rte" dir={dir} lang={lang} data-lang={lang}>
      <Ribbon editor={editor} tr={tr} readOnly={readOnly} onOpenFind={() => setFindOpen(true)} />

      <ContextMenu>
        <ContextMenuTrigger asChild disabled={readOnly}>
          <div className="rte__canvas">
            <div className="rte__page" ref={pageRef}>
              <EditorContent editor={editor} />
            </div>
          </div>
        </ContextMenuTrigger>
        <SelectionMenu editor={editor} tr={tr} onOpenFind={() => setFindOpen(true)} />
      </ContextMenu>

      <StatusBar stats={stats} num={num} lang={lang} tr={tr} />

      <FindReplace
        editor={editor} open={findOpen} onOpenChange={setFindOpen} tr={tr}
      />
    </div>
  )
}

/* ────────────────────────────────────────────────────────────────────────────
   Ribbon
   ──────────────────────────────────────────────────────────────────────────── */

type TrFn = (key: string, ar: string, en: string) => string

function Ribbon({
  editor, tr, readOnly, onOpenFind,
}: { editor: Editor; tr: TrFn; readOnly: boolean; onOpenFind: () => void }) {
  const fontFamily = (editor.getAttributes('textStyle').fontFamily as string) ?? ''
  const fontSize = (editor.getAttributes('textStyle').fontSize as string) ?? ''
  const heading = HEADINGS.find((l) => l !== 0 && editor.isActive('heading', { level: l })) ?? 0
  const blockType = activeBlockType(editor)
  const indent = (editor.getAttributes(blockType).indent as number) ?? 0
  const blockDir = (editor.getAttributes(blockType).blockDir as string) ?? null

  const setIndent = (delta: number) => {
    if (editor.isActive('listItem')) {
      if (delta > 0) editor.chain().focus().sinkListItem('listItem').run()
      else editor.chain().focus().liftListItem('listItem').run()
      return
    }
    const next = Math.min(MAX_INDENT, Math.max(0, indent + delta))
    editor.chain().focus().updateAttributes(blockType, { indent: next }).run()
  }

  const btn = (active: boolean) => `rte__btn${active ? ' is-active' : ''}`

  return (
    <div className="rte__ribbon" role="toolbar" aria-label={tr('editor.toolbar', 'شريط الأدوات', 'Toolbar')} aria-disabled={readOnly}>
      {/* History */}
      <div className="rte__group">
        <button type="button" className={btn(false)} disabled={!editor.can().undo()}
          title={tr('editor.undo', 'تراجع (Ctrl+Z)', 'Undo (Ctrl+Z)')} aria-label={tr('editor.undo', 'تراجع', 'Undo')}
          onClick={() => editor.chain().focus().undo().run()}><Icon name="undo" /></button>
        <button type="button" className={btn(false)} disabled={!editor.can().redo()}
          title={tr('editor.redo', 'إعادة (Ctrl+Y)', 'Redo (Ctrl+Y)')} aria-label={tr('editor.redo', 'إعادة', 'Redo')}
          onClick={() => editor.chain().focus().redo().run()}><Icon name="redo" /></button>
      </div>

      {/* Font family / size / heading */}
      <div className="rte__group">
        <select className="rte__select" value={fontFamily}
          title={tr('editor.font', 'نوع الخط', 'Font')} aria-label={tr('editor.font', 'نوع الخط', 'Font')}
          onChange={(e) => (e.target.value
            ? markChain(editor).setFontFamily(e.target.value).run()
            : markChain(editor).unsetFontFamily().run())}>
          <option value="">{tr('editor.fontDefault', 'الخط الافتراضي', 'Default')}</option>
          <optgroup label={tr('editor.arabicFonts', 'خطوط عربية', 'Arabic')}>
            {FONTS_AR.map((f) => <option key={f} value={`"${f}", serif`}>{f}</option>)}
          </optgroup>
          <optgroup label={tr('editor.latinFonts', 'خطوط لاتينية', 'Latin')}>
            {FONTS_LATIN.map((f) => <option key={f} value={`"${f}", sans-serif`}>{f}</option>)}
          </optgroup>
        </select>

        <select className="rte__select rte__select--sm" value={fontSize}
          title={tr('editor.size', 'حجم الخط', 'Size')} aria-label={tr('editor.size', 'حجم الخط', 'Size')}
          onChange={(e) => (e.target.value
            ? markChain(editor).setFontSize(e.target.value).run()
            : markChain(editor).unsetFontSize().run())}>
          <option value="">{tr('editor.sizeAuto', 'تلقائي', 'Auto')}</option>
          {SIZES.map((s) => <option key={s} value={`${s}pt`}>{s}</option>)}
        </select>

        <select className="rte__select rte__select--md" value={heading}
          title={tr('editor.style', 'نمط الفقرة', 'Style')} aria-label={tr('editor.style', 'نمط الفقرة', 'Style')}
          onChange={(e) => {
            const lvl = Number(e.target.value)
            if (lvl === 0) editor.chain().focus().setParagraph().run()
            else editor.chain().focus().toggleHeading({ level: lvl as 1 | 2 | 3 | 4 }).run()
          }}>
          <option value={0}>{tr('editor.normal', 'نص عادي', 'Normal')}</option>
          {[1, 2, 3, 4].map((l) => (
            <option key={l} value={l}>{tr(`editor.h${l}`, `عنوان ${l}`, `Heading ${l}`)}</option>
          ))}
        </select>
      </div>

      {/* Character formatting */}
      <div className="rte__group">
        <button type="button" className={btn(editor.isActive('bold'))} aria-pressed={editor.isActive('bold')}
          title={tr('editor.bold', 'عريض (Ctrl+B)', 'Bold (Ctrl+B)')} aria-label={tr('editor.bold', 'عريض', 'Bold')}
          onClick={() => markChain(editor).toggleBold().run()}><Icon name="bold" /></button>
        <button type="button" className={btn(editor.isActive('italic'))} aria-pressed={editor.isActive('italic')}
          title={tr('editor.italic', 'مائل (Ctrl+I)', 'Italic (Ctrl+I)')} aria-label={tr('editor.italic', 'مائل', 'Italic')}
          onClick={() => markChain(editor).toggleItalic().run()}><Icon name="italic" /></button>
        <button type="button" className={btn(editor.isActive('underline'))} aria-pressed={editor.isActive('underline')}
          title={tr('editor.underline', 'تسطير (Ctrl+U)', 'Underline (Ctrl+U)')} aria-label={tr('editor.underline', 'تسطير', 'Underline')}
          onClick={() => markChain(editor).toggleUnderline().run()}><Icon name="underline" /></button>
        <button type="button" className={btn(editor.isActive('strike'))} aria-pressed={editor.isActive('strike')}
          title={tr('editor.strike', 'يتوسطه خط', 'Strikethrough')} aria-label={tr('editor.strike', 'يتوسطه خط', 'Strikethrough')}
          onClick={() => markChain(editor).toggleStrike().run()}><Icon name="strike" /></button>
      </div>

      {/* Colour + highlight swatches */}
      <div className="rte__group">
        <SwatchMenu
          label={tr('editor.textColor', 'لون النص', 'Text colour')} icon="textColor" variant="text"
          onPick={(c) => markChain(editor).setColor(c).run()}
          onClear={() => markChain(editor).unsetColor().run()}
          clearLabel={tr('editor.autoColor', 'تلقائي', 'Automatic')}
        />
        <SwatchMenu
          label={tr('editor.highlight', 'لون التظليل', 'Highlight')} icon="highlight" variant="highlight"
          onPick={(c) => markChain(editor).toggleHighlight({ color: c }).run()}
          onClear={() => markChain(editor).unsetHighlight().run()}
          clearLabel={tr('editor.noHighlight', 'بلا تظليل', 'No colour')}
        />
      </div>

      {/* Alignment */}
      <div className="rte__group">
        {([
          ['start', 'alignStart', tr('editor.alignStart', 'محاذاة للبداية', 'Align start')],
          ['center', 'alignCenter', tr('editor.alignCenter', 'توسيط', 'Center')],
          ['end', 'alignEnd', tr('editor.alignEnd', 'محاذاة للنهاية', 'Align end')],
          ['justify', 'justify', tr('editor.justify', 'ضبط', 'Justify')],
        ] as const).map(([a, icon, label]) => (
          <button key={a} type="button" className={btn(editor.isActive({ textAlign: a }))}
            title={label} aria-label={label}
            onClick={() => editor.chain().focus().setTextAlign(a).run()}><Icon name={icon} /></button>
        ))}
      </div>

      {/* Lists + indent */}
      <div className="rte__group">
        <button type="button" className={btn(editor.isActive('orderedList'))}
          title={tr('editor.ordered', 'قائمة رقمية', 'Numbered list')} aria-label={tr('editor.ordered', 'قائمة رقمية', 'Numbered list')}
          onClick={() => editor.chain().focus().toggleOrderedList().run()}><Icon name="orderedList" /></button>
        <button type="button" className={btn(editor.isActive('bulletList'))}
          title={tr('editor.bullet', 'قائمة نقطية', 'Bulleted list')} aria-label={tr('editor.bullet', 'قائمة نقطية', 'Bulleted list')}
          onClick={() => editor.chain().focus().toggleBulletList().run()}><Icon name="bulletList" /></button>
        <button type="button" className={btn(false)} disabled={indent >= MAX_INDENT && !editor.isActive('listItem')}
          title={tr('editor.indent', 'زيادة المسافة البادئة', 'Increase indent')} aria-label={tr('editor.indent', 'زيادة المسافة البادئة', 'Increase indent')}
          onClick={() => setIndent(1)}><Icon name="indent" /></button>
        <button type="button" className={btn(false)} disabled={indent <= 0 && !editor.isActive('listItem')}
          title={tr('editor.outdent', 'تقليل المسافة البادئة', 'Decrease indent')} aria-label={tr('editor.outdent', 'تقليل المسافة البادئة', 'Decrease indent')}
          onClick={() => setIndent(-1)}><Icon name="outdent" /></button>
      </div>

      {/* Insert */}
      <div className="rte__group">
        <TableGridPicker
          label={tr('editor.table', 'إدراج جدول', 'Insert table')}
          onPick={(rows, cols) => editor.chain().focus().insertTable({ rows, cols, withHeaderRow: true }).run()}
        />
        <ImageButton label={tr('editor.image', 'إدراج صورة', 'Insert image')}
          onPick={(src) => editor.chain().focus().setImage({ src }).run()} />
        <button type="button" className={btn(false)}
          title={tr('editor.hr', 'خط أفقي', 'Horizontal rule')} aria-label={tr('editor.hr', 'خط أفقي', 'Horizontal rule')}
          onClick={() => editor.chain().focus().setHorizontalRule().run()}><Icon name="hr" /></button>
      </div>

      {/* Paragraph direction + find + clear */}
      <div className="rte__group">
        <button type="button" className={btn(blockDir === 'rtl')}
          title={tr('editor.rtl', 'اتجاه الفقرة: من اليمين', 'Paragraph RTL')} aria-label={tr('editor.rtl', 'اتجاه الفقرة: من اليمين', 'Paragraph RTL')}
          onClick={() => editor.chain().focus().updateAttributes(blockType, { blockDir: blockDir === 'rtl' ? null : 'rtl' }).run()}><Icon name="rtl" /></button>
        <button type="button" className={btn(blockDir === 'ltr')}
          title={tr('editor.ltr', 'اتجاه الفقرة: من اليسار', 'Paragraph LTR')} aria-label={tr('editor.ltr', 'اتجاه الفقرة: من اليسار', 'Paragraph LTR')}
          onClick={() => editor.chain().focus().updateAttributes(blockType, { blockDir: blockDir === 'ltr' ? null : 'ltr' }).run()}><Icon name="ltr" /></button>
        <button type="button" className={btn(false)}
          title={tr('editor.find', 'بحث واستبدال (Ctrl+H)', 'Find & replace (Ctrl+H)')} aria-label={tr('editor.find', 'بحث واستبدال', 'Find & replace')}
          onClick={onOpenFind}><Icon name="find" /></button>
        <button type="button" className={btn(false)}
          title={tr('editor.clearFormat', 'مسح التنسيق', 'Clear formatting')} aria-label={tr('editor.clearFormat', 'مسح التنسيق', 'Clear formatting')}
          onClick={() => editor.chain().focus().unsetAllMarks().clearNodes().run()}><Icon name="clearFormat" /></button>
      </div>
    </div>
  )
}

/* ────────────────────────────────────────────────────────────────────────────
   Right-click menu — formats the whole selection at once.
   With no selection it falls back to the caret's word (same rule as the ribbon).
   ──────────────────────────────────────────────────────────────────────────── */

function SelectionMenu({
  editor, tr, onOpenFind,
}: { editor: Editor; tr: TrFn; onOpenFind: () => void }) {
  const { prompt } = useConfirmDialog()
  const hasSelection = !editor.state.selection.empty
  const inTable = editor.isActive('table')
  const fontFamily = (editor.getAttributes('textStyle').fontFamily as string) ?? ''
  const fontSize = (editor.getAttributes('textStyle').fontSize as string) ?? ''

  const curSize = () => parseInt(fontSize, 10) || 12
  const grow = () => markChain(editor).setFontSize(`${SIZES.find((s) => s > curSize()) ?? SIZES[SIZES.length - 1]}pt`).run()
  const shrink = () => markChain(editor).setFontSize(`${[...SIZES].reverse().find((s) => s < curSize()) ?? SIZES[0]}pt`).run()

  // Cut/Copy act on the editor's own DOM selection; focus() restores it first.
  const clip = (action: 'cut' | 'copy') => { editor.commands.focus(); document.execCommand(action) }
  // Paste as plain text — matches Word's default "Keep Text Only" option.
  const pastePlain = async () => {
    const text = await navigator.clipboard.readText().catch(() => '')
    if (text) editor.chain().focus().insertContent(text.replace(/\r?\n/g, '<br>')).run()
  }
  const setLink = async () => {
    const prev = editor.getAttributes('link').href as string | undefined
    const url = await prompt({ label: tr('editor.linkUrl', 'أدخل الرابط', 'Enter a URL'), defaultValue: prev ?? 'https://', ltr: true })
    if (url === null) return
    if (url.trim() === '') editor.chain().focus().extendMarkRange('link').unsetLink().run()
    else editor.chain().focus().extendMarkRange('link').setLink({ href: url.trim() }).run()
  }

  // A mini-toolbar button: keeps the editor's selection (no focus steal) and leaves the menu open.
  const mini = (icon: keyof typeof ICONS, label: string, run: () => void, active?: boolean) => (
    <button type="button" className={`rte__mini-btn${active ? ' is-active' : ''}`}
      title={label} aria-label={label} aria-pressed={active}
      onMouseDown={(e) => { e.preventDefault(); run() }}>
      <Icon name={icon} />
    </button>
  )

  const item = (
    key: string, ar: string, en: string, icon: keyof typeof ICONS,
    run: () => void, shortcut?: string, active?: boolean,
  ) => (
    <ContextMenuItem onSelect={run} className={active ? 'rte__cm-item is-active' : 'rte__cm-item'}>
      <Icon name={icon} />
      <span>{tr(key, ar, en)}</span>
      {shortcut && <ContextMenuShortcut>{shortcut}</ContextMenuShortcut>}
    </ContextMenuItem>
  )

  return (
    <ContextMenuContent className="rte__cm rte__cm--word">
      {/* ── Word-style floating mini toolbar ── */}
      <div className="rte__mini" onMouseDown={(e) => e.preventDefault()}>
        <div className="rte__mini-row">
          <select className="rte__mini-sel" value={fontFamily}
            title={tr('editor.font', 'نوع الخط', 'Font')} aria-label={tr('editor.font', 'نوع الخط', 'Font')}
            onMouseDown={(e) => e.stopPropagation()}
            onChange={(e) => (e.target.value
              ? markChain(editor).setFontFamily(e.target.value).run()
              : markChain(editor).unsetFontFamily().run())}>
            <option value="">{tr('editor.fontDefault', 'الخط الافتراضي', 'Default')}</option>
            {[...FONTS_AR, ...FONTS_LATIN].map((f) => <option key={f} value={`"${f}"`}>{f}</option>)}
          </select>
          <select className="rte__mini-sel rte__mini-sel--sm" value={fontSize}
            title={tr('editor.size', 'حجم الخط', 'Size')} aria-label={tr('editor.size', 'حجم الخط', 'Size')}
            onMouseDown={(e) => e.stopPropagation()}
            onChange={(e) => (e.target.value
              ? markChain(editor).setFontSize(e.target.value).run()
              : markChain(editor).unsetFontSize().run())}>
            <option value="">–</option>
            {SIZES.map((s) => <option key={s} value={`${s}pt`}>{s}</option>)}
          </select>
          {mini('grow', tr('editor.grow', 'تكبير الخط', 'Grow font'), grow)}
          {mini('shrink', tr('editor.shrink', 'تصغير الخط', 'Shrink font'), shrink)}
          <span className="rte__mini-sep" />
          {mini('highlight', tr('editor.highlight', 'لون التظليل', 'Highlight'),
            () => markChain(editor).toggleHighlight({ color: SWATCHES[6] }).run(), editor.isActive('highlight'))}
          {mini('textColor', tr('editor.textColor', 'لون النص', 'Text colour'),
            () => markChain(editor).setColor(SWATCHES[3]).run())}
        </div>
        <div className="rte__mini-row">
          {mini('bold', tr('editor.bold', 'عريض', 'Bold'), () => markChain(editor).toggleBold().run(), editor.isActive('bold'))}
          {mini('italic', tr('editor.italic', 'مائل', 'Italic'), () => markChain(editor).toggleItalic().run(), editor.isActive('italic'))}
          {mini('underline', tr('editor.underline', 'تسطير', 'Underline'), () => markChain(editor).toggleUnderline().run(), editor.isActive('underline'))}
          {mini('strike', tr('editor.strike', 'يتوسطه خط', 'Strikethrough'), () => markChain(editor).toggleStrike().run(), editor.isActive('strike'))}
          <span className="rte__mini-sep" />
          {mini('alignStart', tr('editor.alignStart', 'محاذاة للبداية', 'Align start'), () => editor.chain().focus().setTextAlign('start').run(), editor.isActive({ textAlign: 'start' }))}
          {mini('alignCenter', tr('editor.alignCenter', 'توسيط', 'Center'), () => editor.chain().focus().setTextAlign('center').run(), editor.isActive({ textAlign: 'center' }))}
          <span className="rte__mini-sep" />
          {mini('bulletList', tr('editor.bullet', 'قائمة نقطية', 'Bulleted list'), () => editor.chain().focus().toggleBulletList().run(), editor.isActive('bulletList'))}
          {mini('orderedList', tr('editor.ordered', 'قائمة رقمية', 'Numbered list'), () => editor.chain().focus().toggleOrderedList().run(), editor.isActive('orderedList'))}
          {mini('clearFormat', tr('editor.clearFormat', 'مسح التنسيق', 'Clear formatting'),
            () => (hasSelection ? editor.chain().focus().unsetAllMarks().run() : markChain(editor).unsetAllMarks().run()))}
        </div>
      </div>

      <ContextMenuSeparator />

      {/* ── Classic clipboard block ── */}
      {item('editor.cut', 'قص', 'Cut', 'cut', () => clip('cut'), 'Ctrl+X')}
      {item('editor.copy', 'نسخ', 'Copy', 'copy', () => clip('copy'), 'Ctrl+C')}
      {item('editor.paste', 'لصق', 'Paste', 'paste', () => { void pastePlain() }, 'Ctrl+V')}

      <ContextMenuSeparator />

      {/* ── Font… (text colour, highlight, sizes) ── */}
      <ContextMenuSub>
        <ContextMenuSubTrigger className="rte__cm-item">
          <Icon name="font" />
          <span>{tr('editor.fontMenu', 'الخط…', 'Font…')}</span>
        </ContextMenuSubTrigger>
        <ContextMenuSubContent className="rte__cm">
          <div className="rte__cm-label">{tr('editor.textColor', 'لون النص', 'Text colour')}</div>
          <div className="rte__swatches rte__swatches--cm">
            {SWATCHES.map((c, i) => (
              <button key={i} type="button" className={`rte__swatch rte__swatch--${i + 1}`}
                aria-label={`${tr('editor.textColor', 'لون النص', 'Text colour')} ${i + 1}`}
                onClick={() => markChain(editor).setColor(c).run()} />
            ))}
          </div>
          <ContextMenuItem className="rte__cm-item" onSelect={() => markChain(editor).unsetColor().run()}>
            {tr('editor.autoColor', 'تلقائي', 'Automatic')}
          </ContextMenuItem>
          <ContextMenuSeparator />
          <div className="rte__cm-label">{tr('editor.highlight', 'لون التظليل', 'Highlight')}</div>
          <div className="rte__swatches rte__swatches--cm">
            {SWATCHES.map((c, i) => (
              <button key={i} type="button" className={`rte__swatch rte__swatch--${i + 1}`}
                aria-label={`${tr('editor.highlight', 'لون التظليل', 'Highlight')} ${i + 1}`}
                onClick={() => markChain(editor).toggleHighlight({ color: c }).run()} />
            ))}
          </div>
          <ContextMenuItem className="rte__cm-item" onSelect={() => markChain(editor).unsetHighlight().run()}>
            {tr('editor.noHighlight', 'بلا تظليل', 'No colour')}
          </ContextMenuItem>
        </ContextMenuSubContent>
      </ContextMenuSub>

      {/* ── Paragraph… (alignment, lists, indent) ── */}
      <ContextMenuSub>
        <ContextMenuSubTrigger className="rte__cm-item">
          <Icon name="paragraph" />
          <span>{tr('editor.paragraphMenu', 'فقرة…', 'Paragraph…')}</span>
        </ContextMenuSubTrigger>
        <ContextMenuSubContent className="rte__cm">
          {item('editor.alignStart', 'محاذاة للبداية', 'Align start', 'alignStart',
            () => editor.chain().focus().setTextAlign('start').run(), undefined, editor.isActive({ textAlign: 'start' }))}
          {item('editor.alignCenter', 'توسيط', 'Center', 'alignCenter',
            () => editor.chain().focus().setTextAlign('center').run(), undefined, editor.isActive({ textAlign: 'center' }))}
          {item('editor.alignEnd', 'محاذاة للنهاية', 'Align end', 'alignEnd',
            () => editor.chain().focus().setTextAlign('end').run(), undefined, editor.isActive({ textAlign: 'end' }))}
          {item('editor.justify', 'ضبط', 'Justify', 'justify',
            () => editor.chain().focus().setTextAlign('justify').run(), undefined, editor.isActive({ textAlign: 'justify' }))}
          <ContextMenuSeparator />
          {item('editor.bullet', 'قائمة نقطية', 'Bulleted list', 'bulletList',
            () => editor.chain().focus().toggleBulletList().run(), undefined, editor.isActive('bulletList'))}
          {item('editor.ordered', 'قائمة رقمية', 'Numbered list', 'orderedList',
            () => editor.chain().focus().toggleOrderedList().run(), undefined, editor.isActive('orderedList'))}
        </ContextMenuSubContent>
      </ContextMenuSub>

      {item('editor.hyperlink', 'ارتباط تشعبي…', 'Hyperlink…', 'link', setLink)}

      {inTable && (
        <>
          <ContextMenuSeparator />
          {item('editor.addRow', 'إدراج صف', 'Insert row', 'table',
            () => editor.chain().focus().addRowAfter().run())}
          {item('editor.addCol', 'إدراج عمود', 'Insert column', 'table',
            () => editor.chain().focus().addColumnAfter().run())}
          {item('editor.delRow', 'حذف صف', 'Delete row', 'table',
            () => editor.chain().focus().deleteRow().run())}
          {item('editor.delCol', 'حذف عمود', 'Delete column', 'table',
            () => editor.chain().focus().deleteColumn().run())}
          {item('editor.delTable', 'حذف الجدول', 'Delete table', 'table',
            () => editor.chain().focus().deleteTable().run())}
        </>
      )}

      <ContextMenuSeparator />

      {item('editor.find', 'بحث واستبدال', 'Find & replace', 'find', onOpenFind, 'Ctrl+H')}
    </ContextMenuContent>
  )
}

/* ────────────────────────────────────────────────────────────────────────────
   Swatch menu (Word-style colour grid)
   ──────────────────────────────────────────────────────────────────────────── */

function SwatchMenu({
  label, icon, variant, onPick, onClear, clearLabel,
}: {
  label: string; icon: keyof typeof ICONS; variant: 'text' | 'highlight'
  onPick: (c: string) => void; onClear: () => void; clearLabel: string
}) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!open) return
    const onDoc = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false) }
    document.addEventListener('mousedown', onDoc)
    return () => document.removeEventListener('mousedown', onDoc)
  }, [open])

  return (
    <div className="rte__menu" ref={ref}>
      <button type="button" className={`rte__btn rte__btn--swatch rte__btn--${variant}`}
        title={label} aria-label={label} aria-expanded={open}
        onClick={() => setOpen((o) => !o)}>
        <Icon name={icon} />
        <span className="rte__swatch-bar" aria-hidden />
      </button>
      {open && (
        <div className="rte__pop rte__pop--swatches" role="menu">
          <div className="rte__swatches">
            {SWATCHES.map((c, i) => (
              <button key={i} type="button" className={`rte__swatch rte__swatch--${i + 1}`} role="menuitem"
                aria-label={`${label} ${i + 1}`}
                onMouseDown={(e) => { e.preventDefault(); onPick(c); setOpen(false) }} />
            ))}
          </div>
          <button type="button" className="rte__pop-action" role="menuitem"
            onMouseDown={(e) => { e.preventDefault(); onClear(); setOpen(false) }}>{clearLabel}</button>
        </div>
      )}
    </div>
  )
}

/* ────────────────────────────────────────────────────────────────────────────
   Table grid picker — hover an n×m grid like Word
   ──────────────────────────────────────────────────────────────────────────── */

const GRID = 8

function TableGridPicker({ label, onPick }: { label: string; onPick: (rows: number, cols: number) => void }) {
  const [open, setOpen] = useState(false)
  const [hover, setHover] = useState({ r: 0, c: 0 })
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!open) return
    const onDoc = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false) }
    document.addEventListener('mousedown', onDoc)
    return () => document.removeEventListener('mousedown', onDoc)
  }, [open])

  const cells = useMemo(() => Array.from({ length: GRID * GRID }, (_, i) => ({
    r: Math.floor(i / GRID) + 1, c: (i % GRID) + 1,
  })), [])

  return (
    <div className="rte__menu" ref={ref}>
      <button type="button" className="rte__btn" title={label} aria-label={label} aria-expanded={open}
        onClick={() => setOpen((o) => !o)}><Icon name="table" /></button>
      {open && (
        <div className="rte__pop rte__pop--grid" role="menu">
          <div className="rte__grid" onMouseLeave={() => setHover({ r: 0, c: 0 })}>
            {cells.map(({ r, c }) => (
              <button
                key={`${r}-${c}`} type="button" role="menuitem" aria-label={`${c}×${r}`}
                className={`rte__grid-cell${r <= hover.r && c <= hover.c ? ' is-on' : ''}`}
                onMouseEnter={() => setHover({ r, c })}
                onMouseDown={(e) => { e.preventDefault(); onPick(r, c); setOpen(false) }}
              />
            ))}
          </div>
          <div className="rte__grid-label">{hover.r ? `${hover.c} × ${hover.r}` : label}</div>
        </div>
      )}
    </div>
  )
}

/* ────────────────────────────────────────────────────────────────────────────
   Image upload → base64
   ──────────────────────────────────────────────────────────────────────────── */

function ImageButton({ label, onPick }: { label: string; onPick: (src: string) => void }) {
  const input = useRef<HTMLInputElement>(null)
  return (
    <>
      <button type="button" className="rte__btn" title={label} aria-label={label}
        onClick={() => input.current?.click()}><Icon name="image" /></button>
      <input
        ref={input} type="file" accept="image/*" className="rte__file"
        onChange={(e) => {
          const f = e.target.files?.[0]
          if (!f) return
          const reader = new FileReader()
          reader.onload = () => onPick(String(reader.result))
          reader.readAsDataURL(f)
          e.target.value = ''
        }}
      />
    </>
  )
}

/* ────────────────────────────────────────────────────────────────────────────
   Find & Replace (Ctrl+H)
   ──────────────────────────────────────────────────────────────────────────── */

function FindReplace({
  editor, open, onOpenChange, tr,
}: { editor: Editor; open: boolean; onOpenChange: (o: boolean) => void; tr: TrFn }) {
  const [query, setQuery] = useState('')
  const [replacement, setReplacement] = useState('')
  const [matchCase, setMatchCase] = useState(false)
  const [cursor, setCursor] = useState(0)

  const matches = useMemo(
    () => (open ? findMatches(editor, query, matchCase) : []),
    [editor, query, matchCase, open],
  )

  const goTo = (index: number) => {
    if (!matches.length) return
    const i = ((index % matches.length) + matches.length) % matches.length
    setCursor(i)
    const m = matches[i]
    editor.chain().focus().setTextSelection({ from: m.from, to: m.to }).scrollIntoView().run()
  }

  const replaceOne = () => {
    if (!matches.length) return
    const m = matches[Math.min(cursor, matches.length - 1)]
    editor.chain().focus().insertContentAt({ from: m.from, to: m.to }, replacement).run()
  }

  // Replace from the end backwards so earlier positions stay valid.
  const replaceAll = () => {
    const all = findMatches(editor, query, matchCase)
    if (!all.length) return
    const chain = editor.chain().focus()
    all.slice().reverse().forEach((m) => chain.insertContentAt({ from: m.from, to: m.to }, replacement))
    chain.run()
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="rte__dialog">
        <DialogHeader>
          <DialogTitle>{tr('editor.findReplace', 'بحث واستبدال', 'Find and replace')}</DialogTitle>
        </DialogHeader>

        <div className="rte__find">
          <label className="rte__find-row">
            <span>{tr('editor.findWhat', 'البحث عن', 'Find what')}</span>
            <input className="rte__find-input" value={query} autoFocus
              onChange={(e) => { setQuery(e.target.value); setCursor(0) }} />
          </label>
          <label className="rte__find-row">
            <span>{tr('editor.replaceWith', 'استبدال بـ', 'Replace with')}</span>
            <input className="rte__find-input" value={replacement}
              onChange={(e) => setReplacement(e.target.value)} />
          </label>
          <label className="rte__find-check">
            <input type="checkbox" checked={matchCase} onChange={(e) => setMatchCase(e.target.checked)} />
            <span>{tr('editor.matchCase', 'مطابقة حالة الأحرف', 'Match case')}</span>
          </label>
          <p className="rte__find-count">
            {matches.length
              ? tr('editor.matchCount', `${matches.length} نتيجة`, `${matches.length} matches`)
              : query ? tr('editor.noMatch', 'لا نتائج', 'No matches') : ''}
          </p>
        </div>

        <DialogFooter>
          <button type="button" className="rte__dialog-btn" disabled={!matches.length}
            onClick={() => goTo(cursor + 1)}>{tr('editor.findNext', 'التالي', 'Find next')}</button>
          <button type="button" className="rte__dialog-btn" disabled={!matches.length}
            onClick={replaceOne}>{tr('editor.replace', 'استبدال', 'Replace')}</button>
          <button type="button" className="rte__dialog-btn rte__dialog-btn--primary" disabled={!matches.length}
            onClick={replaceAll}>{tr('editor.replaceAll', 'استبدال الكل', 'Replace all')}</button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

/* ────────────────────────────────────────────────────────────────────────────
   Status bar
   ──────────────────────────────────────────────────────────────────────────── */

function StatusBar({
  stats, num, lang, tr,
}: { stats: WordCountStats; num: (n: number) => string; lang: 'ar' | 'en'; tr: TrFn }) {
  return (
    <div className="rte__status" role="status">
      <span>{tr('editor.words', 'الكلمات', 'Words')}: {num(stats.words)}</span>
      <span className="rte__status-sep" aria-hidden>|</span>
      <span>{tr('editor.chars', 'الأحرف', 'Characters')}: {num(stats.chars)}</span>
      <span className="rte__status-sep" aria-hidden>|</span>
      <span>{tr('editor.pages', 'الصفحات', 'Pages')}: {num(stats.pages)}</span>
      <span className="rte__status-sep" aria-hidden>|</span>
      <span className="rte__status-lang">{lang === 'ar' ? 'العربية' : 'English'}</span>
    </div>
  )
}
