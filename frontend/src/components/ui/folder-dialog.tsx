import * as React from 'react'
import { X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { FOLDER_COLORS, FOLDER_ICONS, folderGlyphUrl } from '@/lib/folderStyles'
import { LabeledInput } from '@/components/ui/labeled-input'
import {
  Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'

/* One folder dialog for the whole app. It used to live inside the file browser,
   which left the toolbar's «+ مجلد» asking for a bare name through the generic
   prompt — two ways to make a folder, one of which could not pick a colour. */

export function FolderGlyph({
  className,
  color,
  icon,
}: {
  className?: string
  color?: string | null
  icon?: string | null
}) {
  const Symbol_ = icon ? FOLDER_ICONS[icon]?.icon : null
  return (
    <span className={cn('fb__folderwrap', className)} aria-hidden="true">
      <img src={folderGlyphUrl(color ?? 'blue')} alt="" draggable={false} className="fb__folderimg" />
      {Symbol_ && <Symbol_ className="fb__foldericon" />}
    </span>
  )
}

export type FolderDraft = { name: string; color: string; icon: string | null }

const BLANK: FolderDraft = { name: '', color: 'blue', icon: null }

/**
 * Create or edit a folder: name, colour, symbol.
 *
 * The draft is seeded once, at mount — remount the dialog with a `key` that
 * changes per folder so a second opening does not keep the first one's name.
 */
export function FolderDialog({
  open, mode, initial, onOpenChange, onSubmit,
}: {
  open: boolean
  mode: 'create' | 'edit'
  initial?: FolderDraft
  onOpenChange: (open: boolean) => void
  onSubmit: (draft: FolderDraft) => void
}) {
  const [draft, setDraft] = React.useState<FolderDraft>(initial ?? BLANK)
  const set = <K extends keyof FolderDraft>(key: K, value: FolderDraft[K]) =>
    setDraft((d) => ({ ...d, [key]: value }))

  const submit = () => {
    const name = draft.name.trim()
    if (!name) return
    onSubmit({ ...draft, name })
    onOpenChange(false)
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{mode === 'edit' ? 'تعديل المجلد' : 'مجلد جديد'}</DialogTitle>
        </DialogHeader>

        <div className="fb__newfolder">
          <FolderGlyph className="fb__newpreview" color={draft.color} icon={draft.icon} />
          <LabeledInput
            autoFocus
            label="اسم المجلد"
            containerClassName="fb__newname"
            value={draft.name}
            onChange={(e) => set('name', e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') submit() }}
          />
        </div>

        <div className="fb__pickgroup">
          <span className="fb__picklabel">اللون</span>
          <div className="fb__swatches">
            {Object.entries(FOLDER_COLORS).map(([key, c]) => (
              <button key={key} type="button" title={c.label} aria-label={c.label}
                aria-pressed={draft.color === key}
                className={cn('fb__swatch', draft.color === key && 'fb__swatch--on')}
                style={{ background: `linear-gradient(160deg, ${c.front}, ${c.back})` }}
                onClick={() => set('color', key)} />
            ))}
          </div>
        </div>

        <div className="fb__pickgroup">
          <span className="fb__picklabel">الرمز</span>
          <div className="fb__symbols">
            <button type="button" title="بلا رمز" aria-label="بلا رمز"
              aria-pressed={draft.icon === null}
              className={cn('fb__symbol', draft.icon === null && 'fb__symbol--on')}
              onClick={() => set('icon', null)}>
              <X size={15} />
            </button>
            {Object.entries(FOLDER_ICONS).map(([key, entry]) => (
              <button key={key} type="button" title={entry.label} aria-label={entry.label}
                aria-pressed={draft.icon === key}
                className={cn('fb__symbol', draft.icon === key && 'fb__symbol--on')}
                onClick={() => set('icon', key)}>
                <entry.icon size={15} />
              </button>
            ))}
          </div>
        </div>

        <DialogFooter>
          <button type="button" className="btn btn-ghost" onClick={() => onOpenChange(false)}>إلغاء</button>
          <button type="button" className="btn" disabled={!draft.name.trim()} onClick={submit}>
            {mode === 'edit' ? 'حفظ' : 'إنشاء'}
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
