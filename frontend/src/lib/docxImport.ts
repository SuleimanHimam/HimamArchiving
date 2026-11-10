/**
 * Importing a Word file as a print design.
 *
 * Most letterheads in an organisation already exist as .docx — someone made one in Word years ago
 * and everyone copies it. Retyping that into the designer is the reason people keep printing from
 * Word instead. This converts the file to the designer's own HTML so it becomes a real design.
 *
 * What is *not* claimed: this is a conversion, not a viewer. Word features the designer has no
 * equivalent for (headers/footers, text boxes, columns, floating shapes) do not survive, and the
 * caller is told which ones were dropped rather than being left to spot it on the page.
 */

/** Alignment is carried through Word's model on the paragraph, not as a style mammoth can map. */
const ALIGN_CLASS: Record<string, string> = {
  center: 'text-align:center',
  right: 'text-align:right',
  left: 'text-align:left',
  both: 'text-align:justify',
  justify: 'text-align:justify',
}

/**
 * The designer's own page break. The inline style is what actually breaks the page when printing —
 * the class is only the marker the canvas draws a dashed rule for, and it is stripped on the print
 * path. Must stay identical to `insertPageBreak` in the designer.
 */
const PAGE_BREAK_HTML =
  '<div class="rd-pagebreak" style="break-before:page;page-break-before:always"></div>'

/** Marker mammoth emits for a Word page break, before it is swapped for the designer's own. */
const PB_MARK = '__docx_pagebreak'

export class DocxImportError extends Error {}

export interface DocxImportResult {
  html: string
  /** Human-readable notes about what the conversion could not carry over. */
  notes: string[]
  /** How many pages the design ends up with (page breaks + 1). */
  pages: number
}

export function isDocx(file: File) {
  return /\.docx$/i.test(file.name)
}

/**
 * Convert a .docx to designer HTML.
 *
 * The result still goes through `sanitizeRichHtml` when it reaches the canvas — this function is
 * about fidelity, not safety, and must not be treated as the sanitiser.
 */
export async function importDocx(file: File): Promise<DocxImportResult> {
  if (/\.doc$/i.test(file.name)) {
    // The old binary format is a different thing entirely; saying so beats a parse error.
    throw new DocxImportError('صيغة .doc القديمة غير مدعومة — احفظ الملف بصيغة .docx ثم أعد المحاولة')
  }
  if (!isDocx(file)) throw new DocxImportError('اختر ملف Word بصيغة .docx')

  // Loaded on demand: the converter is large and most sessions never import a Word file.
  const mammoth = (await import('mammoth/mammoth.browser')).default

  let result
  try {
    result = await mammoth.convertToHtml(
      { arrayBuffer: await file.arrayBuffer() },
      {
        styleMap: [
          "p[style-name='Title'] => h1:fresh",
          "p[style-name='Subtitle'] => h2:fresh",
          // Word's explicit page breaks become the designer's, so a multi-page file stays
          // multi-page instead of collapsing into one long column.
          `br[type='page'] => hr.${PB_MARK}:fresh`,
          ...Object.keys(ALIGN_CLASS).map((a) => `p[style-name='__align_${a}'] => p.__align_${a}:fresh`),
        ],
        transformDocument: tagAlignment,
      },
    )
  } catch {
    throw new DocxImportError('تعذّرت قراءة الملف — قد يكون تالفًا أو ليس ملف Word')
  }

  const { html, breaks } = applyPageBreaks(inlineAlignment(result.value))
  if (!html.trim()) throw new DocxImportError('الملف لا يحتوي نصًا يمكن استيراده')

  const notes = describe(result.messages)
  if (breaks === 0) {
    // Said plainly, because "my file was 3 pages and I got one" is otherwise read as a bug: Word
    // repaginates as text flows, and that pagination is not stored in the file.
    notes.unshift('لا يحتوي الملف فواصل صفحات صريحة، فاستُورد كصفحة واحدة متصلة — تقسيم Word التلقائي غير محفوظ داخل الملف. أضف الفواصل من «إدراج ← فاصل الصفحات».')
  }
  return { html, notes, pages: breaks + 1 }
}

/**
 * Swap mammoth's page-break markers for the designer's page-break element.
 *
 * mammoth emits the marker inside the paragraph it broke (`<p><hr/></p>`), which is invalid HTML
 * the browser would silently restructure, so the wrapping paragraph is unwrapped here instead of
 * left to chance.
 */
function applyPageBreaks(html: string): { html: string; breaks: number } {
  let breaks = 0
  const count = () => { breaks++; return PAGE_BREAK_HTML }
  const marker = new RegExp(`<hr class="${PB_MARK}"\\s*/?>`, 'g')
  return {
    html: html
      .replace(new RegExp(`<p>\\s*<hr class="${PB_MARK}"\\s*/?>\\s*</p>`, 'g'), count)
      .replace(marker, count),
    breaks,
  }
}

/**
 * Mark each aligned paragraph with a synthetic style name.
 *
 * Word stores alignment on the paragraph, and mammoth's style map keys off style *names*, so the
 * alignment has to be turned into one before it can be mapped. Defensive: this reaches into
 * mammoth's document model, and a version that changes it should cost alignment, not the import.
 */
function tagAlignment(element: unknown): unknown {
  try {
    const walk = (node: Record<string, unknown>): Record<string, unknown> => {
      const children = node.children
      if (Array.isArray(children)) {
        node.children = children.map((c) => walk(c as Record<string, unknown>))
      }
      if (node.type === 'paragraph') {
        const align = typeof node.alignment === 'string' ? node.alignment : null
        if (align && ALIGN_CLASS[align] && !node.styleName) {
          return { ...node, styleName: `__align_${align}` }
        }
      }
      return node
    }
    return walk(element as Record<string, unknown>)
  } catch { return element }
}

/** Turn the marker classes into inline styles, which survive the print-path sanitiser. */
function inlineAlignment(html: string) {
  return html.replace(
    /<p class="__align_(\w+)"/g,
    (whole, key: string) => (ALIGN_CLASS[key] ? `<p style="${ALIGN_CLASS[key]}"` : whole),
  )
}

/** mammoth reports unsupported constructs as warnings; turn the useful ones into plain Arabic. */
function describe(messages: { type: string; message: string }[]): string[] {
  const notes = new Set<string>()
  for (const m of messages) {
    const text = m.message ?? ''
    if (/unrecognised paragraph style|unrecognised run style/i.test(text)) {
      notes.add('بعض أنماط Word لا مقابل لها في المصمّم، فطُبِّق التنسيق الافتراضي عليها.')
    } else if (/image|picture/i.test(text)) {
      notes.add('تعذّر تضمين بعض الصور من الملف.')
    } else if (m.type === 'error') {
      notes.add(text)
    }
  }
  notes.add('ترويسة وتذييل صفحة Word والمربّعات النصية لا تُستورد — أعِد بناءها من عناصر المصمّم.')
  return [...notes]
}
