/**
 * Screen-capture deterrent for sensitive screens and print previews.
 *
 * Be clear about what this is: a web page cannot stop the operating system from capturing the
 * screen. Nothing running in a browser tab can. What the guard does is raise the cost of a capture
 * and make the obvious routes fail:
 *   • PrintScreen is swallowed, the clipboard is overwritten with a notice, and the page is masked
 *     for a moment — so the grab that lands in the clipboard is the mask, not the letter.
 *   • The page masks itself whenever it loses focus or is hidden. That covers the common capture
 *     tools (Snipping Tool, Win+Shift+S, third-party grabbers) because they all take focus first.
 *   • Copy, cut, drag and the context menu are blocked, and the text is unselectable.
 *   • In preview-only mode the print keystrokes and `window.print()` are neutralised too.
 *
 * Treat it as a deterrent and an audit signal — never as a guarantee.
 */

export interface ScreenGuardOptions {
  /** Text shown over the masked page. */
  message?: string
  /** Fired when a capture attempt is detected, for a toast or an audit entry. */
  onViolation?: (reason: 'printscreen') => void
  /** Also neutralise printing (Ctrl+P and `window.print`) — used for preview-without-print. */
  blockPrint?: boolean
}

const DEFAULT_MESSAGE = 'محتوى محمي — أُخفي أثناء خروج النافذة من التركيز'
const CLIPBOARD_NOTE = '⚠ محتوى محمي — لا يُسمح بالتقاط صورة الشاشة'
const FLASH_MS = 1500
const STYLE_ID = 'sg-style'

/** The stylesheet the guard needs. Exported so a generated print/preview document can inline it. */
export const SCREEN_GUARD_CSS = `
[data-guard] { -webkit-user-select: none; user-select: none; }
[data-guard] img, [data-guard] a { -webkit-user-drag: none; }
/* Masking is strictly a screen concern. Scoped to @media screen because the print renderer reads
   the same computed styles — a page masked while the print dialog held focus would otherwise come
   out of the printer blurred. */
@media screen {
  [data-guard] .sg-veil {
    position: fixed; inset: 0; z-index: 2147483647; display: none;
    align-items: center; justify-content: center; text-align: center;
    padding: 2rem; font: 600 15px/1.8 'Segoe UI', Tahoma, sans-serif;
    color: #f8fafc; background: #0f172a;
  }
  [data-guard-masked] .sg-veil { display: flex; }
  /* The content itself is blurred too, in case the veil is defeated by a compositor trick. */
  [data-guard-masked] body > *:not(.sg-veil) { filter: blur(22px); opacity: .35; }
}
@media print { .sg-veil { display: none !important; } }
/* A capture of the printed page would bypass the veil, so mask on print too when asked. */
@media print { [data-guard-noprint] body { display: none !important; } }
`

function ensureStyle(doc: Document) {
  if (doc.getElementById(STYLE_ID)) return
  const el = doc.createElement('style')
  el.id = STYLE_ID
  el.textContent = SCREEN_GUARD_CSS
  doc.head.appendChild(el)
}

/**
 * Turn the guard on for a document (the app's own, or a preview window's). Returns a disposer that
 * puts everything back — always call it, or a masked page can be left stuck behind.
 */
export function attachScreenGuard(doc: Document, opts: ScreenGuardOptions = {}): () => void {
  const win = doc.defaultView
  if (!win) return () => {}

  const root = doc.documentElement
  root.setAttribute('data-guard', '')
  if (opts.blockPrint) root.setAttribute('data-guard-noprint', '')
  ensureStyle(doc)

  const veil = doc.createElement('div')
  veil.className = 'sg-veil'
  veil.textContent = opts.message ?? DEFAULT_MESSAGE
  doc.body?.appendChild(veil)

  let flashTimer: number | undefined
  const mask = () => root.setAttribute('data-guard-masked', '')
  const unmask = () => root.removeAttribute('data-guard-masked')

  /** Mask long enough that a capture taken now lands on the veil, then restore. */
  const flash = () => {
    mask()
    win.clearTimeout(flashTimer)
    flashTimer = win.setTimeout(unmask, FLASH_MS)
  }

  // PrintScreen never reaches the page as a normal key on every platform, so react to both edges
  // and stamp the clipboard either way — that is the copy the user ends up pasting.
  const onKey = (e: KeyboardEvent) => {
    if (e.key === 'PrintScreen' || e.code === 'PrintScreen') {
      e.preventDefault()
      flash()
      void win.navigator.clipboard?.writeText(CLIPBOARD_NOTE).catch(() => {})
      opts.onViolation?.('printscreen')
      return
    }
    if (opts.blockPrint && (e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'p') e.preventDefault()
  }

  const onBlur = () => mask()
  const onFocus = () => { win.clearTimeout(flashTimer); unmask() }
  const onVisibility = () => (doc.visibilityState === 'hidden' ? mask() : unmask())
  const swallow = (e: Event) => e.preventDefault()

  win.addEventListener('keydown', onKey, true)
  win.addEventListener('keyup', onKey, true)
  win.addEventListener('blur', onBlur)
  win.addEventListener('focus', onFocus)
  doc.addEventListener('visibilitychange', onVisibility)
  doc.addEventListener('contextmenu', swallow)
  doc.addEventListener('copy', swallow)
  doc.addEventListener('cut', swallow)
  doc.addEventListener('dragstart', swallow)

  // `window.print` is restored by the disposer; a preview that may not print must not be printable
  // from the browser's own menu either.
  const nativePrint = win.print
  if (opts.blockPrint) win.print = () => {}

  return () => {
    win.clearTimeout(flashTimer)
    win.removeEventListener('keydown', onKey, true)
    win.removeEventListener('keyup', onKey, true)
    win.removeEventListener('blur', onBlur)
    win.removeEventListener('focus', onFocus)
    doc.removeEventListener('visibilitychange', onVisibility)
    doc.removeEventListener('contextmenu', swallow)
    doc.removeEventListener('copy', swallow)
    doc.removeEventListener('cut', swallow)
    doc.removeEventListener('dragstart', swallow)
    if (opts.blockPrint) win.print = nativePrint
    root.removeAttribute('data-guard')
    root.removeAttribute('data-guard-masked')
    root.removeAttribute('data-guard-noprint')
    veil.remove()
  }
}
