import {
  Briefcase, FileText, Heart, Image as ImageIcon, Lock, Star, Tag, Users,
} from 'lucide-react'

/* The folder look — palette, symbols, and the glyph itself. Kept out of the
   dialog component so both can be imported without breaking fast refresh. */

/** Folder tints, as the two gradient stops the glyph is drawn from. */
export const FOLDER_COLORS: Record<string, { label: string; back: string; front: string }> = {
  blue:   { label: 'أزرق',   back: '#1d84dd', front: '#7accfb' },
  slate:  { label: 'رمادي',  back: '#5b6472', front: '#9aa4b2' },
  red:    { label: 'أحمر',   back: '#c0392b', front: '#f1786a' },
  orange: { label: 'برتقالي', back: '#d1741a', front: '#f9b168' },
  amber:  { label: 'ذهبي',   back: '#c69214', front: '#f6d06a' },
  green:  { label: 'أخضر',   back: '#1f8b4c', front: '#6ed49a' },
  teal:   { label: 'فيروزي', back: '#12868f', front: '#5fd3da' },
  purple: { label: 'بنفسجي', back: '#6c3fbe', front: '#b18cf5' },
  pink:   { label: 'وردي',   back: '#c02f6a', front: '#f785ae' },
}

/** Symbols that can sit on the folder face. */
export const FOLDER_ICONS: Record<string, { label: string; icon: typeof Star }> = {
  star:      { label: 'نجمة',   icon: Star },
  heart:     { label: 'قلب',    icon: Heart },
  briefcase: { label: 'حقيبة',  icon: Briefcase },
  lock:      { label: 'قفل',    icon: Lock },
  tag:       { label: 'وسم',    icon: Tag },
  users:     { label: 'فريق',   icon: Users },
  image:     { label: 'صور',    icon: ImageIcon },
  file:      { label: 'ملفات',  icon: FileText },
}

// Drawn inline so it needs no asset pipeline. The gradient ids are suffixed per
// colour: two data URLs on one page would otherwise share ids and the second
// folder would pick up the first one's gradient.
export function folderGlyphUrl(colorKey: string) {
  const c = FOLDER_COLORS[colorKey] ?? FOLDER_COLORS.blue
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 50" width="64" height="50"><defs><linearGradient id="b${colorKey}" x1="0" x2="0" y1="0" y2="1"><stop offset="0" stop-color="${c.front}"/><stop offset="1" stop-color="${c.back}"/></linearGradient><linearGradient id="f${colorKey}" x1="0" x2="0" y1="0" y2="1"><stop offset="0" stop-color="${c.front}"/><stop offset="1" stop-color="${c.back}"/></linearGradient></defs><path d="M5 10c0-3.31 2.69-6 6-6h10.9c1.6 0 3.13.7 4.18 1.9l1.5 1.73a3.5 3.5 0 0 0 2.64 1.22H54c2.76 0 5 2.24 5 5V40c0 3.87-3.13 7-7 7H12c-3.87 0-7-3.13-7-7V10Z" fill="url(#b${colorKey})" opacity="0.75"/><path d="M5 15.5h54V40c0 3.87-3.13 7-7 7H12c-3.87 0-7-3.13-7-7V15.5Z" fill="url(#f${colorKey})"/></svg>`
  return `data:image/svg+xml,${encodeURIComponent(svg)}`
}
