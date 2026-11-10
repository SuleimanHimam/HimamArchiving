import { useEffect, useMemo } from 'react'
import { File, FileText, FileType, FileSpreadsheet, X } from 'lucide-react'

const fmtBytes = (n: number) =>
  n < 1024 ? `${n} B` : n < 1048576 ? `${Math.round(n / 1024)} KB` : `${(n / 1048576).toFixed(1)} MB`

const extOf = (name: string) => (name.split('.').pop() || '').toUpperCase().slice(0, 4)
const kindOf = (ext: string) =>
  ext === 'PDF' ? 'pdf' : /^DOCX?$/.test(ext) ? 'doc' : /^XLSX?$/.test(ext) ? 'xls' : 'other'
const iconFor = (ext: string) =>
  ext === 'PDF' ? FileType : /^DOCX?$/.test(ext) ? FileText : /^XLSX?$/.test(ext) ? FileSpreadsheet : File

/** One attachment card: image files get a live thumbnail; others get a type icon + badge + size. */
export default function AttachmentCard({ file, onRemove, removeLabel }: { file: File; onRemove: () => void; removeLabel: string }) {
  const isImg = file.type.startsWith('image/')
  const url = useMemo(() => (isImg ? URL.createObjectURL(file) : null), [file, isImg])
  useEffect(() => () => { if (url) URL.revokeObjectURL(url) }, [url])
  const ext = extOf(file.name)
  return (
    <div className="cx-att">
      <div className="cx-att__thumb" data-kind={kindOf(ext)}>
        {isImg && url ? <img src={url} alt="" /> : (() => { const Ic = iconFor(ext); return <Ic size={20} aria-hidden /> })()}
      </div>
      <div className="cx-att__meta">
        <span className="cx-att__name" title={file.name}>{file.name}</span>
        <span className="cx-att__badges">
          <span className="cx-att__badge">{ext || 'FILE'}</span>
          <span className="cx-att__size">{fmtBytes(file.size)}</span>
        </span>
      </div>
      <button type="button" className="cx-att__x" onClick={onRemove} aria-label={removeLabel}><X size={14} aria-hidden /></button>
    </div>
  )
}
