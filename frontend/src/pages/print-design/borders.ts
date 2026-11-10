import type { PageBorder } from '../../lib/reportTemplates'

/** The model behind «الحدود والتظليل» — kept out of the dialog file so the component module
 *  exports a component and nothing else (fast refresh), and so the designer can read it too. */

export type BorderSetting = 'none' | 'box' | 'shadow' | 'custom'
export type BorderStyle = 'solid' | 'dashed' | 'dotted' | 'double'
export type Edge = 'top' | 'right' | 'bottom' | 'left'
export type BordersTab = 'borders' | 'page' | 'shading'

export interface BordersDraft {
  setting: BorderSetting
  style: BorderStyle
  color: string
  width: number                                   // pt
  edges: Record<Edge, boolean>
  /** تظليل: the fill behind the paragraph. null = بلا لون. */
  fill: string | null
}

export const DEFAULT_BORDERS: BordersDraft = {
  setting: 'none', style: 'solid', color: '#1f2937', width: 1,
  edges: { top: true, right: true, bottom: true, left: true }, fill: null,
}

export const BORDER_STYLES: { k: BorderStyle; label: string }[] = [
  { k: 'solid', label: 'مصمت' }, { k: 'dashed', label: 'شرطات' },
  { k: 'dotted', label: 'منقّط' }, { k: 'double', label: 'مزدوج' },
]
export const BORDER_WIDTHS = [0.5, 1, 1.5, 2, 3, 4.5, 6]
export const BORDER_SETTINGS: { k: BorderSetting; label: string }[] = [
  { k: 'none', label: 'بلا' }, { k: 'box', label: 'إطار' },
  { k: 'shadow', label: 'ظل' }, { k: 'custom', label: 'مخصّص' },
]

/** The draft as a page border, or null when the setting is «بلا». */
export function draftToPageBorder(d: BordersDraft): PageBorder | null {
  if (d.setting === 'none') return null
  return { style: d.style, width: d.width, color: d.color, edges: { ...d.edges }, shadow: d.setting === 'shadow' }
}

/** A page border read back into a draft, so «حدود الصفحة» opens on what the page already has. */
export function pageBorderToDraft(b: PageBorder, base: BordersDraft): BordersDraft {
  return {
    ...base, setting: b.shadow ? 'shadow' : 'box', style: b.style,
    width: b.width, color: b.color, edges: { ...b.edges },
  }
}

/** The draft as the inline CSS a paragraph carries. Empty strings clear what was there. */
export function draftToBlockCss(d: BordersDraft, tab: BordersTab): Record<string, string> {
  if (tab === 'shading') return { backgroundColor: d.fill ?? '' }
  const line = d.setting === 'none' ? '' : `${d.width}pt ${d.style} ${d.color}`
  return {
    borderTop: d.edges.top ? line : '',
    borderRight: d.edges.right ? line : '',
    borderBottom: d.edges.bottom ? line : '',
    borderLeft: d.edges.left ? line : '',
    boxShadow: d.setting === 'shadow' ? `3px 3px 0 0 ${d.color}` : '',
    // Word insets the text from its frame; without it the border sits on the glyphs.
    padding: d.setting === 'none' ? '' : '4px 8px',
  }
}
