import { useEffect, useState, type ReactNode } from 'react'
import { Dialog, DialogContent } from './ui/dialog'
import { FolderGlyph } from './ui/folder-dialog'
import { documents, DOC_STATUS_LABELS, type DocumentProperties } from '../lib/documents'
import { clearanceLabel } from '../lib/shared'
import { formatBytes, fileFormatOf, fileBadgeOf } from '../lib/fileFormats'
import { FileCard } from './ui/file-card-collections'
import { useDateFormat } from '../lib/useCalendar'

type Props = {
  documentId: number
  /** Shown while the card is still loading, so the dialog is never blank. */
  fallbackTitle?: string
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
 * Read-only card for one document: what it is, who put it here, and every file
 * attached to it with its size and uploader.
 *
 * Its own endpoint rather than the detail payload — the uploader is a user id on
 * the row, and turning ids into names is a join the detail mapper does not do.
 */
export default function DocumentPropertiesDialog({ documentId, fallbackTitle, onClose }: Props) {
  const [info, setInfo] = useState<DocumentProperties | null>(null)
  const [error, setError] = useState('')
  const fmt = useDateFormat()

  useEffect(() => {
    let alive = true
    documents.properties(documentId)
      .then((d) => { if (alive) setInfo(d) })
      .catch(() => { if (alive) setError('تعذّر قراءة خصائص الوثيقة') })
    return () => { alive = false }
  }, [documentId])

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose() }}>
      <DialogContent className="docprops">
        <header className="docprops__head">
          <h2 className="docprops__title">خصائص</h2>
          <span className="docprops__ref">{info?.title ?? fallbackTitle ?? ''}</span>
        </header>

        <div className="docprops__body">
          {error && <p className="login__error">{error}</p>}
          {!info && !error && <p className="docprops__loading">جارٍ التحميل…</p>}

          {info && (
            <>
              <section className="docprops__sec">
                <Row label="رقم الوثيقة" value={info.documentNumber} />
                <Row label="النوع" value={info.documentTypeName} />
                <Row label="الحالة" value={DOC_STATUS_LABELS[info.status] ?? info.status} />
                <Row label="السرّية" value={clearanceLabel(info.confidentiality)} />
                <Row label="الإصدار" value={String(info.version)} />
                <Row label="تاريخ الوثيقة" value={fmt.date(info.documentDate)} />
              </section>

              <div className="docprops__sectitle">الإضافة والتعديل</div>
              <section className="docprops__sec">
                <Row label="أضافها" value={info.createdByName} />
                <Row label="تاريخ الإضافة" value={fmt.dateTime(info.createdAt)} />
                <Row label="آخر تعديل بواسطة" value={info.updatedByName} />
                <Row label="تاريخ آخر تعديل" value={info.updatedAt ? fmt.dateTime(info.updatedAt) : null} />
                <Row label="المجلد" value={info.folderName}
                  art={<FolderGlyph className="docprops__folderart"
                    color={info.folderColor} icon={info.folderIcon} />} />
                <Row label="مكان الحفظ" value={info.physicalLocationName} />
              </section>

              <div className="docprops__sectitle">
                الملفات المرفقة
                {/* The count and the total are the two numbers people come here for,
                    so they sit in the heading rather than at the end of the list. */}
                <span className="docprops__total">
                  {info.attachmentCount} ملف · {formatBytes(info.totalSizeBytes)}
                </span>
              </div>

              {info.attachments.length === 0 ? (
                <p className="docprops__empty">لا توجد ملفات مرفقة بهذه الوثيقة</p>
              ) : (
                <ul className="docprops__files">
                  {info.attachments.map((a) => (
                    <li key={a.id} className="docprops__file">
                      <span className="docprops__fileart">
                        <FileCard formatFile={fileFormatOf(a.fileExtension)}
                          badgeLabel={fileBadgeOf(a.fileExtension) ?? undefined} />
                      </span>
                      <span className="docprops__filemain">
                        <span className="docprops__filename" title={a.fileName}>{a.fileName}</span>
                        <span className="docprops__filemeta">
                          {formatBytes(a.sizeBytes)}
                          {a.uploadedByName ? ` · رفعه ${a.uploadedByName}` : ''}
                          {` · ${fmt.dateTime(a.createdAt)}`}
                          {a.isScanned ? ' · ممسوح ضوئيًا' : ''}
                        </span>
                      </span>
                    </li>
                  ))}
                </ul>
              )}
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
