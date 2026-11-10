import { useEditor, EditorContent, type Editor } from '@tiptap/react'
import { Undo2, Redo2, AlignJustify, Quote, Link2, Image as ImageIcon } from 'lucide-react'
import StarterKit from '@tiptap/starter-kit'
import { TextStyleKit } from '@tiptap/extension-text-style'
import Highlight from '@tiptap/extension-highlight'
import TextAlign from '@tiptap/extension-text-align'
import Subscript from '@tiptap/extension-subscript'
import Superscript from '@tiptap/extension-superscript'
import { Image } from '@tiptap/extension-image'
import { Table, TableRow, TableCell, TableHeader } from '@tiptap/extension-table'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useConfirmDialog } from './ui/confirm-dialog'
import './richeditor.css'

const FONTS = [
  { v: '', label: 'الخط الافتراضي' },
  { v: 'Tahoma, sans-serif', label: 'Tahoma' },
  { v: 'Arial, Helvetica, sans-serif', label: 'Arial' },
  { v: '"Times New Roman", serif', label: 'Times New Roman' },
  { v: '"Courier New", monospace', label: 'Courier New' },
  { v: 'Amiri, serif', label: 'Amiri' },
  { v: 'Cairo, sans-serif', label: 'Cairo' },
]
const SIZES = ['12px', '14px', '16px', '18px', '20px', '24px', '32px', '40px']

/** Rich correspondence-body editor (TipTap) with a Word-like toolbar: fonts, sizes, headings,
 * bold/italic/underline/strike, text + highlight colour, alignment, lists, sub/superscript,
 * quote/code, rules, links, images and tables. RTL-first; emits HTML on every change. */
export default function RichEditor({
  value, onChange, ariaLabel,
}: { value: string; onChange: (html: string) => void; ariaLabel?: string }) {
  const { t } = useTranslation()

  const editor = useEditor({
    extensions: [
      // StarterKit v3 already provides bold/italic/underline/strike/code/heading/lists/quote/hr/link/history.
      StarterKit.configure({ link: { openOnClick: false } }),
      TextStyleKit,                                   // TextStyle + Color + FontFamily + FontSize + BackgroundColor
      Highlight.configure({ multicolor: true }),
      TextAlign.configure({ types: ['heading', 'paragraph'] }),
      Subscript, Superscript,
      Image,
      Table.configure({ resizable: true }), TableRow, TableHeader, TableCell,
    ],
    content: value,
    onUpdate: ({ editor }) => onChange(editor.getHTML()),
    editorProps: { attributes: { class: 'richeditor__content', 'aria-label': ariaLabel ?? '' } },
  })

  // Keep the editor in sync when the value is reset externally (e.g. loading a draft).
  useEffect(() => {
    if (editor && value !== editor.getHTML()) editor.commands.setContent(value || '', { emitUpdate: false })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value])

  if (!editor) return null
  return (
    <div className="richeditor">
      <Toolbar editor={editor} t={t} ariaLabel={ariaLabel} />
      <EditorContent editor={editor} />
    </div>
  )
}

type TFn = (key: string, opts?: Record<string, unknown>) => string

const IS_WORD_CHAR = /[\p{L}\p{N}_]/u

/** Word-processor behaviour: with nothing selected, a character format applies to the word the caret
 * sits in (so each word can carry its own format), instead of only arming the next keystroke.
 * With an explicit selection, that selection is used untouched. */
function markChain(editor: Editor) {
  const chain = editor.chain().focus()
  const { empty, $from } = editor.state.selection
  if (!empty) return chain

  const text = $from.parent.textContent
  const offset = $from.parentOffset
  if (!text) return chain

  let start = offset
  let end = offset
  while (start > 0 && IS_WORD_CHAR.test(text[start - 1])) start--
  while (end < text.length && IS_WORD_CHAR.test(text[end])) end++
  if (start === end) return chain                    // caret isn't inside a word

  const base = $from.start()
  return chain.setTextSelection({ from: base + start, to: base + end })
}

