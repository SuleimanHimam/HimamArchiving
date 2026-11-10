import type { FormatFileProps } from '@/components/ui/file-card-collections'

// Real attachment extensions are wider than the card's own format union (the
// register stores docx/tiff/mp4 and friends), so map them onto the nearest
// supported placeholder. Unknown or missing extensions fall back to the plain
// text-page card rather than rendering nothing.
const EXTENSION_FORMATS: Record<string, FormatFileProps> = {
  avi: "video", bmp: "img", css: "css", csv: "csv", doc: "doc", docx: "doc",
  gif: "img", gz: "gz", htm: "html", html: "html", jpeg: "jpeg", jpg: "jpg",
  js: "js", json: "json", jsx: "jsx", md: "md", mdx: "mdx", mkv: "video",
  mov: "video", mp4: "video", pdf: "pdf", png: "png", ppt: "ppt", pptx: "pptx",
  rar: "rar", svg: "img", tar: "tar", tif: "img", tiff: "img", ts: "tsx",
  tsx: "tsx", txt: "txt", webm: "video", webp: "img", wmv: "video",
  xls: "xls", xlsx: "xlsx", zip: "zip",
};

/**
 * The badge text for any extension: its own name. Known formats already say it;
 * unknown ones borrow the neutral text-page card and label themselves, so every
 * archived file carries a visible type rather than an anonymous blank page.
 * Returns null when there is no file at all.
 */
export function fileBadgeOf(extension: string | null | undefined): string | null {
  if (!extension) return null
  const e = extension.replace(/^\./, '').toLowerCase()
  // Long extensions would overflow the 8px banner; four characters is its limit.
  return e.length === 0 ? null : e.slice(0, 4)
}

/** True when the extension maps to a real card format. Anything else falls back to
 *  the plain page, and its badge has to be suppressed: now that every file type is
 *  accepted, a .dwg stamped TXT states something about the file that is not true. */
export function isKnownFormat(extension: string | null | undefined): boolean {
  if (!extension) return false
  return extension.replace(/^\./, '').toLowerCase() in EXTENSION_FORMATS
}

export function fileFormatOf(extension: string | null | undefined): FormatFileProps {
  if (!extension) return "txt";
  return EXTENSION_FORMATS[extension.replace(/^\./, "").toLowerCase()] ?? "txt";
}

export function formatBytes(size: number) {
  if (!size) return '—'
  if (size < 1000) return `${size} بايت`
  const units = ['كب', 'مب', 'جب', 'تب']
  let value = size
  for (const unit of units) {
    value /= 1000
    if (value < 1000 || unit === 'تب') {
      return `${value >= 100 ? Math.round(value) : Number(value.toFixed(value >= 10 ? 1 : 2))} ${unit}`
    }
  }
  return '—'
}

/** Extensions the browser can paint directly, so the register can show the real
 *  picture instead of a placeholder card. Deliberately narrow: TIFF is a valid
 *  attachment here but no browser renders it, and BMP is too heavy to thumbnail. */
const IMAGE_EXTENSIONS = new Set(['jpg', 'jpeg', 'png', 'gif', 'webp', 'svg', 'avif'])

export function isImageExtension(extension: string | null | undefined): boolean {
  if (!extension) return false
  return IMAGE_EXTENSIONS.has(extension.replace(/^\./, '').toLowerCase())
}
