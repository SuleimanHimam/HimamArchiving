import { useEffect, useState, type ReactNode } from 'react'
import { Dialog, DialogContent } from './ui/dialog'
import { FolderGlyph } from './ui/folder-dialog'
import { filesApi, type UserFileProperties } from '../lib/files'
import { formatBytes, fileFormatOf, fileBadgeOf } from '../lib/fileFormats'
import { FileCard } from './ui/file-card-collections'
import { useDateFormat } from '../lib/useCalendar'

type Props = {
  fileId: number
  fallbackName?: string
  onClose: () => void
}

function Row({ label, value, art }: {
  label: string
  value: string | null | undefined
  /** Drawn before the value — used to give the folder row the folder's own colour. */
  art?: ReactNode
}) {
  return (
    <div className="docprops__row">
      <span className="docprops__key">{label}</span>
      <span className="docprops__val">{value ? <>{art}{value}</> : '—'}</span>
    </div>
  )
}

/**
 * Properties for a plain file.
 *
 * Deliberately shorter than a document's card, and shares its styling rather than
 * its component: a file has no number, no type, no confidentiality and no retention,
 * and a dialog with six empty rows would suggest those fields were merely unfilled.
 */
export default function FilePropertiesDialog({ fileId, fallbackName, onClose }: Props) {
  const [info, setInfo] = useState<UserFileProperties | null>(null)
  const [error, setError] = useState('')
  const fmt = useDateFormat()

  useEffect(() => {
    let alive = true
    filesApi.properties(fileId)
      .then((d) => { if (alive) setInfo(d) })
      .catch(() => { if (alive) setError('تعذّر قراءة خصائص الملف') })
    return () => { alive = false }
  }, [fileId])

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose() }}>
      <DialogContent className="docprops">
        <header className="docprops__head">
          <h2 className="docprops__title">خصائص الملف</h2>
          <span className="docprops__ref">{info?.name ?? fallbackName ?? ''}</span>
        </header>

        <div className="docprops__body">
          {error && <p className="login__error">{error}</p>}
          {!info && !error && <p className="docprops__loading">جارٍ التحميل…</p>}

          {info && (
            <>
              <div className="docprops__file">
                <span className="docprops__fileart">
                  <FileCard formatFile={fileFormatOf(info.fileExtension)}
                    hideBadge={!info.fileExtension}
                    badgeLabel={fileBadgeOf(info.fileExtension) ?? undefined} />
                </span>
                <span className="docprops__filemain">
                  <span className="docprops__filename" title={info.name}>{info.name}</span>
                  <span className="docprops__filemeta">
                    {formatBytes(info.sizeBytes)}
                    {info.contentDestroyed ? ' · المحتوى مُتلَف' : ''}
                  </span>
                </span>
              </div>

              <section className="docprops__sec">
                <Row label="النوع" value={info.fileExtension ? info.fileExtension.toUpperCase() : null} />
                <Row label="نوع المحتوى" value={info.contentType} />
                <Row label="الحجم" value={formatBytes(info.sizeBytes)} />
                <Row label="المجلد" value={info.folderName}
                  art={<FolderGlyph className="docprops__folderart"
                    color={info.folderColor} icon={info.folderIcon} />} />
              </section>

              <div className="docprops__sectitle">الرفع والتعديل</div>
              <section className="docprops__sec">
                <Row label="رفعه" value={info.createdByName} />
                <Row label="تاريخ الرفع" value={fmt.dateTime(info.createdAt)} />
                <Row label="آخر تعديل بواسطة" value={info.updatedByName} />
                <Row label="تاريخ آخر تعديل" value={info.updatedAt ? fmt.dateTime(info.updatedAt) : null} />
              </section>
            </>
          )}
        </div>

        <footer className="docprops__foot">
          <button type="button" className="btn btn-ghost" onClick={onClose}>إغلاق</button>
        </footer>
      </DialogContent>
    </Dialog>
  )
}
