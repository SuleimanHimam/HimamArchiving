/** The appearance model behind «تنسيق العنصر» — kept out of the panel file so that module
 *  exports a component and nothing else, and so the designer can read an element without it. */

/** Everything about how one element looks, read from what it currently renders. */
export interface ElementFormat {
  fontFamily: string
  fontSizePt: number
  bold: boolean
  italic: boolean
  underline: boolean
  color: string
  fill: string | null
  align: string
  lineHeight: string
  borderStyle: string
  borderWidth: number
  borderColor: string
  radius: number
  padding: number
  opacity: number
  width: string
  height: string
}

const px = (v: string) => Math.round(parseFloat(v) || 0)
const hex = (v: string): string => {
  // getComputedStyle answers in rgb(); <input type=color> only speaks hex.
  const m = v.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/)
  if (!m) return v.startsWith('#') ? v : '#000000'
  return '#' + [1, 2, 3].map((i) => Number(m[i]).toString(16).padStart(2, '0')).join('')
}
const transparent = (v: string) => !v || v === 'transparent' || /rgba?\([^)]*,\s*0\s*\)$/.test(v)

/** Read an element's current appearance into the panel's shape. */
export function readFormat(el: HTMLElement): ElementFormat {
  const c = getComputedStyle(el)
  return {
    fontFamily: c.fontFamily.split(',')[0].replace(/["']/g, '').trim(),
    fontSizePt: Math.round((parseFloat(c.fontSize) || 16) * 0.75),   // px → pt
    bold: Number(c.fontWeight) >= 600 || c.fontWeight === 'bold',
    italic: c.fontStyle === 'italic',
    underline: c.textDecorationLine.includes('underline'),
    color: hex(c.color),
    fill: transparent(c.backgroundColor) ? null : hex(c.backgroundColor),
    align: c.textAlign === 'start' ? 'right' : c.textAlign,
    lineHeight: el.style.lineHeight || '',
    borderStyle: c.borderTopStyle || 'none',
    borderWidth: px(c.borderTopWidth),
    borderColor: hex(c.borderTopColor),
    radius: px(c.borderTopLeftRadius),
    padding: px(c.paddingTop),
    opacity: Math.round((parseFloat(c.opacity) || 1) * 100),
    width: el.style.width || '',
    height: el.style.height || '',
  }
}

/** The panel's shape back into CSS. Only what changed is written. */
export function formatToCss(f: ElementFormat, was: ElementFormat): Partial<CSSStyleDeclaration> {
  const css: Record<string, string> = {}
  if (f.fontFamily !== was.fontFamily) css.fontFamily = f.fontFamily
  if (f.fontSizePt !== was.fontSizePt) css.fontSize = `${f.fontSizePt}pt`
  if (f.bold !== was.bold) css.fontWeight = f.bold ? '700' : '400'
  if (f.italic !== was.italic) css.fontStyle = f.italic ? 'italic' : 'normal'
  if (f.underline !== was.underline) css.textDecorationLine = f.underline ? 'underline' : 'none'
  if (f.color !== was.color) css.color = f.color
  if (f.fill !== was.fill) css.backgroundColor = f.fill ?? 'transparent'
  if (f.align !== was.align) css.textAlign = f.align
  if (f.lineHeight !== was.lineHeight) css.lineHeight = f.lineHeight
  if (f.borderStyle !== was.borderStyle || f.borderWidth !== was.borderWidth || f.borderColor !== was.borderColor) {
    css.border = f.borderStyle === 'none' ? 'none' : `${f.borderWidth || 1}px ${f.borderStyle} ${f.borderColor}`
  }
  if (f.radius !== was.radius) css.borderRadius = `${f.radius}px`
  if (f.padding !== was.padding) css.padding = `${f.padding}px`
  if (f.opacity !== was.opacity) css.opacity = String(f.opacity / 100)
  if (f.width !== was.width) css.width = f.width
  if (f.height !== was.height) css.height = f.height
  return css as Partial<CSSStyleDeclaration>
}