function Toolbar({ editor, t, ariaLabel }: { editor: Editor; t: TFn; ariaLabel?: string }) {
  const { prompt } = useConfirmDialog()
  const btn = (active: boolean) => `richeditor__btn ${active ? 'is-active' : ''}`
  const currentSize = (editor.getAttributes('textStyle').fontSize as string) ?? ''
  const currentFont = (editor.getAttributes('textStyle').fontFamily as string) ?? ''
  const headingLevel = [1, 2, 3].find((l) => editor.isActive('heading', { level: l })) ?? 0

  return (
    <div className="richeditor__toolbar" role="toolbar" aria-label={ariaLabel}>
      {/* ── History ── */}
      <div className="richeditor__group">
        <button type="button" className={btn(false)} title={t('editor.undo', { defaultValue: 'تراجع' })}
          aria-label={t('editor.undo', { defaultValue: 'تراجع' })}
          onClick={() => editor.chain().focus().undo().run()} disabled={!editor.can().undo()}><Undo2 size={15} aria-hidden /></button>
        <button type="button" className={btn(false)} title={t('editor.redo', { defaultValue: 'إعادة' })}
          aria-label={t('editor.redo', { defaultValue: 'إعادة' })}
          onClick={() => editor.chain().focus().redo().run()} disabled={!editor.can().redo()}><Redo2 size={15} aria-hidden /></button>
      </div>

      {/* ── Font family / size / block type ── */}
      <div className="richeditor__group">
        <select className="richeditor__sel" title={t('editor.font', { defaultValue: 'نوع الخط' })}
          aria-label={t('editor.font', { defaultValue: 'نوع الخط' })}
          value={currentFont}
          onChange={(e) => e.target.value
            ? markChain(editor).setFontFamily(e.target.value).run()
            : markChain(editor).unsetFontFamily().run()}>
          {FONTS.map((f) => <option key={f.v} value={f.v}>{f.label}</option>)}
        </select>

        <select className="richeditor__sel richeditor__sel--sm" title={t('editor.size', { defaultValue: 'حجم الخط' })}
          aria-label={t('editor.size', { defaultValue: 'حجم الخط' })}
          value={currentSize}
          onChange={(e) => e.target.value
            ? markChain(editor).setFontSize(e.target.value).run()
            : markChain(editor).unsetFontSize().run()}>
          <option value="">{t('editor.sizeAuto', { defaultValue: 'تلقائي' })}</option>
          {SIZES.map((s) => <option key={s} value={s}>{parseInt(s, 10)}</option>)}
        </select>

        <select className="richeditor__sel" title={t('editor.block', { defaultValue: 'نمط الفقرة' })}
          aria-label={t('editor.block', { defaultValue: 'نمط الفقرة' })}
          value={headingLevel}
          onChange={(e) => {
            const lvl = Number(e.target.value)
            if (lvl === 0) editor.chain().focus().setParagraph().run()
            else editor.chain().focus().toggleHeading({ level: lvl as 1 | 2 | 3 }).run()
          }}>
          <option value={0}>{t('editor.paragraph', { defaultValue: 'نص عادي' })}</option>
          <option value={1}>{t('editor.h1', { defaultValue: 'عنوان 1' })}</option>
          <option value={2}>{t('editor.h2', { defaultValue: 'عنوان 2' })}</option>
          <option value={3}>{t('editor.h3', { defaultValue: 'عنوان 3' })}</option>
        </select>
      </div>

      {/* ── Character formatting ── */}
      <div className="richeditor__group">
        <button type="button" className={btn(editor.isActive('bold'))} title={t('editor.bold', { defaultValue: 'عريض' })}
          aria-label={t('editor.bold', { defaultValue: 'عريض' })} aria-pressed={editor.isActive('bold')}
          onClick={() => markChain(editor).toggleBold().run()}><b>B</b></button>
        <button type="button" className={btn(editor.isActive('italic'))} title={t('editor.italic', { defaultValue: 'مائل' })}
          aria-label={t('editor.italic', { defaultValue: 'مائل' })} aria-pressed={editor.isActive('italic')}
          onClick={() => markChain(editor).toggleItalic().run()}><i>I</i></button>
        <button type="button" className={btn(editor.isActive('underline'))} title={t('editor.underline', { defaultValue: 'تسطير' })}
          aria-label={t('editor.underline', { defaultValue: 'تسطير' })} aria-pressed={editor.isActive('underline')}
          onClick={() => markChain(editor).toggleUnderline().run()}><u>U</u></button>
        <button type="button" className={btn(editor.isActive('strike'))} title={t('editor.strike', { defaultValue: 'يتوسطه خط' })}
          aria-label={t('editor.strike', { defaultValue: 'يتوسطه خط' })} aria-pressed={editor.isActive('strike')}
          onClick={() => markChain(editor).toggleStrike().run()}><s>S</s></button>
        <button type="button" className={btn(editor.isActive('subscript'))} title={t('editor.sub', { defaultValue: 'منخفض' })}
          aria-label={t('editor.sub', { defaultValue: 'منخفض' })}
          onClick={() => markChain(editor).toggleSubscript().run()}>X₂</button>
        <button type="button" className={btn(editor.isActive('superscript'))} title={t('editor.sup', { defaultValue: 'مرتفع' })}
          aria-label={t('editor.sup', { defaultValue: 'مرتفع' })}
          onClick={() => markChain(editor).toggleSuperscript().run()}>X²</button>
      </div>

      {/* ── Colours ── */}
      <div className="richeditor__group">
        <label className="richeditor__color" title={t('editor.textColor', { defaultValue: 'لون النص' })}>
          <span className="richeditor__color-ic" style={{ color: (editor.getAttributes('textStyle').color as string) || undefined }}>A</span>
          <input type="color" aria-label={t('editor.textColor', { defaultValue: 'لون النص' })}
            value={(editor.getAttributes('textStyle').color as string) || '#000000'}
            onChange={(e) => markChain(editor).setColor(e.target.value).run()} />
        </label>
        <button type="button" className={btn(false)} title={t('editor.clearColor', { defaultValue: 'إزالة لون النص' })}
          aria-label={t('editor.clearColor', { defaultValue: 'إزالة لون النص' })}
          onClick={() => markChain(editor).unsetColor().run()}>A̶</button>

        <label className="richeditor__color" title={t('editor.highlight', { defaultValue: 'لون التظليل' })}>
          <span className="richeditor__color-ic richeditor__color-ic--hl"
            style={{ background: (editor.getAttributes('highlight').color as string) || undefined }}>▮</span>
          <input type="color" aria-label={t('editor.highlight', { defaultValue: 'لون التظليل' })}
            value={(editor.getAttributes('highlight').color as string) || '#ffe066'}
            onChange={(e) => markChain(editor).toggleHighlight({ color: e.target.value }).run()} />
        </label>
        <button type="button" className={btn(false)} title={t('editor.clearHighlight', { defaultValue: 'إزالة التظليل' })}
          aria-label={t('editor.clearHighlight', { defaultValue: 'إزالة التظليل' })}
          onClick={() => markChain(editor).unsetHighlight().run()}>▯</button>
      </div>

      {/* ── Alignment ── */}
      <div className="richeditor__group">
        <button type="button" className={btn(editor.isActive({ textAlign: 'right' }))} title={t('editor.alignRight', { defaultValue: 'محاذاة يمين' })}
          aria-label={t('editor.alignRight', { defaultValue: 'محاذاة يمين' })}
          onClick={() => editor.chain().focus().setTextAlign('right').run()}>≡</button>
        <button type="button" className={btn(editor.isActive({ textAlign: 'center' }))} title={t('editor.alignCenter', { defaultValue: 'توسيط' })}
          aria-label={t('editor.alignCenter', { defaultValue: 'توسيط' })}
          onClick={() => editor.chain().focus().setTextAlign('center').run()}>⁙</button>
        <button type="button" className={btn(editor.isActive({ textAlign: 'left' }))} title={t('editor.alignLeft', { defaultValue: 'محاذاة يسار' })}
          aria-label={t('editor.alignLeft', { defaultValue: 'محاذاة يسار' })}
          onClick={() => editor.chain().focus().setTextAlign('left').run()}>≣</button>
        <button type="button" className={btn(editor.isActive({ textAlign: 'justify' }))} title={t('editor.justify', { defaultValue: 'ضبط' })}
          aria-label={t('editor.justify', { defaultValue: 'ضبط' })}
          onClick={() => editor.chain().focus().setTextAlign('justify').run()}><AlignJustify size={15} aria-hidden /></button>
      </div>

      {/* ── Blocks ── */}
      <div className="richeditor__group">
        <button type="button" className={btn(editor.isActive('bulletList'))} title={t('editor.bullet', { defaultValue: 'قائمة نقطية' })}
          aria-label={t('editor.bullet', { defaultValue: 'قائمة نقطية' })}
          onClick={() => editor.chain().focus().toggleBulletList().run()}>•</button>
        <button type="button" className={btn(editor.isActive('orderedList'))} title={t('editor.ordered', { defaultValue: 'قائمة رقمية' })}
          aria-label={t('editor.ordered', { defaultValue: 'قائمة رقمية' })}
          onClick={() => editor.chain().focus().toggleOrderedList().run()}>1.</button>
        <button type="button" className={btn(editor.isActive('blockquote'))} title={t('editor.quote', { defaultValue: 'اقتباس' })}
          aria-label={t('editor.quote', { defaultValue: 'اقتباس' })}
          onClick={() => editor.chain().focus().toggleBlockquote().run()}><Quote size={15} aria-hidden /></button>
        <button type="button" className={btn(editor.isActive('code'))} title={t('editor.code', { defaultValue: 'رمز' })}
          aria-label={t('editor.code', { defaultValue: 'رمز' })}
          onClick={() => editor.chain().focus().toggleCode().run()}>{'</>'}</button>
        <button type="button" className={btn(false)} title={t('editor.hr', { defaultValue: 'خط فاصل' })}
          aria-label={t('editor.hr', { defaultValue: 'خط فاصل' })}
          onClick={() => editor.chain().focus().setHorizontalRule().run()}>―</button>
      </div>

      {/* ── Insert ── */}
      <div className="richeditor__group">
        <button type="button" className={btn(editor.isActive('link'))} title={t('editor.link', { defaultValue: 'رابط' })}
          aria-label={t('editor.link', { defaultValue: 'رابط' })}
          onClick={async () => {
            const url = await prompt({ title: t('editor.link', { defaultValue: 'رابط' }), label: 'URL', placeholder: 'https://', ltr: true })
            if (url) editor.chain().focus().setLink({ href: url }).run()
            else editor.chain().focus().unsetLink().run()
          }}><Link2 size={15} aria-hidden /></button>
        <button type="button" className={btn(false)} title={t('editor.image', { defaultValue: 'صورة' })}
          aria-label={t('editor.image', { defaultValue: 'صورة' })}
          onClick={async () => {
            const url = await prompt({ title: t('editor.image', { defaultValue: 'صورة' }), label: 'Image URL', placeholder: 'https://', ltr: true })
            if (url) editor.chain().focus().setImage({ src: url }).run()
          }}><ImageIcon size={15} aria-hidden /></button>
        <button type="button" className={btn(false)} title={t('editor.table', { defaultValue: 'جدول' })}
          aria-label={t('editor.table', { defaultValue: 'جدول' })}
          onClick={() => editor.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run()}>▦</button>
      </div>

      {/* ── Clear formatting ── */}
      <div className="richeditor__group">
        <button type="button" className={btn(false)} title={t('editor.clearFormat', { defaultValue: 'مسح التنسيق' })}
          aria-label={t('editor.clearFormat', { defaultValue: 'مسح التنسيق' })}
          onClick={() => editor.chain().focus().unsetAllMarks().clearNodes().run()}>⌫</button>
      </div>
    </div>
  )
}
